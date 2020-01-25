# Workflow Foundation Internals (I)
Andrew Au

Inspired by the book [Essential Windows Workflow Foundation][1] that describes the last version of Workflow Foundation, I can’t stop myself trying to write an equivalent piece for WF4. While the basic working principle is fundamentally the same, the programming model is quite different. We will start from the same principle of using serialization of delegate, and we will develop our ‘home-made’ workflow runtime, and we will see how WF4 made its design decisions.
First of all, let’s us review the underlying CLR technology for continuation. Continuation is a point that can be saved for resuming execution, and therefore it needs to contain pointer to executable code. Delegate is used for this purpose. The great thing about delegate can be round-tripped to a binary store and back remain executable. Here is a code sample to show the roundtrip of that delegate works.

```C#
namespace SerializeDelegate
{
    using System;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;

    [Serializable]
    public class Program
    {
        string greeting;

        public Program(string greeting)
        {
            this.greeting = greeting;
        }

        public static void Main()
        {
            Func<string, string> func = new Program("Hello {0}!").Greet;
            using (FileStream stream = File.Create(@"c:\delegate.dat"))
            {
                new BinaryFormatter().Serialize(stream, func);
                stream.Close();
            }
            using (FileStream stream = File.OpenRead(@"c:\delegate.dat"))
            {
                Console.WriteLine(((Func<string, string>)new BinaryFormatter().Deserialize(stream))("Andrew"));
                stream.Close();
            }
            File.Delete(@"c:\delegate.dat");
        }

        public string Greet(string name)
        {
            return string.Format(this.greeting, name);
        }
    }
}
```

Serializable delegate provided us with a mechanism to suspend a running managed thread, and resume in another process (perhaps on another machine). Doing so has a lot of advantages. The most important one is that we remove the ‘affinities’. The code is no longer stuck to original process or even the original machine. This allows us to scale the application by simply adding more machines. Moreover, we now have control. For example, we could delete the serialized delegate instead of resuming it. By doing so, we essentially canceled the execution. Similarly, we could suspend the execution for days without worrying main memory are being used. Let us take a look at this code sample to see how serializable delegate allow us to break a program into several processes.

```C#
namespace HomeMadeWF
{
    using System;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;

    // We want this program to read two files and output the concatenated version
    // Run this program three times to see "Hello world to homemade workflow foundation!"
    [Serializable]
    public class Program
    {
        const string stateFileName = @"c:\execution.dat";
        string file1Content;
        string file2Content;

        public static void Main()
        {
            if (File.Exists(stateFileName))
            {
                ContinueAt()();
            }
            else
            {
                SetupProgramExecution();
                new Program().RunStep1();
            }
        }

        public void RunStep1()
        {
            file1Content = File.ReadAllText(@"c:\file1.txt");
            SaveAt(this.RunStep2);
        }

        public void RunStep2()
        {
            file2Content = File.ReadAllText(@"c:\file2.txt");
            SaveAt(this.RunStep3);
        }

        public void RunStep3()
        {
            Console.WriteLine(file1Content + file2Content);
            File.Delete(stateFileName);
            CleanupProgramExecution();
        }

        static void SetupProgramExecution()
        {
            File.WriteAllText(@"c:\file1.txt", "Hello world to ");
            File.WriteAllText(@"c:\file2.txt", "homemade workflow foundation!");
        }

        static void CleanupProgramExecution()
        {
            File.Delete(@"c:\file1.txt");
            File.Delete(@"c:\file2.txt");
        }

        static void SaveAt(Action action)
        {
            using (FileStream stream = File.Create(stateFileName))
            {
                new BinaryFormatter().Serialize(stream, action);
                stream.Close();
            }
        }

        static Action ContinueAt()
        {
            using (FileStream stream = File.OpenRead(stateFileName))
            {
                Action a = ((Action)new BinaryFormatter().Deserialize(stream));
                stream.Close();
                return a;
            }
        }
    }
}
```

Run the program three times, you will see “Hello world to homemade workflow foundation!” is displayed on the console. With the code above, we have just engineered our first most primitive workflow application using the code above. Needless to say, this is rough and there are a lot of things we can improve upon. We will take our first step to separate the concern of scalability (i.e. the fact that we are serializing delegates from the business logic). For that, we create a new class named `ReadReadWrite`, move the business logic method there, and that leads us to the version 2.

![1-to-2.png][pic1]

Host.cs
```C#
namespace HomeMadeWF
{
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;

    public static class Host
    {
        const string stateFileName = @"c:\execution.dat";

        public static void Main()
        {
            while (true)
            {
                if (File.Exists(stateFileName))
                {
                    ReadReadWrite continuation = ContinueAt();
                    continuation.NextDelegate();
                    if (continuation.NextDelegate == null)
                    {
                        File.Delete(stateFileName);
                        CleanupProgramExecution();
                        return;
                    }
                    else
                    {
                        SaveAt(continuation);
                    }
                }
                else
                {
                    SetupProgramExecution();
                    ReadReadWrite workflow = new ReadReadWrite();
                    workflow.RunStep1();
                    SaveAt(workflow);
                }
            }
        }

        static void SetupProgramExecution()
        {
            File.WriteAllText(@"c:\file1.txt", "Hello world to ");
            File.WriteAllText(@"c:\file2.txt", "homemade workflow foundation!");
        }

        static void CleanupProgramExecution()
        {
            File.Delete(@"c:\file1.txt");
            File.Delete(@"c:\file2.txt");
        }

        static void SaveAt(ReadReadWrite workflow)
        {
            using (FileStream stream = File.Create(stateFileName))
            {
                new BinaryFormatter().Serialize(stream, workflow);
                stream.Close();
            }
        }

        static ReadReadWrite ContinueAt()
        {
            using (FileStream stream = File.OpenRead(stateFileName))
            {
                ReadReadWrite continuation = (ReadReadWrite)new BinaryFormatter().Deserialize(stream);
                stream.Close();
                return continuation;
            }
        }
    }
}
```

ReadReadWrite.cs
```C#
namespace HomeMadeWF
{
    using System;
    using System.IO;

    [Serializable]
    public class ReadReadWrite
    {
        string file1Content;
        string file2Content;

        public Action NextDelegate { get; set; }

        public void RunStep1()
        {
            file1Content = File.ReadAllText(@"c:\file1.txt");
            this.NextDelegate = new Action(this.RunStep2);
        }

        public void RunStep2()
        {
            file2Content = File.ReadAllText(@"c:\file2.txt");
            this.NextDelegate = new Action(this.RunStep3);
        }

        public void RunStep3()
        {
            Console.WriteLine(file1Content + file2Content);
            this.NextDelegate = null;
        }
    }
}

```

First, I put a while loop in the main program to avoid run the program three times. This is really just for convenience. It is good enough for us to know we CAN break them into different processes, but we don’t have to. The refactoring is mostly straightforward, but there is one thing that worth notice here. I made `NextDelegate` a property of `ReadReadWrite`. This is to allow all delegates to share a uniform signature.

The hosting program and the business logic are separated now. At this point, we have the still got two coupling between the host and the business logic. The host need to know the starting point is `RunStep1`, and the host need to know `NextDelegate` is the property storing the continuation. These coupling make the host not a generic one. We could remove these coupling by having a base class for `ReadReadWrite`. Let’s call it `Activity`.

![2-to-3.png][pic2]

Host.cs
```C#
namespace HomeMadeWF
{
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;

    public static class Host
    {
        const string stateFileName = @"c:\execution.dat";

        public static void Main()
        {
            while (true)
            {
                if (File.Exists(stateFileName))
                {
                    Activity continuation = ContinueAt();
                    continuation.NextDelegate();
                    if (continuation.NextDelegate == null)
                    {
                        File.Delete(stateFileName);
                        CleanupProgramExecution();
                        return;
                    }
                    else
                    {
                        SaveAt(continuation);
                    }
                }
                else
                {
                    SetupProgramExecution();
                    Activity workflow = new ReadReadWrite();
                    workflow.Execute();
                    SaveAt(workflow);
                }
            }
        }


        static void SetupProgramExecution()
        {
            File.WriteAllText(@"c:\file1.txt", "Hello world to ");
            File.WriteAllText(@"c:\file2.txt", "homemade workflow foundation!");
        }

        static void CleanupProgramExecution()
        {
            File.Delete(@"c:\file1.txt");
            File.Delete(@"c:\file2.txt");
        }

        static void SaveAt(Activity workflow)
        {
            using (FileStream stream = File.Create(stateFileName))
            {
                new BinaryFormatter().Serialize(stream, workflow);
                stream.Close();
            }
        }

        static Activity ContinueAt()
        {
            using (FileStream stream = File.OpenRead(stateFileName))
            {
                Activity continuation = (Activity)new BinaryFormatter().Deserialize(stream);
                stream.Close();
                return continuation;
            }
        }
    }
}
```

Activity.cs
```C#
namespace HomeMadeWF
{
    using System;

    [Serializable]
    public abstract class Activity
    {
        public Action NextDelegate { get; set; }

        public abstract void Execute();
    }
}
```

ReadReadWrite.cs
```C#
namespace HomeMadeWF
{
    using System;
    using System.IO;

    [Serializable]
    public class ReadReadWrite : Activity
    {
        string file1Content;
        string file2Content;

        public override void Execute()
        {
            file1Content = File.ReadAllText(@"c:\file1.txt");
            this.NextDelegate = new Action(this.RunStep2);
        }

        public void RunStep2()
        {
            file2Content = File.ReadAllText(@"c:\file2.txt");
            this.NextDelegate = new Action(this.RunStep3);
        }

        public void RunStep3()
        {
            Console.WriteLine(file1Content + file2Content);
            this.NextDelegate = null;
        }
    }
}
```

The refactoring is straightforward. Looking at `ReadReadWrite`, the code is now pretty easy to write. One thing that we don’t like is that the `Execute` method and the `RunStep2` method are essentially the same piece of logic to read a file, it is best to share the logic into reusable components. To tackle this problem, we realize there are really two hurdles. One is that they update different states, and that they return different delegates. The fact that they are returning different delegate is a problem, because these delegates are really control logic, and is orthogonal to reading a file. We made them together and therefore the logic not cohesive. Now we further optimize the structure by separating them.

![3-to-4.png][pic3]

Activity.cs
```C#
namespace HomeMadeWF
{
    using System;

    [Serializable]
    public abstract class Activity
    {
        public static States globalState = new States();
        protected States states;

        public Activity()
        {
            this.states = globalState;
        }

        public Action NextDelegate { get; set; }

        public abstract void Execute();
    }
}
```

Host.cs
```C#
namespace HomeMadeWF
{
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;

    public static class Host
    {
        const string stateFileName = @"c:\execution.dat";

        public static void Main()
        {
            while (true)
            {
                if (File.Exists(stateFileName))
                {
                    Activity continuation = ContinueAt();
                    continuation.NextDelegate();
                    if (continuation.NextDelegate == null)
                    {
                        File.Delete(stateFileName);
                        CleanupProgramExecution();
                        return;
                    }
                    else
                    {
                        SaveAt(continuation);
                    }
                }
                else
                {
                    SetupProgramExecution();
                    Activity workflow = new Sequence
                    {
                        Activities =
                        {
                            new Read1(),
                            new Read2(),
                            new Write(),
                        }
                    };
                    workflow.Execute();
                    SaveAt(workflow);
                }
            }
        }

        static void SetupProgramExecution()
        {
            File.WriteAllText(@"c:\file1.txt", "Hello world to ");
            File.WriteAllText(@"c:\file2.txt", "homemade workflow foundation!");
        }

        static void CleanupProgramExecution()
        {
            File.Delete(@"c:\file1.txt");
            File.Delete(@"c:\file2.txt");
        }

        static void SaveAt(Activity workflow)
        {
            using (FileStream stream = File.Create(stateFileName))
            {
                new BinaryFormatter().Serialize(stream, workflow);
                stream.Close();
            }
        }

        static Activity ContinueAt()
        {
            using (FileStream stream = File.OpenRead(stateFileName))
            {
                Activity continuation = (Activity)new BinaryFormatter().Deserialize(stream);
                stream.Close();
                return continuation;
            }
        }
    }
}
```

Read1.cs
```C#
namespace HomeMadeWF
{
    using System;
    using System.IO;

    [Serializable]
    public class Read1 : Activity
    {
        public override void Execute()
        {
            base.states.file1Content = File.ReadAllText(@"c:\file1.txt");
        }
    }
}
```
Read2.cs
```C#
namespace HomeMadeWF
{
    using System;
    using System.IO;

    [Serializable]
    public class Read2 : Activity
    {
        public override void Execute()
        {
            base.states.file2Content = File.ReadAllText(@"c:\file2.txt");
        }
    }
}
```
Sequence.cs
```C#
namespace HomeMadeWF
{
    using System;
    using System.IO;

    [Serializable]
    public class Read2 : Activity
    {
        public override void Execute()
        {
            base.states.file2Content = File.ReadAllText(@"c:\file2.txt");
        }
    }
}
```
Write.cs
```C#
namespace HomeMadeWF
{
    using System;

    [Serializable]
    public class States
    {
        public string file1Content;
        public string file2Content;
    }
}
```
States.cs
```C#
namespace HomeMadeWF
{
    using System;

    [Serializable]
    public class States
    {
        public string file1Content;
        public string file2Content;
    }
}
```

It is non-trivial to write the `Sequence` activity, and it is far from optimal for now. We will postpone the discussion of optimizing `Sequence` to the next post. For now I want to focus on removing the duplication of `Read1` and `Read2`. With the code above, `Read1` and `Read2` are really just reading file now. The last step to merge these two classes together is to make them access the state by name instead of a static field reference.

![4-to-5.png][pic4]
Activity.cs
```C#
namespace HomeMadeWF
{
    using System;

    [Serializable]
    public abstract class Activity
    {
        public static States globalState = new States();
        protected States states;

        public Activity()
        {
            this.states = globalState;
        }

        public Action NextDelegate { get; set; }

        public abstract void Execute();
    }
}
```
Host.cs
```C#
namespace HomeMadeWF
{
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;

    public static class Host
    {
        const string stateFileName = @"c:\execution.dat";

        public static void Main()
        {
            while (true)
            {
                if (File.Exists(stateFileName))
                {
                    Activity continuation = ContinueAt();
                    continuation.NextDelegate();
                    if (continuation.NextDelegate == null)
                    {
                        File.Delete(stateFileName);
                        CleanupProgramExecution();
                        return;
                    }
                    else
                    {
                        SaveAt(continuation);
                    }
                }
                else
                {
                    SetupProgramExecution();
                    Activity workflow = new Sequence
                    {
                        Activities =
                        {
                            new Read { ContentKey = "file1Content", FileName = @"c:\file1.txt" },
                            new Read { ContentKey = "file2Content", FileName = @"c:\file2.txt" },
                            new Write { Content1Key = "file1Content", Content2Key = "file2Content" }
                        }
                    };
                    workflow.Execute();
                    SaveAt(workflow);
                }
            }
        }

        static void SetupProgramExecution()
        {
            File.WriteAllText(@"c:\file1.txt", "Hello world to ");
            File.WriteAllText(@"c:\file2.txt", "homemade workflow foundation!");
        }

        static void CleanupProgramExecution()
        {
            File.Delete(@"c:\file1.txt");
            File.Delete(@"c:\file2.txt");
        }

        static void SaveAt(Activity workflow)
        {
            using (FileStream stream = File.Create(stateFileName))
            {
                new BinaryFormatter().Serialize(stream, workflow);
                stream.Close();
            }
        }

        static Activity ContinueAt()
        {
            using (FileStream stream = File.OpenRead(stateFileName))
            {
                Activity continuation = (Activity)new BinaryFormatter().Deserialize(stream);
                stream.Close();
                return continuation;
            }
        }
    }
}
```
Read.cs
```C#
namespace HomeMadeWF
{
    using System;
    using System.IO;

    [Serializable]
    public class Read : Activity
    {
        public string FileName { get; set; }

        public string ContentKey { get; set; }

        public override void Execute()
        {
            base.states.Contents[this.ContentKey] = File.ReadAllText(this.FileName);
        }
    }
}
```
Sequence.cs
```C#
namespace HomeMadeWF
{
    using System;
    using System.Collections.ObjectModel;

    [Serializable]
    public class Sequence : Activity
    {
        Collection<Activity> activities;
        int sequenceCounter;
        bool executingChildActivity;

        public Collection<Activity> Activities
        {
            get
            {
                if (this.activities == null)
                {
                    activities = new Collection<Activity>();
                }
                return this.activities;
            }
        }

        public override void Execute()
        {
            Activity currentActivity = activities[sequenceCounter];
            this.NextDelegate = new Action(this.Execute);
            if (currentActivity.NextDelegate != null)
            {
                activities[sequenceCounter].NextDelegate();
            }
            else if (!executingChildActivity)
            {
                executingChildActivity = true;
                currentActivity.Execute();
            }
            else
            {
                sequenceCounter++;
                executingChildActivity = false;
                if (sequenceCounter == this.Activities.Count)
                {
                    this.NextDelegate = null;
                }
            }
        }
    }
}
```
States.cs
```C#
namespace HomeMadeWF
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class States
    {
        public Dictionary<string, string> Contents = new Dictionary<string, string>();
    }
}
```

Now we have reached a state where we can write reusable activity. These activities don’t have to concern themselves with the serialization. Without reading the code of `Main`, one does not even know serialization is happening. Looking at `Read` or `Write`, does it look like our activities API? In the next post of this series, we will continue to work on this example and show why there is a parameter to the `Execute` method, and how that separates program from data.

[1]: https://www.amazon.com/Essential-Windows-Workflow-Foundation-Dharma/dp/0321399838
[pic1]: 1-to-2.png
[pic2]: 2-to-3.png
[pic3]: 3-to-4.png
[pic4]: 4-to-5.png

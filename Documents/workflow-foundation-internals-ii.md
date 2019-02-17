# Workflow Foundation Internals (II)
Andrew Au

Continue with our last article, we will work on separating program and data. Program and data are very distinct concepts from the perspective of a programmer. For example, you can run code, but you can’t run data. Code are not expected to be changed during runtime (except some circumstances such as hook or overlay), but data are expected to be changed all the time. From the processor’s perspective, they are no different. They are just some bytes. From an operating system perspective, the fact that code is read only allow optimization of main memory usage, because a single copy of the code can be used to run multiple processes.

Exactly the same argument applies for workflow. We think of `Sequence` as the program, but then `sequenceCounter` is just execution data. The fact that `sequenceCounter` is defined on the Sequence class made them inseparable. Consider multiple workflows are running on the same definition, the `sequenceCounter` will mix up and cause problem.

Reading the code for Activity, the fact that `States` is defined on the workflow is an even bigger problem. Instead of having them accessible from the activity as a protected field, we will retrieve it from the parameter of the `Execute` method. Similarly, we should not allow `NextDelegate` to be defined on the activity as well. This one is getting more complicated, as `Sequence` execution will require storage of two delegate objects. To solve this problem, we will use a `Stack<Frame>` holder for these objects. Diligent readers are recommended to perform the exercise himself, because we are going to take a quantum leap (instead of small refactorings) to move to the next version as shown below

![5-to-6.png][pic5]

Activity.cs
```
namespace HomeMadeWF
{
    using System;

    [Serializable]
    public abstract class Activity
    {
        public abstract void Execute(States states);
    }
}
```
Frame.cs
```
namespace HomeMadeWF
{
    using System;

    [Serializable]
    public class Frame
    {
        public Action<States> NextDelegate { get; set; }
    }
}
```
Host.cs
```
namespace HomeMadeWF
{
    using System;
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
                    States states = ContinueAt();
                    Frame currentFrame = states.Frames.Peek();
                    Action<States> nextDelegate = currentFrame.NextDelegate;
                    currentFrame.NextDelegate = null;
                    nextDelegate(states);
                    while (states.Frames.Count > 0 && states.Frames.Peek().NextDelegate == null)
                    {
                        states.Frames.Pop();
                    }
                    if (states.Frames.Count == 0)
                    {
                        File.Delete(stateFileName);
                        CleanupProgramExecution();
                        return;
                    }
                    else
                    {
                        SaveAt(states);
                    }
                }
                else
                {
                    SetupProgramExecution();
                    States states = new States();
                    states.Frames.Push(new Frame());
                    Activity workflow = new Sequence()
                    {
                        Activities =
                        {
                            new Read() { ContentKey = "file1Content", FileName = @"c:\file1.txt" },
                            new Read() { ContentKey = "file2Content", FileName = @"c:\file2.txt" },
                            new Write() { Content1Key = "file1Content", Content2Key = "file2Content" }
                        }
                    };
                    workflow.Execute(states);
                    SaveAt(states);
                }
            }
        }

        static void SetupProgramExecution()
        {
            File.WriteAllText(@"c:\file1.txt", "Hello world to ");
            File.WriteAllText(@"c:\file2.txt", "home made workflow foundation!");
        }

        static void CleanupProgramExecution()
        {
            File.Delete(@"c:\file1.txt");
            File.Delete(@"c:\file2.txt");
        }

        static void SaveAt(States states)
        {
            using (FileStream stream = File.Create(stateFileName))
            {
                new BinaryFormatter().Serialize(stream, states);
                stream.Close();
            }
        }

        static States ContinueAt()
        {
            using (FileStream stream = File.OpenRead(stateFileName))
            {
                States states = (States)new BinaryFormatter().Deserialize(stream);
                stream.Close();
                return states;
            }
        }
    }
}
```
Read.cs
```
namespace HomeMadeWF
{
    using System;
    using System.IO;

    [Serializable]
    public class Read : Activity
    {
        public string FileName { get; set; }

        public string ContentKey { get; set; }

        public override void Execute(States states)
        {
            states.Contents[this.ContentKey] = File.ReadAllText(this.FileName);
        }
    }
}
```
Sequence.cs
```
namespace HomeMadeWF
{
    using System;
    using System.Collections.ObjectModel;

    [Serializable]
    public class Sequence : Activity
    {
        Collection<Activity> activities;

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

        public override void Execute(States states)
        {
            if (states.sequenceCounter != this.Activities.Count)
            {
                states.Frames.Peek().NextDelegate = this.Execute;
                states.Frames.Push(new Frame() { NextDelegate = activities[states.sequenceCounter++].Execute });
            }
        }
    }
}
```
States.cs
```
namespace HomeMadeWF
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class States
    {
        public Dictionary<string, string> Contents = new Dictionary<string, string>();
        public Stack<Frame> Frames = new Stack<Frame>();
        public int sequenceCounter;
    }
}
```
Write.cs
```
namespace HomeMadeWF
{
    using System;

    [Serializable]
    public class Write : Activity
    {
        public string Content1Key { get; set; }

        public string Content2Key { get; set; }

        public override void Execute(States states)
        {
            Console.WriteLine(states.Contents[this.Content1Key] + states.Contents[this.Content2Key]);
        }
    }
}
```

I tried to keep modification of Version 6 from Version 5 as small as possible. As we are moving the delegate storage from the activity to state, we also optimized `Sequence` execution. For now, executing a particular activity will not pass through sequence, but rather just saving the continuation of the sequence in the stack. Given the code is good now, I will refactor. In particular, we will move more logic into `States` so that the stack is maintained by state instead of being distributed in `Sequence` and `Program`. Making `Frames` a private field drives all these refactorings. All I did is really just giving the right name to the set of operations I did to the states.

Activity.cs
```
namespace HomeMadeWF
{
    using System;

    [Serializable]
    public abstract class Activity
    {
        public abstract void Execute(States states);
    }
}
```
Frame.cs
```
namespace HomeMadeWF
{
    using System;

    [Serializable]
    public class Frame
    {
        public Action<States> NextDelegate { get; set; }
    }
}
```
Host.cs
```
namespace HomeMadeWF
{
    using System;
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
                    States states = ContinueAt();
                    Action<States> nextDelegate = states.ConsumeNextDelegate();
                    nextDelegate(states);
                    if (states.HasMoreWork())
                    {
                        SaveAt(states);
                    }
                    else
                    {
                        File.Delete(stateFileName);
                        CleanupProgramExecution();
                        return;
                    }
                }
                else
                {
                    SetupProgramExecution();
                    States states = new States();
                    Activity workflow = new Sequence()
                    {
                        Activities =
                        {
                            new Read() { ContentKey = "file1Content", FileName = @"c:\file1.txt" },
                            new Read() { ContentKey = "file2Content", FileName = @"c:\file2.txt" },
                            new Write() { Content1Key = "file1Content", Content2Key = "file2Content" }
                        }
                    };
                    workflow.Execute(states);
                    SaveAt(states);
                }
            }
        }

        static void SetupProgramExecution()
        {
            File.WriteAllText(@"c:\file1.txt", "Hello world to ");
            File.WriteAllText(@"c:\file2.txt", "home made workflow foundation!");
        }

        static void CleanupProgramExecution()
        {
            File.Delete(@"c:\file1.txt");
            File.Delete(@"c:\file2.txt");
        }

        static void SaveAt(States states)
        {
            using (FileStream stream = File.Create(stateFileName))
            {
                new BinaryFormatter().Serialize(stream, states);
                stream.Close();
            }
        }

        static States ContinueAt()
        {
            using (FileStream stream = File.OpenRead(stateFileName))
            {
                States states = (States)new BinaryFormatter().Deserialize(stream);
                stream.Close();
                return states;
            }
        }
    }
}
```
Read.cs
```
namespace HomeMadeWF
{
    using System;
    using System.IO;

    [Serializable]
    public class Read : Activity
    {
        public string FileName { get; set; }

        public string ContentKey { get; set; }

        public override void Execute(States states)
        {
            states.Contents[this.ContentKey] = File.ReadAllText(this.FileName);
        }
    }
}
```
Sequence.cs
```
namespace HomeMadeWF
{
    using System;
    using System.Collections.ObjectModel;

    [Serializable]
    public class Sequence : Activity
    {
        Collection<Activity> activities;

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

        public override void Execute(States states)
        {
            if (states.sequenceCounter != this.Activities.Count)
            {
                states.ScheduleActivity(this.Activities[states.sequenceCounter++], this.Execute);
            }
        }
    }
}
```
States.cs
```
namespace HomeMadeWF
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class States
    {
        public Dictionary<string, string> Contents = new Dictionary<string, string>();
        Stack<Frame> Frames = new Stack<Frame>();
        public int sequenceCounter;

        public States()
        {
            this.Frames.Push(new Frame());
        }

        public Action<States> ConsumeNextDelegate()
        {
            Frame currentFrame = this.Frames.Peek();
            Action<States> nextDelegate = currentFrame.NextDelegate;
            currentFrame.NextDelegate = null;
            return nextDelegate;
        }

        public bool HasMoreWork()
        {
            while (this.Frames.Count > 0 && this.Frames.Peek().NextDelegate == null)
            {
                this.Frames.Pop();
            }
            return this.Frames.Count != 0;
        }

        public void ScheduleActivity(Activity activity, Action<States> callback)
        {
            this.Frames.Peek().NextDelegate = callback;
            this.Frames.Push(new Frame() { NextDelegate = activity.Execute });
        }
    }
}
```
Write.cs
```
namespace HomeMadeWF
{
    using System;

    [Serializable]
    public class Write : Activity
    {
        public string Content1Key { get; set; }

        public string Content2Key { get; set; }

        public override void Execute(States states)
        {
            Console.WriteLine(states.Contents[this.Content1Key] + states.Contents[this.Content2Key]);
        }
    }
}
```

We are getting very close now. For we are now serializing the state object, we should have completed the separation? The answer is NO. This is because the states contain a reference to the delegates, and then the delegates will link back to the program. To break this link, we cannot serialize the delegate anymore. We will serialize the MethodInfo and the activity ID as well, where activity ID can be obtained from a traversal of the activity tree. We will skip this part as this complicates our sample. Another problem is that the program is not constrained to be read only. There are multiple ways of solving it. WF3 chooses to make a copy of the program and always run the copy, while WF4 chooses to make a copy of the program (parts that is important to the runtime) and verify the main program is not modified at runtime. We could have also done by making user fail when program is changed at runtime as well. Again, this complicates the sample and we will stop our discussion here. Trying to implement or reading the existing implementation is a good way to appreciate this.
Another big problem with program and data is that a program line can be executed multiple times in a process. The simplest example is a loop. Those data has to be stored on the frame instead of the state. In fact, all states should be stored on a frame. Storing on `States` itself make them global variables. With frames, we can control variable scoping. This is one big improvement over the last version of WF.

Look at the implementation of `States`. One problem is `ScheduleActivity` can only be called once, because calling a subsequent call to `ScheduleActivity` will overwrite the `NextDelegate` field. In this next post, I will talk improve `ScheduleActivity` to allow multiple outstanding activities, and workflow-host communication through Bookmarks.

[pic5]: 5-to-6.png
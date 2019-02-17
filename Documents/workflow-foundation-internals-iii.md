# Workflow Foundation Internals (III)
Andrew Au

Continue with our last article, we will work on allowing parallelism and create the workflow/host communication pattern. This pattern is actually very common, such as waiting for multiple approvers to approve a document. Last time we notice the problem is that `ScheduleActivity` can only be called once, so the most obvious solution is to allow multiple delegates to be put in the states. Looking at `states.Frames`, it is a stack and we access the top of the stack anytime. With parallelism, we want to have a stack like data structure with multiple tops. Think about it, this is really just a rooted tree. In our data structure, we will keep all the leaves and have parent pointers for the parent frames. With multiple tops, activity execution with a pointer to the set of all states will not work, because the activity doesn’t know which frame it is in. We changed the delegate from `Action<State>` to `Action<Frame>` and the rest will become obvious.

![7-to-8.png][pic6]

Activities.cs
```
namespace HomeMadeWF
{
    using System;

    [Serializable]
    public abstract class Activity
    {
        public abstract void Execute(Frame frame);
    }
}
```
Frame.cs
```
namespace HomeMadeWF
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class Frame
    {
        Dictionary<Frame, Action<Frame>> nextDelegatesWithDependencies;
        Stack<Action<Frame>> nextDelegates;
        States states;
        Frame parentFrame;

        public Frame(States states, Frame parentFrame)
        {
            this.states = states;
            this.parentFrame = parentFrame;
        }

        public States States
        {
            get { return this.states; }
        }

        public Frame ParentFrame
        {
            get { return this.parentFrame; }
        }

        public Dictionary<Frame, Action<Frame>> NextDelegatesWithDependencies
        {
            get
            {
                if (this.nextDelegatesWithDependencies == null)
                {
                    this.nextDelegatesWithDependencies = new Dictionary<Frame, Action<Frame>>();
                }
                return this.nextDelegatesWithDependencies;
            }
        }

        public Stack<Action<Frame>> NextDelegates
        {
            get
            {
                if (this.nextDelegates == null)
                {
                    this.nextDelegates = new Stack<Action<Frame>>();
                }
                return this.nextDelegates;
            }
        }

        public void ScheduleActivity(Activity activity, Action<Frame> callback)
        {
            Frame newFrame = new Frame(this.states, this);
            newFrame.NextDelegates.Push(activity.Execute);
            this.NextDelegatesWithDependencies.Add(newFrame, callback);
            this.states.Frames.Remove(this);
            this.states.Frames.Add(newFrame);
        }
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
                    Frame frame;
                    Action<Frame> nextDelegate = states.ConsumeNextDelegate(out frame);
                    nextDelegate(frame);
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
                    Activity workflow = new Sequence
                    {
                        Activities =
                        {
                            new Read { ContentKey = "file1Content", FileName = @"c:\file1.txt" },
                            new Read { ContentKey = "file2Content", FileName = @"c:\file2.txt" },
                            new Write { Content1Key = "file1Content", Content2Key = "file2Content" }
                        }
                    };
                    workflow.Execute(states.Frames[0]);
                    SaveAt(states);
                }
            }
        }

        private static void SetupProgramExecution()
        {
            File.WriteAllText(@"c:\file1.txt", "Hello world to ");
            File.WriteAllText(@"c:\file2.txt", "home made workflow foundation!");
        }

        private static void CleanupProgramExecution()
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

        public override void Execute(Frame frame)
        {
            frame.States.Contents[this.ContentKey] = File.ReadAllText(this.FileName);
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

        public override void Execute(Frame frame)
        {
            if (frame.States.sequenceCounter != this.Activities.Count)
            {
                frame.ScheduleActivity(this.Activities[frame.States.sequenceCounter++], this.Execute);
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
    using System.Collections.ObjectModel;

    [Serializable]
    public class States
    {
        public Dictionary<string, string> Contents = new Dictionary<string, string>();
        public Collection<Frame> Frames = new Collection<Frame>();
        public int sequenceCounter;

        public States()
        {
            this.Frames.Add(new Frame(this, null));
        }

        public Action<Frame> ConsumeNextDelegate(out Frame frame)
        {
            return (frame = FindLastFrameWithWork()).NextDelegates.Pop();
        }

        public bool HasMoreWork()
        {
            Collection<Frame> candidateToRemove = new Collection<Frame>();
            bool stop = false;
            do
            {
                candidateToRemove.Clear();
                foreach (Frame frame in this.Frames)
                {
                    if (frame.NextDelegates.Count == 0 && frame.NextDelegatesWithDependencies.Count == 0)
                    {
                        candidateToRemove.Add(frame);
                    }
                }
                if (candidateToRemove.Count == 0)
                {
                    stop = true;

                }
                else
                {
                    foreach (Frame frame in candidateToRemove)
                    {
                        if (frame.ParentFrame != null)
                        {
                            this.Frames.Add(frame.ParentFrame);
                            if (frame.ParentFrame.NextDelegatesWithDependencies.ContainsKey(frame))
                            {
                                Action<Frame> nextDelegate = frame.ParentFrame.NextDelegatesWithDependencies[frame];
                                frame.ParentFrame.NextDelegatesWithDependencies.Remove(frame);
                                if (nextDelegate != null)
                                {
                                    frame.ParentFrame.NextDelegates.Push(nextDelegate);
                                }
                            }
                        }
                        this.Frames.Remove(frame);
                    }
                }
            } while (!stop);
            return this.Frames.Count != 0;
        }

        Frame FindLastFrameWithWork()
        {
            Frame frameWithWork = null;
            foreach (Frame frame in this.Frames)
            {
                if (frame.NextDelegates.Count != 0)
                {
                    frameWithWork = frame;
                }
            }
            return frameWithWork;
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

        public override void Execute(Frame frame)
        {
            Console.WriteLine(frame.States.Contents[this.Content1Key] + frame.States.Contents[this.Content2Key]);
        }
    }
}
```

We are using the old activities because we want to keep safe. No parallelism feature is used in the last sample, we will now introduce parallelism. `Parallel` is the simplest activity out of the box and we will use parallel to schedule two Reads.

![8-to-9.png][pic7]

Activity.cs
```
namespace HomeMadeWF
{
    using System;

    [Serializable]
    public abstract class Activity
    {
        public abstract void Execute(Frame frame);
    }
}
```
Frame.cs
```
namespace HomeMadeWF
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class Frame
    {
        Dictionary<Frame, Action<Frame>> nextDelegatesWithDependencies;
        Stack<Action<Frame>> nextDelegates;
        States states;
        Frame parentFrame;

        public Frame(States states, Frame parentFrame)
        {
            this.states = states;
            this.parentFrame = parentFrame;
        }

        public States States
        {
            get { return this.states; }
        }

        public Frame ParentFrame
        {
            get { return this.parentFrame; }
        }

        public Dictionary<Frame, Action<Frame>> NextDelegatesWithDependencies
        {
            get
            {
                if (this.nextDelegatesWithDependencies == null)
                {
                    this.nextDelegatesWithDependencies = new Dictionary<Frame, Action<Frame>>();
                }
                return this.nextDelegatesWithDependencies;
            }
        }

        public Stack<Action<Frame>> NextDelegates
        {
            get
            {
                if (this.nextDelegates == null)
                {
                    this.nextDelegates = new Stack<Action<Frame>>();
                }
                return this.nextDelegates;
            }
        }

        public void ScheduleActivity(Activity activity, Action<Frame> callback)
        {
            Frame newFrame = new Frame(this.states, this);
            newFrame.NextDelegates.Push(activity.Execute);
            this.NextDelegatesWithDependencies.Add(newFrame, callback);
            this.states.Frames.Remove(this);
            this.states.Frames.Add(newFrame);
        }
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
                    Frame frame;
                    Action<Frame> nextDelegate = states.ConsumeNextDelegate(out frame);
                    nextDelegate(frame);
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
                    Activity workflow = new Sequence
                    {
                        Activities =
                        {
                            new Parallel
                            {
                                Activities = 
                                {
                                    new Read { ContentKey = "file1Content", FileName = @"c:\file1.txt" },
                                    new Read { ContentKey = "file2Content", FileName = @"c:\file2.txt" },
                                }
                            },
                            new Write { Content1Key = "file1Content", Content2Key = "file2Content" }
                        }
                    };
                    workflow.Execute(states.Frames[0]);
                    SaveAt(states);
                }
            }
        }

        private static void SetupProgramExecution()
        {
            File.WriteAllText(@"c:\file1.txt", "Hello world to ");
            File.WriteAllText(@"c:\file2.txt", "home made workflow foundation!");
        }

        private static void CleanupProgramExecution()
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
Parallel.cs
```
namespace HomeMadeWF
{
    using System;
    using System.Collections.ObjectModel;

    [Serializable]
    public class Parallel : Activity
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

        public override void Execute(Frame frame)
        {
            foreach (Activity activity in this.Activities)
            {
                frame.ScheduleActivity(activity, null);
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

        public override void Execute(Frame frame)
        {
            frame.States.Contents[this.ContentKey] = File.ReadAllText(this.FileName);
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

        public override void Execute(Frame frame)
        {
            if (frame.States.sequenceCounter != this.Activities.Count)
            {
                frame.ScheduleActivity(this.Activities[frame.States.sequenceCounter++], this.Execute);
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
    using System.Collections.ObjectModel;

    [Serializable]
    public class States
    {
        public Dictionary<string, string> Contents = new Dictionary<string, string>();
        public Collection<Frame> Frames = new Collection<Frame>();
        public int sequenceCounter;

        public States()
        {
            this.Frames.Add(new Frame(this, null));
        }

        public Action<Frame> ConsumeNextDelegate(out Frame frame)
        {
            return (frame = FindLastFrameWithWork()).NextDelegates.Pop();
        }

        public bool HasMoreWork()
        {
            Collection<Frame> candidateToRemove = new Collection<Frame>();
            bool stop = false;
            do
            {
                candidateToRemove.Clear();
                foreach (Frame frame in this.Frames)
                {
                    if (frame.NextDelegates.Count == 0 && frame.NextDelegatesWithDependencies.Count == 0)
                    {
                        candidateToRemove.Add(frame);
                    }
                }
                if (candidateToRemove.Count == 0)
                {
                    stop = true;

                }
                else
                {
                    foreach (Frame frame in candidateToRemove)
                    {
                        if (frame.ParentFrame != null)
                        {
                            this.Frames.Add(frame.ParentFrame);
                            if (frame.ParentFrame.NextDelegatesWithDependencies.ContainsKey(frame))
                            {
                                Action<Frame> nextDelegate = frame.ParentFrame.NextDelegatesWithDependencies[frame];
                                frame.ParentFrame.NextDelegatesWithDependencies.Remove(frame);
                                if (nextDelegate != null)
                                {
                                    frame.ParentFrame.NextDelegates.Push(nextDelegate);
                                }
                            }
                        }
                        this.Frames.Remove(frame);
                    }
                }
            } while (!stop);
            return this.Frames.Count != 0;
        }

        Frame FindLastFrameWithWork()
        {
            Frame frameWithWork = null;
            foreach (Frame frame in this.Frames)
            {
                if (frame.NextDelegates.Count != 0)
                {
                    frameWithWork = frame;
                }
            }
            return frameWithWork;
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

        public override void Execute(Frame frame)
        {
            Console.WriteLine(frame.States.Contents[this.Content1Key] + frame.States.Contents[this.Content2Key]);
        }
    }
}
```
Notice the sequential execution of the work despite of the use of `Parallel` activity. `Parallel` activity essentially allows multiple outstanding works. When these work items are waiting for external signals, then these external signals are all waiting together. We used frame as dependencies for delegates, but sometimes delegates are not ready for execution not because of control dependencies, but data dependencies. These data eventually come from the host. From states, we can signal the host to wait for these data, and from the host, those dependencies could be broken when the data is ready. WF3 provided this mechanism though `WorkflowQueue`, and WF4 provided this through `Bookmarks`. Queue is sometimes overkill when the program really just request one item.

![9-to-10.png][pic8]

Activity.cs
```
namespace HomeMadeWF
{
    using System;

    [Serializable]
    public abstract class Activity
    {
        public abstract void Execute(Frame frame);
    }
}
```
Bookmark.cs
```
namespace HomeMadeWF
{
    using System;

    [Serializable]
    public class Bookmark : IDependency
    {
        Frame frame;

        public Bookmark(Frame frame, string name)
        {
            this.Name = name;
            this.frame = frame;
        }

        public Frame Frame
        {
            get { return this.frame; }
        }

        public string Name { get; private set; }

        public Object Object { get; set; }
    }
}
```
Frame.cs
```
namespace HomeMadeWF
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class Frame : IDependency
    {
        Dictionary<IDependency, Action<Frame>> nextDelegatesWithDependencies;
        Stack<Action<Frame>> nextDelegates;
        States states;
        Frame parentFrame;

        public Frame(States states, Frame parentFrame)
        {
            this.states = states;
            this.parentFrame = parentFrame;
        }

        public States States
        {
            get { return this.states; }
        }

        public Frame ParentFrame
        {
            get { return this.parentFrame; }
        }

        public Dictionary<IDependency, Action<Frame>> NextDelegatesWithDependencies
        {
            get
            {
                if (this.nextDelegatesWithDependencies == null)
                {
                    this.nextDelegatesWithDependencies = new Dictionary<IDependency, Action<Frame>>();
                }
                return this.nextDelegatesWithDependencies;
            }
        }

        public Stack<Action<Frame>> NextDelegates
        {
            get
            {
                if (this.nextDelegates == null)
                {
                    this.nextDelegates = new Stack<Action<Frame>>();
                }
                return this.nextDelegates;
            }
        }

        public void ScheduleActivity(Activity activity, Action<Frame> callback)
        {
            Frame newFrame = new Frame(this.states, this);
            newFrame.NextDelegates.Push(activity.Execute);
            this.NextDelegatesWithDependencies.Add(newFrame, callback);
            this.states.Frames.Remove(this);
            this.states.Frames.Add(newFrame);
        }

        public void WaitBookmark(string bookmarkName, Action<Frame> callback)
        {
            Bookmark bookmark = new Bookmark(this, bookmarkName);
            states.bookmarks.Add(bookmarkName, bookmark);
            if (callback != null)
            {
                this.NextDelegatesWithDependencies.Add(bookmark, callback);
            }
        }
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
    using System.Threading;

    public static class Host
    {
        const string stateFileName = @"c:\execution.dat";

        static States states;

        public static void ResumeBookmarks(object o)
        {
            string line1 = Console.ReadLine();
            if (states == null)
            {
                Console.WriteLine(line1);
            }
            states.ResumeBookmark("1", line1);
            string line2 = Console.ReadLine();
            states.ResumeBookmark("2", line2);
        }

        public static void Main()
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(ResumeBookmarks));
            while (true)
            {
                if (File.Exists(stateFileName))
                {
                    states = ContinueAt();
                    Frame frame;
                    Action<Frame> nextDelegate = states.ConsumeNextDelegate(out frame);
                    nextDelegate(frame);
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
                    states = new States();
                    Activity workflow = new Sequence
                    {
                        Activities =
                        {
                            new Parallel
                            {
                                Activities = 
                                {
                                    new Read { ContentKey = "file1Content", BookmarkName = @"1" },
                                    new Read { ContentKey = "file2Content", BookmarkName = @"2" },
                                }
                            },
                            new Write { Content1Key = "file1Content", Content2Key = "file2Content" }
                        }
                    };
                    workflow.Execute(states.Frames[0]);
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
IDependency.cs
```
namespace HomeMadeWF
{
    public interface IDependency
    {
    }
}
```
Parallel.cs
```
namespace HomeMadeWF
{
    using System;
    using System.Collections.ObjectModel;

    [Serializable]
    public class Parallel : Activity
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

        public override void Execute(Frame frame)
        {
            foreach (Activity activity in this.Activities)
            {
                frame.ScheduleActivity(activity, null);
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

    [Serializable]
    public class Read : Activity
    {
        public string BookmarkName { get; set; }

        public string ContentKey { get; set; }

        public override void Execute(Frame frame)
        {
            frame.WaitBookmark(this.BookmarkName, this.OnDataAvailable);
        }

        public void OnDataAvailable(Frame frame)
        {
            frame.States.Contents[this.ContentKey] = frame.States.bookmarks[BookmarkName].Object.ToString();
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

        public override void Execute(Frame frame)
        {
            if (frame.States.sequenceCounter != this.Activities.Count)
            {
                frame.ScheduleActivity(this.Activities[frame.States.sequenceCounter++], this.Execute);
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
    using System.Collections.ObjectModel;
    using System.Threading;

    [Serializable]
    public class States
    {
        public Dictionary<string, string> Contents = new Dictionary<string, string>();
        public Dictionary<string, Bookmark> bookmarks = new Dictionary<string, Bookmark>();
        public Collection<Frame> Frames = new Collection<Frame>();
        public int sequenceCounter;

        public States()
        {
            this.Frames.Add(new Frame(this, null));
        }

        public Action<Frame> ConsumeNextDelegate(out Frame frame)
        {
            frame = FindLastFrameWithWork();
            if (frame != null)
            {
                return frame.NextDelegates.Pop();
            }
            else
            {
                lock (bookmarks)
                {
                    Monitor.Wait(bookmarks);
                }
                return ConsumeNextDelegate(out frame);
            }
        }

        public bool HasMoreWork()
        {
            Collection<Frame> candidateToRemove = new Collection<Frame>();
            bool stop = false;
            do
            {
                candidateToRemove.Clear();
                foreach (Frame frame in this.Frames)
                {
                    if (frame.NextDelegates.Count == 0 && frame.NextDelegatesWithDependencies.Count == 0)
                    {
                        candidateToRemove.Add(frame);
                    }
                }
                if (candidateToRemove.Count == 0)
                {
                    stop = true;

                }
                else
                {
                    foreach (Frame frame in candidateToRemove)
                    {
                        if (frame.ParentFrame != null)
                        {
                            this.Frames.Add(frame.ParentFrame);
                            if (frame.ParentFrame.NextDelegatesWithDependencies.ContainsKey(frame))
                            {
                                Action<Frame> nextDelegate = frame.ParentFrame.NextDelegatesWithDependencies[frame];
                                frame.ParentFrame.NextDelegatesWithDependencies.Remove(frame);
                                if (nextDelegate != null)
                                {
                                    frame.ParentFrame.NextDelegates.Push(nextDelegate);
                                }
                            }
                        }
                        this.Frames.Remove(frame);
                    }
                }
            } while (!stop);
            return this.Frames.Count != 0;
        }

        Frame FindLastFrameWithWork()
        {
            Frame frameWithWork = null;
            foreach (Frame frame in this.Frames)
            {
                if (frame.NextDelegates.Count != 0)
                {
                    frameWithWork = frame;
                }
            }
            return frameWithWork;
        }

        public void ResumeBookmark(string bookmarkName, object data)
        {
            lock (this.bookmarks)
            {
                Bookmark bookmark = this.bookmarks[bookmarkName];
                bookmark.Object = data;
                Frame currentFrame = bookmark.Frame;
                if (currentFrame.NextDelegatesWithDependencies.ContainsKey(bookmark))
                {
                    Action<Frame> resumable = currentFrame.NextDelegatesWithDependencies[bookmark];
                    currentFrame.NextDelegatesWithDependencies.Remove(bookmark);
                    currentFrame.NextDelegates.Push(resumable);
                }
                Monitor.Pulse(this.bookmarks);
            }
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

        public override void Execute(Frame frame)
        {
            Console.WriteLine(frame.States.Contents[this.Content1Key] + frame.States.Contents[this.Content2Key]);
        }
    }
}
```

This will be the end of our home made WF series, certainly we still have a long way to go until we get what will have today for workflow foundation. But I guess up to now you should able to appreciate what WF brings to you and how to best leverage WF for your application.

[pic6]: 7-to-8.png
[pic7]: 8-to-9.png
[pic8]: 9-to-10.png
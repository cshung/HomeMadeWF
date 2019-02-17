namespace HomeMadeWF
{
    using System;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Threading;

    public static class Host
    {
        const string stateFileName = @"c:\temp\execution.dat";

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
            File.WriteAllText(@"c:\temp\file1.txt", "Hello world to ");
            File.WriteAllText(@"c:\temp\file2.txt", "home made workflow foundation!");
        }

        static void CleanupProgramExecution()
        {
            File.Delete(@"c:\temp\file1.txt");
            File.Delete(@"c:\temp\file2.txt");
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

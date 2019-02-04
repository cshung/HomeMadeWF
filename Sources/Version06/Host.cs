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

﻿namespace HomeMadeWF
{
    using System;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;

    public static class Host
    {
        const string stateFileName = @"c:\temp\execution.dat";

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
                                    new Read { ContentKey = "file1Content", FileName = @"c:\temp\file1.txt" },
                                    new Read { ContentKey = "file2Content", FileName = @"c:\temp\file2.txt" },
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
            File.WriteAllText(@"c:\temp\file1.txt", "Hello world to ");
            File.WriteAllText(@"c:\temp\file2.txt", "home made workflow foundation!");
        }

        private static void CleanupProgramExecution()
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

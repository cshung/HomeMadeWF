namespace HomeMadeWF
{
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
                            new Read { ContentKey = "file1Content", FileName = @"c:\temp\file1.txt" },
                            new Read { ContentKey = "file2Content", FileName = @"c:\temp\file2.txt" },
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
            File.WriteAllText(@"c:\temp\file1.txt", "Hello world to ");
            File.WriteAllText(@"c:\temp\file2.txt", "homemade workflow foundation!");
        }

        static void CleanupProgramExecution()
        {
            File.Delete(@"c:\temp\file1.txt");
            File.Delete(@"c:\temp\file2.txt");
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

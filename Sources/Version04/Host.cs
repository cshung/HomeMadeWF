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

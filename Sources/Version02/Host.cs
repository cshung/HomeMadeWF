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

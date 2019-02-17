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
        const string stateFileName = @"c:\temp\execution.dat";
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
            file1Content = File.ReadAllText(@"c:\temp\file1.txt");
            SaveAt(this.RunStep2);
        }

        public void RunStep2()
        {
            file2Content = File.ReadAllText(@"c:\temp\file2.txt");
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
            File.WriteAllText(@"c:\temp\file1.txt", "Hello world to ");
            File.WriteAllText(@"c:\temp\file2.txt", "homemade workflow foundation!");
        }

        static void CleanupProgramExecution()
        {
            File.Delete(@"c:\temp\file1.txt");
            File.Delete(@"c:\temp\file2.txt");
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

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

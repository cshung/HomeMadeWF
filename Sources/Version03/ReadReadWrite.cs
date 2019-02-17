namespace HomeMadeWF
{
    using System;
    using System.IO;

    [Serializable]
    public class ReadReadWrite : Activity
    {
        string file1Content;
        string file2Content;

        public override void Execute()
        {
            file1Content = File.ReadAllText(@"c:\temp\file1.txt");
            this.NextDelegate = new Action(this.RunStep2);
        }

        public void RunStep2()
        {
            file2Content = File.ReadAllText(@"c:\temp\file2.txt");
            this.NextDelegate = new Action(this.RunStep3);
        }

        public void RunStep3()
        {
            Console.WriteLine(file1Content + file2Content);
            this.NextDelegate = null;
        }
    }
}

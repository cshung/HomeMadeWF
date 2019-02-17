namespace HomeMadeWF
{
    using System;
    using System.IO;

    [Serializable]
    public class Read1 : Activity
    {
        public override void Execute()
        {
            base.states.file1Content = File.ReadAllText(@"c:\temp\file1.txt");
        }
    }
}

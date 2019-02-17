namespace HomeMadeWF
{
    using System;
    using System.IO;

    [Serializable]
    public class Read2 : Activity
    {
        public override void Execute()
        {
            base.states.file2Content = File.ReadAllText(@"c:\temp\file2.txt");
        }
    }
}

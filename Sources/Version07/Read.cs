namespace HomeMadeWF
{
    using System;
    using System.IO;

    [Serializable]
    public class Read : Activity
    {
        public string FileName { get; set; }

        public string ContentKey { get; set; }

        public override void Execute(States states)
        {
            states.Contents[this.ContentKey] = File.ReadAllText(this.FileName);
        }
    }
}

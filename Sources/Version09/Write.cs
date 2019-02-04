namespace HomeMadeWF
{
    using System;

    [Serializable]
    public class Write : Activity
    {
        public string Content1Key { get; set; }

        public string Content2Key { get; set; }

        public override void Execute(Frame frame)
        {
            Console.WriteLine(frame.States.Contents[this.Content1Key] + frame.States.Contents[this.Content2Key]);
        }
    }
}

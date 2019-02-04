namespace HomeMadeWF
{
    using System;

    [Serializable]
    public class Write : Activity
    {
        public string Content1Key { get; set; }

        public string Content2Key { get; set; }

        public override void Execute()
        {
            Console.WriteLine(states.Contents[this.Content1Key] + states.Contents[this.Content2Key]);
        }
    }
}

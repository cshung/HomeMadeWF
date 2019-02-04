namespace HomeMadeWF
{
    using System;

    [Serializable]
    public class Write : Activity
    {
        public override void Execute()
        {
            Console.WriteLine(base.states.file1Content + base.states.file2Content);
        }
    }
}

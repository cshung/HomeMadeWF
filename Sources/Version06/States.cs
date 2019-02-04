namespace HomeMadeWF
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class States
    {
        public Dictionary<string, string> Contents = new Dictionary<string, string>();
        public Stack<Frame> Frames = new Stack<Frame>();
        public int sequenceCounter;
    }
}

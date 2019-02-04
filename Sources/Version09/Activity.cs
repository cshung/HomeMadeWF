namespace HomeMadeWF
{
    using System;

    [Serializable]
    public abstract class Activity
    {
        public abstract void Execute(Frame frame);
    }
}

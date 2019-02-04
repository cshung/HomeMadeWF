namespace HomeMadeWF
{
    using System;

    [Serializable]
    public abstract class Activity
    {
        public Action NextDelegate { get; set; }

        public abstract void Execute();
    }
}
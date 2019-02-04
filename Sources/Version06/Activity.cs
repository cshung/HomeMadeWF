namespace HomeMadeWF
{
    using System;

    [Serializable]
    public abstract class Activity
    {
        public abstract void Execute(States states);
    }
}

namespace HomeMadeWF
{
    using System;

    [Serializable]
    public abstract class Activity
    {
        public static States globalState = new States();
        protected States states;

        public Activity()
        {
            this.states = globalState;
        }

        public Action NextDelegate { get; set; }

        public abstract void Execute();
    }
}

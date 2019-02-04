namespace HomeMadeWF
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class States
    {
        public Dictionary<string, string> Contents = new Dictionary<string, string>();
        Stack<Frame> Frames = new Stack<Frame>();
        public int sequenceCounter;

        public States()
        {
            this.Frames.Push(new Frame());
        }

        public Action<States> ConsumeNextDelegate()
        {
            Frame currentFrame = this.Frames.Peek();
            Action<States> nextDelegate = currentFrame.NextDelegate;
            currentFrame.NextDelegate = null;
            return nextDelegate;
        }

        public bool HasMoreWork()
        {
            while (this.Frames.Count > 0 && this.Frames.Peek().NextDelegate == null)
            {
                this.Frames.Pop();
            }
            return this.Frames.Count != 0;
        }

        public void ScheduleActivity(Activity activity, Action<States> callback)
        {
            this.Frames.Peek().NextDelegate = callback;
            this.Frames.Push(new Frame() { NextDelegate = activity.Execute });
        }
    }
}
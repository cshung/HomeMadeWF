namespace HomeMadeWF
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class Frame
    {
        Dictionary<Frame, Action<Frame>> nextDelegatesWithDependencies;
        Stack<Action<Frame>> nextDelegates;
        States states;
        Frame parentFrame;

        public Frame(States states, Frame parentFrame)
        {
            this.states = states;
            this.parentFrame = parentFrame;
        }

        public States States
        {
            get { return this.states; }
        }

        public Frame ParentFrame
        {
            get { return this.parentFrame; }
        }

        public Dictionary<Frame, Action<Frame>> NextDelegatesWithDependencies
        {
            get
            {
                if (this.nextDelegatesWithDependencies == null)
                {
                    this.nextDelegatesWithDependencies = new Dictionary<Frame, Action<Frame>>();
                }
                return this.nextDelegatesWithDependencies;
            }
        }

        public Stack<Action<Frame>> NextDelegates
        {
            get
            {
                if (this.nextDelegates == null)
                {
                    this.nextDelegates = new Stack<Action<Frame>>();
                }
                return this.nextDelegates;
            }
        }

        public void ScheduleActivity(Activity activity, Action<Frame> callback)
        {
            Frame newFrame = new Frame(this.states, this);
            newFrame.NextDelegates.Push(activity.Execute);
            this.NextDelegatesWithDependencies.Add(newFrame, callback);
            this.states.Frames.Remove(this);
            this.states.Frames.Add(newFrame);
        }
    }
}

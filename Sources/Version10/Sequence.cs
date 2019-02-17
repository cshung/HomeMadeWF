namespace HomeMadeWF
{
    using System;
    using System.Collections.ObjectModel;

    [Serializable]
    public class Sequence : Activity
    {
        Collection<Activity> activities;

        public Collection<Activity> Activities
        {
            get
            {
                if (this.activities == null)
                {
                    activities = new Collection<Activity>();
                }
                return this.activities;
            }
        }

        public override void Execute(Frame frame)
        {
            object sequenceCounterObject;
            if (!frame.States.Contents.TryGetValue("sequenceCounter", out sequenceCounterObject))
            {
                sequenceCounterObject = 0;
                frame.States.Contents.Add("sequenceCounter", sequenceCounterObject);
            }
            int sequenceCounter = (int)sequenceCounterObject;
            if (sequenceCounter != this.Activities.Count)
            {
                frame.ScheduleActivity(this.Activities[sequenceCounter++], this.Execute);
                frame.States.Contents["sequenceCounter"] = sequenceCounter;
            }
        }
    }
}

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
            if (frame.States.sequenceCounter != this.Activities.Count)
            {
                frame.ScheduleActivity(this.Activities[frame.States.sequenceCounter++], this.Execute);
            }
        }
    }
}

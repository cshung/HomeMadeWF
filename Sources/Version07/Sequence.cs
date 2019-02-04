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

        public override void Execute(States states)
        {
            if (states.sequenceCounter != this.Activities.Count)
            {
                states.ScheduleActivity(this.Activities[states.sequenceCounter++], this.Execute);
            }
        }
    }
}

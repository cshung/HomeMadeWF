namespace HomeMadeWF
{
    using System;
    using System.Collections.ObjectModel;

    [Serializable]
    public class Parallel : Activity
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
            foreach (Activity activity in this.Activities)
            {
                frame.ScheduleActivity(activity, null);
            }
        }
    }
}

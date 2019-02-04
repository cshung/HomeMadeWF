namespace HomeMadeWF
{
    using System;
    using System.Collections.ObjectModel;

    [Serializable]
    public class Sequence : Activity
    {
        Collection<Activity> activities;
        int sequenceCounter;
        bool executingChildActivity;

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

        public override void Execute()
        {
            Activity currentActivity = activities[sequenceCounter];
            this.NextDelegate = new Action(this.Execute);
            if (currentActivity.NextDelegate != null)
            {
                activities[sequenceCounter].NextDelegate();
            }
            else if (!executingChildActivity)
            {
                executingChildActivity = true;
                currentActivity.Execute();
            }
            else
            {
                sequenceCounter++;
                executingChildActivity = false;
                if (sequenceCounter == this.Activities.Count)
                {
                    this.NextDelegate = null;
                }
            }
        }
    }
}

using System.Collections.Generic;
using ResearchQueue.Research;
using Verse;

namespace ResearchQueue.State
{
    // One research system's saved queue. Entries are defNames so projects from a removed mod keep their place.
    public sealed class ResearchSystemQueueState : IExposable
    {
        private string systemId = string.Empty;
        private bool initialized;
        private List<string> projectIds = new List<string>();
        private List<string> snoozedProjectIds = new List<string>();
        private List<string> newProjectIds = new List<string>();
        private List<float> rates = new List<float>();

        public string SystemId => systemId;
        public bool Initialized { get => initialized; set => initialized = value; }
        public List<string> Queue => projectIds;
        public List<string> Snoozed => snoozedProjectIds;
        public List<string> NewIds => newProjectIds;
        // Apparent research points per day, sampled hourly.
        public List<float> Rates => rates;

        public ResearchSystem? System { get; set; }
        public float SampledProgress { get; set; }

        public ResearchSystemQueueState()
        {
        }

        public ResearchSystemQueueState(string systemId)
        {
            this.systemId = systemId;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref systemId, "systemId", string.Empty);
            Scribe_Values.Look(ref initialized, "initialized", false);
            Scribe_Collections.Look(ref projectIds, "projectIds", LookMode.Value);
            Scribe_Collections.Look(ref snoozedProjectIds, "snoozedProjectIds", LookMode.Value);
            Scribe_Collections.Look(ref newProjectIds, "newProjectIds", LookMode.Value);
            Scribe_Collections.Look(ref rates, "rates", LookMode.Value);
            projectIds ??= new List<string>();
            snoozedProjectIds ??= new List<string>();
            newProjectIds ??= new List<string>();
            rates ??= new List<float>();
        }
    }
}

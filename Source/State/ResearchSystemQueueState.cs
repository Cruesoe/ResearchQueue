using System.Collections.Generic;
using Verse;

namespace ResearchQueue.State
{
    public sealed class ResearchSystemQueueState : IExposable
    {
        private string systemId = string.Empty;
        private bool initialized;
        private List<string> projectIds = new List<string>();
        private List<string> snoozedProjectIds = new List<string>();
        private List<string> newProjectIds = new List<string>();

        public string SystemId => systemId;
        public bool Initialized { get => initialized; set => initialized = value; }
        public List<string> ProjectIds => projectIds;
        public List<string> SnoozedProjectIds => snoozedProjectIds;
        public List<string> NewProjectIds => newProjectIds;

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
            // Retain defNames so missing definitions can be handled by an explicit recovery policy.
            Scribe_Collections.Look(ref projectIds, "projectIds", LookMode.Value);
            Scribe_Collections.Look(ref snoozedProjectIds, "snoozedProjectIds", LookMode.Value);
            Scribe_Collections.Look(ref newProjectIds, "newProjectIds", LookMode.Value);
            projectIds ??= new List<string>();
            snoozedProjectIds ??= new List<string>();
            newProjectIds ??= new List<string>();
        }
    }
}

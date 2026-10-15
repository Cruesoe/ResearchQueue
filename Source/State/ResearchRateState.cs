using System.Collections.Generic;
using Verse;

namespace ResearchQueue.State
{
    public sealed class ResearchRateState : IExposable
    {
        public string Id = string.Empty;
        public float LastRealProgress = -1f;
        public List<float> Rates = new List<float>();

        public ResearchRateState() { }
        public ResearchRateState(string id) { Id = id; }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id", string.Empty);
            Scribe_Values.Look(ref LastRealProgress, "lastRealProgress", -1f);
            Scribe_Collections.Look(ref Rates, "rates", LookMode.Value);
            Rates ??= new List<float>();
        }
    }
}

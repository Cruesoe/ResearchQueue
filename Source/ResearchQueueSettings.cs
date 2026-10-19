using Verse;

namespace ResearchQueue
{
    public sealed class ResearchQueueSettings : ModSettings
    {
        public bool ConfirmDependentSnooze = true;
        public bool ShowCompletionDate;
        public bool ShowCompletionPopup;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ConfirmDependentSnooze, "confirmDependentSnooze", true);
            Scribe_Values.Look(ref ShowCompletionDate, "showCompletionDate", false);
            Scribe_Values.Look(ref ShowCompletionPopup, "showCompletionPopup", false);
        }
    }
}

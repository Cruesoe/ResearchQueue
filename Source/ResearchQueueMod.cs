using UnityEngine;
using HarmonyLib;
using Verse;

namespace ResearchQueue
{
    public sealed class ResearchQueueMod : Mod
    {
        public ResearchQueueSettings Settings { get; }
        public static ResearchQueueMod? Instance { get; private set; }

        public ResearchQueueMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ResearchQueueSettings>();
            Instance = this;
            new Harmony("cruesoe.researchqueue").PatchAll();
        }

        public override string SettingsCategory()
        {
            return "ResearchQueue_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(
                "ResearchQueue_ConfirmDependentSnooze".Translate(),
                ref Settings.ConfirmDependentSnooze,
                "ResearchQueue_ConfirmDependentSnoozeTip".Translate());
            listing.End();
        }
    }
}

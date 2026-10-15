using HarmonyLib;
using UnityEngine;
using Verse;

namespace ResearchQueue
{
    public sealed class ResearchQueueMod : Mod
    {
        public static ResearchQueueSettings Settings { get; private set; } = null!;
        public static ResearchQueueMod Instance { get; private set; } = null!;

        public ResearchQueueMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<ResearchQueueSettings>();
            new Harmony("cruesoe.researchqueue").PatchAll();
        }

        public override string SettingsCategory() => "ResearchQueue_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("ResearchQueue_ConfirmDependentSnooze".Translate(), ref Settings.ConfirmDependentSnooze,
                "ResearchQueue_ConfirmDependentSnoozeTip".Translate());
            listing.CheckboxLabeled("ResearchQueue_ShowCompletionDate".Translate(), ref Settings.ShowCompletionDate,
                "ResearchQueue_ShowCompletionDateTip".Translate());
            listing.End();
        }
    }
}

using HarmonyLib;
using ResearchQueue.Research;
using ResearchQueue.State;
using RimWorld;
using Verse;

namespace ResearchQueue.Patches
{
    // Refreshes on the next tick so the freed slot is refilled and new unlocks are queued.
    // With Node Research, its "auto open menu on finish" is off for the call (restored in the finalizer), so the tab stays shut.
    // Unless "Show a pop-up when research finishes" is on, the game's completion pop-up becomes a message, since the queue starts the next project itself.
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
    public static class Patch_ResearchManager_FinishProject
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        public static void Prefix(ResearchProjectDef proj, ref bool doCompletionDialog, out bool? __state)
        {
            __state = NodeResearch.SuppressAutoOpen();
            if (!doCompletionDialog || ResearchQueueMod.Settings.ShowCompletionPopup || proj == null) return;
            doCompletionDialog = false;
            Messages.Message("ResearchFinished".Translate(proj.LabelCap), MessageTypeDefOf.PositiveEvent);
        }

        [HarmonyPostfix]
        public static void Postfix() => ResearchQueueWorldComponent.Get()?.RequestRefresh();

        [HarmonyFinalizer]
        public static void Finalizer(bool? __state) => NodeResearch.RestoreAutoOpen(__state);
    }
}

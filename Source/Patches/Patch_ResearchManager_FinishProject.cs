using HarmonyLib;
using ResearchQueue.Research;
using ResearchQueue.State;
using RimWorld;

namespace ResearchQueue.Patches
{
    // Refreshes on the next tick so the freed slot is refilled and new unlocks are queued.
    // With Node Research, its "auto open menu on finish" is off for the call (restored in the finalizer), so the tab stays shut.
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
    public static class Patch_ResearchManager_FinishProject
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        public static void Prefix(out bool? __state) => __state = NodeResearch.SuppressAutoOpen();

        [HarmonyPostfix]
        public static void Postfix() => ResearchQueueWorldComponent.Get()?.RequestRefresh();

        [HarmonyFinalizer]
        public static void Finalizer(bool? __state) => NodeResearch.RestoreAutoOpen(__state);
    }
}

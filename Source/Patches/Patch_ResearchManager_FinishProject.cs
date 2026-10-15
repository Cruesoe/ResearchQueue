using HarmonyLib;
using ResearchQueue.State;
using RimWorld;

namespace ResearchQueue.Patches
{
    // Refreshes on the next tick so the freed slot is refilled and new unlocks are queued.
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
    public static class Patch_ResearchManager_FinishProject
    {
        [HarmonyPostfix]
        public static void Postfix() => ResearchQueueWorldComponent.Get()?.RequestRefresh();
    }
}

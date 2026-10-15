using HarmonyLib;
using ResearchQueue.State;
using RimWorld;
using Verse;

namespace ResearchQueue.Patches
{
    // During play only the queue picks research; other callers are refused with a message.
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.SetCurrentProject), new[] { typeof(ResearchProjectDef) })]
    public static class Patch_ResearchManager_SetCurrentProject
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        public static bool Prefix(ResearchProjectDef proj)
        {
            if (proj == null || ResearchSelection.Applying || Current.ProgramState != ProgramState.Playing) return true;
            Messages.Message("ResearchQueue_SelectionBlocked".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }
    }
}

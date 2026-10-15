using HarmonyLib;
using ResearchQueue.State;
using ResearchQueue.UI;
using RimWorld;
using Verse;

namespace ResearchQueue
{
    public static class ResearchQueueRuntime
    {
        public static bool ApplyingProject { get; private set; }
        public static bool ConflictingSelector => ModsConfig.IsActive("cruesoe.research.auto") ||
            ModsConfig.IsActive("cruesoe.semirandompfork.continued") || ModsConfig.IsActive("cruesoe.researchdashboard");
        public static bool Enabled => ResearchQueueMod.Instance != null && !ConflictingSelector;

        public static void Start(ResearchProjectDef project)
        {
            ApplyingProject = true;
            try { Find.ResearchManager.SetCurrentProject(project); }
            finally { ApplyingProject = false; }
        }

        public static void ApplyWindow()
        {
            if (!Enabled || MainButtonDefOf.Research == null) return;
            MainButtonDef button = MainButtonDefOf.Research;
            if (button.tabWindowClass == typeof(MainTabWindow_ResearchQueue)) return;
            button.tabWindowClass = typeof(MainTabWindow_ResearchQueue);
            button.ClearCachedData();
        }
    }

    [StaticConstructorOnStartup]
    public static class ResearchQueueStartup
    {
        static ResearchQueueStartup()
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                ResearchQueueRuntime.ApplyWindow();
                if (ResearchQueueRuntime.ConflictingSelector)
                    Log.Warning("[Research Queue] Another automatic research selector is active. Disable Research Auto, Semi-Random Research, or Research Dashboard before using Research Queue.");
            });
        }
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.SetCurrentProject), new[] { typeof(ResearchProjectDef) })]
    public static class Patch_ResearchManager_SetCurrentProject
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        public static bool Prefix(ResearchProjectDef proj) => proj == null || !ResearchQueueRuntime.Enabled ||
            Current.ProgramState != ProgramState.Playing || ResearchQueueRuntime.ApplyingProject;
    }

    [HarmonyPatch(typeof(MainTabWindow_Research), "AttemptBeginResearch")]
    public static class Patch_MainTabWindow_Research_AttemptBeginResearch
    {
        public static bool Prefix() => !ResearchQueueRuntime.Enabled || ResearchQueueRuntime.ApplyingProject;
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
    public static class Patch_ResearchManager_FinishProject
    {
        public static void Postfix() => ResearchQueueWorldComponent.Get()?.RequestRefresh();
    }
}

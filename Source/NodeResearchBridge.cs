using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ResearchQueue
{
    public static class NodeResearchBridge
    {
        private static bool resolved;
        private static MethodInfo? collapse;
        private static FieldInfo? queueField;
        private static Dictionary<ResearchProjectDef, ResearchTabDef>? originalTabs;

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            if (!ModsConfig.IsActive("ferny.noderesearch")) return;
            System.Type? startup = AccessTools.TypeByName("BetterResearchMenu.Startup");
            System.Type? state = AccessTools.TypeByName("BetterResearchMenu.State");
            if (startup != null)
            {
                collapse = AccessTools.Method(startup, "Collapse");
                originalTabs = AccessTools.Field(startup, "originalTabs")?.GetValue(null) as Dictionary<ResearchProjectDef, ResearchTabDef>;
            }
            if (state != null) queueField = AccessTools.Field(state, "researchQueue");
        }

        public static void Prepare()
        {
            Resolve();
            // Node's advancement check counts projects on Main, as established by its Collapse method.
            collapse?.Invoke(null, null);
        }

        public static IEnumerable<string> LegacyQueue()
        {
            Resolve();
            return queueField?.GetValue(null) as List<string> ?? new List<string>();
        }

        public static ResearchTabDef? OriginalTab(ResearchProjectDef project)
        {
            Resolve();
            return originalTabs != null && originalTabs.TryGetValue(project, out ResearchTabDef? tab) ? tab : project.tab;
        }

        public static string Tag(ResearchProjectDef project)
        {
            if (project.modExtensions == null) return string.Empty;
            foreach (DefModExtension extension in project.modExtensions)
            {
                if (extension.GetType().Name == "ResearchFoundationExtension") return "ResearchQueue_Foundation".Translate();
                if (extension.GetType().Name == "EmergenceExtension") return "ResearchQueue_Emergence".Translate();
            }
            return string.Empty;
        }
    }
}

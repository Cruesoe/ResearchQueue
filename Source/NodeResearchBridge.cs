using System;
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
        private static FieldInfo? settingsField;
        private static FieldInfo? restrictEra;
        private static FieldInfo? emergenceEnabled;
        private static FieldInfo? advancementMode;
        private static MethodInfo? advancementProgress;

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
            Type? mod = AccessTools.TypeByName("BetterResearchMenu.BetterResearchMenuMod");
            settingsField = mod == null ? null : AccessTools.Field(mod, "settings");
            Type? settings = settingsField?.FieldType;
            if (settings != null)
            {
                restrictEra = AccessTools.Field(settings, "restrictResearchToTechLevel");
                emergenceEnabled = AccessTools.Field(settings, "enableEmergence");
                advancementMode = AccessTools.Field(settings, "advancementTiedTo");
            }
            Type? window = AccessTools.TypeByName("BetterResearchMenu.MainTabWindow_BetterResearch");
            if (window != null) advancementProgress = AccessTools.Method(window, "GetAdvancementProgressRaw");
        }

        public static void Prepare()
        {
            Resolve();
            // Node's advancement check counts projects on Main, as established by its Collapse method.
            collapse?.Invoke(null, null);
        }

        public static IReadOnlyList<string> TakeLegacyQueue()
        {
            Resolve();
            List<string>? legacy = queueField?.GetValue(null) as List<string>;
            if (legacy == null || legacy.Count == 0) return Array.Empty<string>();
            string[] snapshot = legacy.ToArray();
            legacy.Clear();
            return snapshot;
        }

        public static string? BlockReason(ResearchProjectDef project)
        {
            Resolve();
            object? settings = settingsField?.GetValue(null);
            if (settings == null) return null;
            if (restrictEra?.GetValue(settings) is true && !ResearchSystems.IsAnomaly(project) &&
                ResearchSystems.Key(project) != ResearchSystems.Gravship && project.techLevel > Faction.OfPlayer.def.techLevel)
                return "ResearchQueue_EraRequired".Translate(project.techLevel.ToStringHuman());
            if (project.modExtensions?.Exists(e => e.GetType().Name == "EmergenceExtension") != true) return null;
            if (emergenceEnabled?.GetValue(settings) is false) return "ResearchQueue_EmergenceDisabled".Translate();
            if (advancementProgress == null) return null;
            object[] args = { project.techLevel, ResearchTabDefOf.Main, 0, 0 };
            float progress = (float)advancementProgress.Invoke(null, args)!;
            if (progress >= 1f) return null;
            string key = advancementMode?.GetValue(settings)?.ToString() == "Foundations"
                ? "ResearchQueue_FoundationsRequired" : "ResearchQueue_EraCompletionRequired";
            return key.Translate((int)args[2], (int)args[3]);
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

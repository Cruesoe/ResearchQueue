using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ResearchQueue.Research
{
    // Optional Node Research support, read by reflection; inert without ferny.noderesearch.
    public static class NodeResearch
    {
        public static readonly bool Active = ModsConfig.IsActive("ferny.noderesearch");

        private static bool resolved;
        private static FieldInfo? queueField;
        private static FieldInfo? settingsField;
        private static FieldInfo? restrictEra;
        private static FieldInfo? emergenceEnabled;
        private static FieldInfo? advancementMode;
        private static MethodInfo? advancementProgress;

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            if (!Active) return;
            queueField = AccessTools.Field(AccessTools.TypeByName("BetterResearchMenu.State"), "researchQueue");
            settingsField = AccessTools.Field(AccessTools.TypeByName("BetterResearchMenu.BetterResearchMenuMod"), "settings");
            Type? settings = settingsField?.FieldType;
            if (settings != null)
            {
                restrictEra = AccessTools.Field(settings, "restrictResearchToTechLevel");
                emergenceEnabled = AccessTools.Field(settings, "enableEmergence");
                advancementMode = AccessTools.Field(settings, "advancementTiedTo");
            }
            advancementProgress = AccessTools.Method(AccessTools.TypeByName("BetterResearchMenu.MainTabWindow_BetterResearch"), "GetAdvancementProgressRaw");
        }

        // Empties Node's own queue and returns what it held, so Node can't start projects from it on completion.
        public static List<string>? TakeQueue()
        {
            if (!Active) return null;
            Resolve();
            if (!(queueField?.GetValue(null) is List<string> queue) || queue.Count == 0) return null;
            List<string> taken = new List<string>(queue);
            queue.Clear();
            return taken;
        }

        public static string Tag(ResearchProjectDef project)
        {
            if (!Active) return string.Empty;
            if (ResearchCatalog.HasExtension(project, "ResearchFoundationExtension")) return "ResearchQueue_Foundation".Translate();
            if (ResearchCatalog.HasExtension(project, "EmergenceExtension")) return "ResearchQueue_Emergence".Translate();
            return string.Empty;
        }

        // Explains Node's era, emergence and advancement gates, which it applies through its CanStartNow patch.
        public static string? BlockReason(ResearchProjectDef project)
        {
            if (!Active) return null;
            Resolve();
            object? settings = settingsField?.GetValue(null);
            if (settings == null) return null;
            ResearchSystem? system = ResearchCatalog.SystemOf(project);
            if (restrictEra?.GetValue(settings) is true && system == ResearchCatalog.Standard && project.techLevel > Faction.OfPlayer.def.techLevel)
                return "ResearchQueue_EraRequired".Translate(project.techLevel.ToStringHuman());
            if (!ResearchCatalog.HasExtension(project, "EmergenceExtension")) return null;
            if (emergenceEnabled?.GetValue(settings) is false) return "ResearchQueue_EmergenceDisabled".Translate();
            if (advancementProgress == null) return null;
            object[] args = { project.techLevel, ResearchTabDefOf.Main, 0, 0 };
            if ((float)advancementProgress.Invoke(null, args)! >= 1f) return null;
            string key = advancementMode?.GetValue(settings)?.ToString() == "Foundations"
                ? "ResearchQueue_FoundationsRequired" : "ResearchQueue_EraCompletionRequired";
            return key.Translate((int)args[2], (int)args[3]);
        }
    }
}

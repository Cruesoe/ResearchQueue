using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ResearchQueue
{
    public static class ResearchSystems
    {
        public const string Standard = "Standard";
        public const string Gravship = "Gravship";

        public static string Key(ResearchProjectDef project)
        {
            string? tab = NodeResearchBridge.OriginalTab(project)?.defName;
            if (tab == "VGE_Gravtech" || tab == "VGE_GravShip") return Gravship;
            return project.knowledgeCategory?.defName ?? Standard;
        }

        public static string Label(string key)
        {
            if (key == Standard) return "ResearchQueue_Standard".Translate();
            if (key == Gravship) return "ResearchQueue_Gravtech".Translate();
            KnowledgeCategoryDef? category = DefDatabase<KnowledgeCategoryDef>.GetNamedSilentFail(key);
            if (category != null && ModsConfig.AnomalyActive && (category == KnowledgeCategoryDefOf.Basic || category == KnowledgeCategoryDefOf.Advanced))
                return "ResearchQueue_AnomalyCategory".Translate(category.LabelCap);
            return category?.LabelCap.ToString() ?? key;
        }

        public static IEnumerable<string> AllKeys() => new[] { Standard }.Concat(
            DefDatabase<ResearchProjectDef>.AllDefsListForReading.Select(Key)).Distinct();

        public static bool IsAnomaly(ResearchProjectDef project) => ModsConfig.AnomalyActive &&
            (project.knowledgeCategory == KnowledgeCategoryDefOf.Basic || project.knowledgeCategory == KnowledgeCategoryDefOf.Advanced);

        public static bool CanStart(ResearchProjectDef project) => !project.IsFinished && project.Cost > 0f &&
            (!IsAnomaly(project) || (Find.Anomaly != null && Find.Anomaly.AnomalyStudyEnabled)) &&
            project.CanStartNow;

        public static IEnumerable<ResearchProjectDef> Prerequisites(ResearchProjectDef project) =>
            (project.prerequisites ?? Enumerable.Empty<ResearchProjectDef>())
            .Concat(project.hiddenPrerequisites ?? Enumerable.Empty<ResearchProjectDef>());

        public static string BlockReason(ResearchProjectDef project)
        {
            if (project.IsFinished) return "ResearchQueue_Completed".Translate();
            List<string> reasons = new List<string>();
            string? nodeReason = NodeResearchBridge.BlockReason(project);
            if (nodeReason != null) reasons.Add(nodeReason);
            List<ResearchProjectDef> missing = Prerequisites(project).Where(p => !p.IsFinished).ToList();
            if (missing.Count > 0) reasons.Add("ResearchQueue_Requires".Translate(string.Join(", ", missing.Select(p => p.LabelCap.ToString()))));
            if (!project.TechprintRequirementMet) reasons.Add("InsufficientTechprintsApplied".Translate(project.TechprintsApplied, project.TechprintCount));
            if (project.requiredResearchBuilding != null && !project.PlayerHasAnyAppropriateResearchBench)
            {
                reasons.Add("ResearchQueue_Requires".Translate(project.requiredResearchBuilding.LabelCap));
                if (project.requiredResearchFacilities != null && project.requiredResearchFacilities.Count > 0)
                    reasons.Add("ResearchQueue_FacilitiesRequired".Translate(string.Join(", ", project.requiredResearchFacilities.Select(f => f.LabelCap.ToString()))));
            }
            if (!project.PlayerMechanitorRequirementMet) reasons.Add("MissingRequiredMechanitor".Translate());
            if (!project.InspectionRequirementsMet) reasons.Add("MissingGravEngineInspection".Translate());
            if (!project.AnalyzedThingsRequirementsMet)
                reasons.Add("ResearchQueue_AnalysisRequired".Translate(project.AnalyzedThingsCompleted, project.RequiredAnalyzedThingCount,
                    string.Join(", ", project.requiredAnalyzed.Select(t => t.LabelCap.ToString()))));
            if (project.IsHidden) reasons.Add("ResearchQueue_DiscoveryRequired".Translate());
            if (IsAnomaly(project) && (Find.Anomaly == null || !Find.Anomaly.AnomalyStudyEnabled))
                reasons.Add("ResearchQueue_StudyRequired".Translate());
            return reasons.Count > 0 ? string.Join("\n", reasons) : "ResearchQueue_OtherRequirements".Translate().ToString();
        }
    }
}

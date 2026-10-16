using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ResearchQueue.Research
{
    public static class ResearchRequirements
    {
        // CanStartNow carries the game's gates and those added by mods (Node Research patches it).
        public static bool CanStart(ResearchProjectDef project) =>
            !project.IsFinished && project.CanStartNow && Find.Storyteller.difficulty.AllowedBy(project.hideWhen) &&
            (project.knowledgeCategory == null || (Find.Anomaly != null && Find.Anomaly.AnomalyStudyEnabled));

        public static string BlockReason(ResearchProjectDef project)
        {
            if (project.IsFinished) return "ResearchQueue_Completed".Translate();
            List<string> reasons = new List<string>();
            string? node = NodeResearch.BlockReason(project);
            if (node != null) reasons.Add(node);
            List<ResearchProjectDef> missing = ResearchCatalog.Prerequisites(project).Where(p => !p.IsFinished).ToList();
            if (missing.Count > 0)
                reasons.Add("ResearchQueue_Requires".Translate(string.Join(", ", missing.Select(ResearchCatalog.ProjectLabel))));
            if (!project.TechprintRequirementMet)
                reasons.Add("InsufficientTechprintsApplied".Translate(project.TechprintsApplied, project.TechprintCount));
            if (project.requiredResearchBuilding != null && !project.PlayerHasAnyAppropriateResearchBench)
            {
                reasons.Add("ResearchQueue_Requires".Translate(project.requiredResearchBuilding.LabelCap));
                if (!project.requiredResearchFacilities.NullOrEmpty())
                    reasons.Add("ResearchQueue_FacilitiesRequired".Translate(string.Join(", ", project.requiredResearchFacilities.Select(f => f.LabelCap.ToString()))));
            }
            if (!project.PlayerMechanitorRequirementMet) reasons.Add("MissingRequiredMechanitor".Translate());
            if (!project.InspectionRequirementsMet) reasons.Add("MissingGravEngineInspection".Translate());
            if (!project.AnalyzedThingsRequirementsMet)
                reasons.Add("ResearchQueue_AnalysisRequired".Translate(project.AnalyzedThingsCompleted, project.RequiredAnalyzedThingCount,
                    string.Join(", ", project.requiredAnalyzed.Select(t => t.LabelCap.ToString()))));
            if (project.IsHidden) reasons.Add("ResearchQueue_DiscoveryRequired".Translate());
            if (project.knowledgeCategory != null && (Find.Anomaly == null || !Find.Anomaly.AnomalyStudyEnabled))
                reasons.Add("ResearchQueue_StudyRequired".Translate());
            return reasons.Count > 0 ? string.Join("\n", reasons) : "ResearchQueue_OtherRequirements".Translate().ToString();
        }
    }
}

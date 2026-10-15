using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ResearchQueue.Research
{
    // A set of projects that share one research slot: standard research, an Anomaly knowledge category, or Gravtech.
    public sealed class ResearchSystem
    {
        public const string StandardId = "Standard";
        public const string GravtechId = "Gravship";

        private string? label;

        public string Id { get; }
        public KnowledgeCategoryDef? Category { get; }
        public bool IsGravtech => Id == GravtechId;
        public List<ResearchProjectDef> Projects { get; } = new List<ResearchProjectDef>();

        public ResearchSystem(string id, KnowledgeCategoryDef? category)
        {
            Id = id;
            Category = category;
        }

        public string Label => label ??= Category != null
            ? "ResearchQueue_AnomalyCategory".Translate(Category.LabelCap).ToString()
            : (IsGravtech ? "ResearchQueue_Gravtech" : "ResearchQueue_Standard").Translate().ToString();

        // Gravtech's slot belongs to Vanilla Gravship Expanded, which answers through its IsCurrentProject patch.
        public ResearchProjectDef? CurrentProject()
        {
            ResearchManager manager = Find.ResearchManager;
            if (Category != null) return manager.GetProject(Category);
            if (!IsGravtech)
            {
                ResearchProjectDef? current = manager.GetProject();
                return current != null && ResearchCatalog.SystemOf(current) == this ? current : null;
            }
            for (int i = 0; i < Projects.Count; i++)
                if (manager.IsCurrentProject(Projects[i])) return Projects[i];
            return null;
        }
    }
}

using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ResearchQueue.Research
{
    // Research systems, dependents and card tags, built once from the loaded defs.
    public static class ResearchCatalog
    {
        private static readonly List<ResearchProjectDef> NoProjects = new List<ResearchProjectDef>();
        private static readonly Dictionary<ResearchProjectDef, ResearchSystem> systemOf = new Dictionary<ResearchProjectDef, ResearchSystem>();
        private static readonly Dictionary<ResearchProjectDef, List<ResearchProjectDef>> dependents = new Dictionary<ResearchProjectDef, List<ResearchProjectDef>>();
        private static readonly Dictionary<ResearchProjectDef, string> tags = new Dictionary<ResearchProjectDef, string>();
        private static List<ResearchSystem>? systems;

        // Standard first, then Anomaly categories, then Gravtech; empty systems other than Standard are left out.
        public static IReadOnlyList<ResearchSystem> Systems
        {
            get
            {
                if (systems == null) Build();
                return systems!;
            }
        }

        public static ResearchSystem Standard => Systems[0];

        public static ResearchSystem? SystemOf(ResearchProjectDef project)
        {
            if (systems == null) Build();
            return systemOf.TryGetValue(project, out ResearchSystem? system) ? system : null;
        }

        public static IReadOnlyList<ResearchProjectDef> Dependents(ResearchProjectDef project)
        {
            if (systems == null) Build();
            return dependents.TryGetValue(project, out List<ResearchProjectDef>? list) ? list : NoProjects;
        }

        public static string Tag(ResearchProjectDef project)
        {
            if (systems == null) Build();
            return tags.TryGetValue(project, out string? tag) ? tag : string.Empty;
        }

        public static string Label(Def def) => def.label.NullOrEmpty() ? def.defName : def.LabelCap.ToString();

        // Shown but unnamed: undiscovered Anomaly projects, and future-era ones while Node Research hides those.
        public static bool Masked(ResearchProjectDef project) => project.IsHidden || NodeResearch.HidesFutureEra(project);

        // Masked projects are labeled as vanilla labels undiscovered ones: "(Unknown research)".
        public static string ProjectLabel(ResearchProjectDef project) =>
            Masked(project) ? "(" + "UnknownResearch".Translate() + ")" : Label(project);

        // Era, then cost: the queue's order, for lists of related projects.
        public static int ByEra(ResearchProjectDef a, ResearchProjectDef b) =>
            a.techLevel != b.techLevel ? a.techLevel.CompareTo(b.techLevel) : a.CostApparent.CompareTo(b.CostApparent);

        // As vanilla's research tab: projects the difficulty settings hide are left out, unless already being researched.
        public static bool Allowed(ResearchProjectDef project) =>
            Find.Storyteller.difficulty.AllowedBy(project.hideWhen) || Find.ResearchManager.IsCurrentProject(project);

        public static IEnumerable<ResearchProjectDef> Prerequisites(ResearchProjectDef project)
        {
            if (project.prerequisites != null)
                foreach (ResearchProjectDef prerequisite in project.prerequisites) yield return prerequisite;
            if (project.hiddenPrerequisites != null)
                foreach (ResearchProjectDef prerequisite in project.hiddenPrerequisites) yield return prerequisite;
        }

        private static void Build()
        {
            ResearchSystem standard = new ResearchSystem(ResearchSystem.StandardId, null);
            ResearchSystem gravtech = new ResearchSystem(ResearchSystem.GravtechId, null);
            Dictionary<KnowledgeCategoryDef, ResearchSystem> categories = new Dictionary<KnowledgeCategoryDef, ResearchSystem>();
            List<ResearchSystem> built = new List<ResearchSystem> { standard };
            if (ModsConfig.AnomalyActive)
            {
                foreach (KnowledgeCategoryDef category in DefDatabase<KnowledgeCategoryDef>.AllDefsListForReading)
                {
                    ResearchSystem system = new ResearchSystem(category.defName, category);
                    categories[category] = system;
                    built.Add(system);
                }
            }
            built.Add(gravtech);

            foreach (ResearchProjectDef project in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                foreach (ResearchProjectDef prerequisite in Prerequisites(project))
                {
                    if (!dependents.TryGetValue(prerequisite, out List<ResearchProjectDef>? list))
                        dependents[prerequisite] = list = new List<ResearchProjectDef>();
                    if (!list.Contains(project)) list.Add(project);
                }
                string tag = NodeResearch.Tag(project);
                if (tag.Length > 0) tags[project] = tag;

                if (!(project.Cost > 0f)) continue;
                ResearchSystem target;
                if (project.knowledgeCategory != null)
                {
                    if (!categories.TryGetValue(project.knowledgeCategory, out target!)) continue;
                }
                else target = IsGravtech(project) ? gravtech : standard;
                target.Projects.Add(project);
                systemOf[project] = target;
            }
            built.RemoveAll(system => system != standard && system.Projects.Count == 0);
            systems = built;
        }

        // Mirrors Vanilla Gravship Expanded's own test: the extension on the project or on its research tab.
        private static bool IsGravtech(ResearchProjectDef project) =>
            HasExtension(project, "GravtechResearchExtension") || (project.tab != null && HasExtension(project.tab, "GravtechResearchExtension"));

        public static bool HasExtension(Def def, string typeName)
        {
            if (def.modExtensions == null) return false;
            for (int i = 0; i < def.modExtensions.Count; i++)
                if (def.modExtensions[i].GetType().Name == typeName) return true;
            return false;
        }
    }
}

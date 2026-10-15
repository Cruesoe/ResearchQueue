using RimWorld;
using UnityEngine;
using Verse;

namespace ResearchQueue.UI
{
    // Card geometry, palette, separators, progress fill, and selection follow Semi-Random Research.
    public static class ResearchCard
    {
        public const float Height = 48f;

        public static Color ColorFor(ResearchProjectDef project)
        {
            if (ModsConfig.AnomalyActive && project.knowledgeCategory == KnowledgeCategoryDefOf.Basic) return new Color(0.65f, 0.35f, 0.5f);
            if (ModsConfig.AnomalyActive && project.knowledgeCategory == KnowledgeCategoryDefOf.Advanced) return new Color(0.45f, 0.35f, 0.5f);
            if (ResearchSystems.Key(project) == ResearchSystems.Gravship) return new Color(0.2f, 0.6f, 0.8f);
            switch (project.techLevel)
            {
                case TechLevel.Animal: return new Color(0.5f, 0.4f, 0.2f);
                case TechLevel.Neolithic: return new Color(0.6f, 0.35f, 0.35f);
                case TechLevel.Medieval: return new Color(0.6f, 0.6f, 0.3f);
                case TechLevel.Industrial: return new Color(0.4f, 0.6f, 0.3f);
                case TechLevel.Spacer: return new Color(0.3f, 0.5f, 0.6f);
                case TechLevel.Ultra: return new Color(0.45f, 0.35f, 0.6f);
                case TechLevel.Archotech: return new Color(0.6f, 0.35f, 0.6f);
                default: return TexUI.AvailResearchColor;
            }
        }

        public static string Label(Def project) => project.label.NullOrEmpty() ? project.defName : project.LabelCap.ToString();
        public static string Cost(ResearchProjectDef project) => project.ProgressApparent > 0f
            ? $"{project.ProgressApparent:N0}/{project.CostApparent:N0}" : project.CostApparent.ToString("N0");

        public static void Draw(Rect rect, ResearchProjectDef project, bool selected, bool blocked, float costWidth)
        {
            Color oldColor = GUI.color;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            Text.Font = GameFont.Small;
            Color category = ColorFor(project);
            bool over = Mouse.IsOver(rect);
            Color accent = selected ? Color.Lerp(category, Color.white, 0.3f) : category;
            if (Event.current.type == EventType.Repaint)
            {
                Widgets.DrawBoxSolid(rect, Color.Lerp(TexUI.AvailResearchColor, category, over ? 0.4f : 0.3f));
                if (project.ProgressPercent > 0f)
                {
                    Color progress = Color.Lerp(category, Color.white, 0.15f);
                    progress.a = 0.45f;
                    Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width * project.ProgressPercent, rect.height), progress);
                }
                GUI.color = accent;
                Widgets.DrawBox(rect, selected ? 2 : 1);
            }
            Rect icon = new Rect(rect.x + 4f, rect.y + 8f, 32f, 32f);
            float first = icon.xMax + 8f;
            float second = rect.xMax - costWidth - 9f;
            if (Event.current.type == EventType.Repaint)
            {
                GUI.color = Color.white;
                if (project.UnlockedDefs.Count > 0) Widgets.DefIcon(icon, project.UnlockedDefs[0]);
                Widgets.DrawLine(new Vector2(first, rect.y), new Vector2(first, rect.yMax), accent, 1f);
                Widgets.DrawLine(new Vector2(second, rect.y), new Vector2(second, rect.yMax), accent, 1f);
            }
            Rect name = new Rect(first + 12f, rect.y, Mathf.Max(0f, second - first - 16f), rect.height);
            string tag = NodeResearchBridge.Tag(project);
            Text.WordWrap = false;
            GUI.color = blocked ? Color.gray : new Color(0.95f, 0.95f, 0.95f);
            Text.Anchor = TextAnchor.MiddleLeft;
            if (tag.Length > 0)
            {
                Widgets.Label(new Rect(name.x, name.y + 2f, name.width, 22f), Label(project));
                GUI.color = new Color(0.95f, 0.75f, 0.25f);
                Widgets.Label(new Rect(name.x, name.y + 24f, name.width, 22f), tag);
            }
            else Widgets.Label(name, Label(project));
            GUI.color = blocked ? Color.gray : Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(second + 4f, rect.y, costWidth, rect.height), Cost(project));
            TooltipHandler.TipRegion(rect, Label(project) + "\n\n" + project.description +
                (blocked ? "\n\n" + ResearchSystems.BlockReason(project) : string.Empty));
            Text.Anchor = oldAnchor;
            Text.WordWrap = oldWrap;
            GUI.color = oldColor;
        }
    }
}

using System.Collections.Generic;
using ResearchQueue.Research;
using RimWorld;
using UnityEngine;
using Verse;

namespace ResearchQueue.UI
{
    // Card geometry, palette, separators, progress fill and selection follow Semi Random Research.
    public static class ResearchCard
    {
        public const float Height = 48f;
        public const float StatusWidth = 170f;

        public static readonly Color TextColor = new Color(0.95f, 0.95f, 0.95f);
        public static readonly Color TagColor = new Color(0.95f, 0.75f, 0.25f);

        public static Color ColorFor(ResearchProjectDef project)
        {
            if (project.knowledgeCategory != null)
                return project.knowledgeCategory == KnowledgeCategoryDefOf.Advanced ? new Color(0.45f, 0.35f, 0.5f) : new Color(0.65f, 0.35f, 0.5f);
            if (ResearchCatalog.SystemOf(project)?.IsGravtech == true) return new Color(0.2f, 0.6f, 0.8f);
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

        public static string Label(Def def) => def.label.NullOrEmpty() ? def.defName : def.LabelCap.ToString();

        public static string Cost(ResearchProjectDef project) => project.ProgressApparent > 0f
            ? project.ProgressApparent.ToString("N0") + "/" + project.CostApparent.ToString("N0")
            : project.CostApparent.ToString("N0");

        // Columns: icon | name and tag | cost | estimate or state.
        public static void Draw(Rect rect, QueueRow row, bool selected)
        {
            Color oldColor = GUI.color;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            Text.Font = GameFont.Small;
            Color accent = selected ? Color.Lerp(row.Color, Color.white, 0.3f) : row.Color;
            Rect icon = new Rect(rect.x + 4f, rect.y + 8f, 32f, 32f);
            float first = icon.xMax + 8f;
            float third = rect.xMax - StatusWidth;
            float second = third - row.CostWidth;
            if (Event.current.type == EventType.Repaint)
            {
                Background(rect, row.Color, Mouse.IsOver(rect));
                float progress = row.Project.ProgressPercent;
                if (progress > 0f)
                {
                    Color fill = Color.Lerp(row.Color, Color.white, 0.15f);
                    fill.a = 0.45f;
                    Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width * progress, rect.height), fill);
                }
                GUI.color = accent;
                Widgets.DrawBox(rect, selected ? 2 : 1);
                GUI.color = Color.white;
                if (row.Project.UnlockedDefs.Count > 0) Widgets.DefIcon(icon, row.Project.UnlockedDefs[0]);
                Widgets.DrawLine(new Vector2(first, rect.y), new Vector2(first, rect.yMax), accent, 1f);
                Widgets.DrawLine(new Vector2(second, rect.y), new Vector2(second, rect.yMax), accent, 1f);
                Widgets.DrawLine(new Vector2(third, rect.y), new Vector2(third, rect.yMax), accent, 1f);
            }
            Rect name = new Rect(first + 12f, rect.y, Mathf.Max(0f, second - first - 16f), rect.height);
            Text.WordWrap = false;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = row.Blocked ? Color.gray : TextColor;
            if (row.Tag.Length > 0)
            {
                Widgets.Label(new Rect(name.x, name.y + 2f, name.width, 22f), row.Label);
                GUI.color = TagColor;
                Widgets.Label(new Rect(name.x, name.y + 24f, name.width, 22f), row.Tag);
            }
            else Widgets.Label(name, row.Label);

            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = row.Blocked ? Color.gray : Color.white;
            Widgets.Label(new Rect(second, rect.y, row.CostWidth, rect.height), row.Cost);

            Rect status = new Rect(third + 4f, rect.y, StatusWidth - 8f, rect.height);
            if (row.IsNew)
            {
                GUI.color = TagColor;
                Widgets.Label(new Rect(status.x, status.y + 2f, status.width, 22f), "ResearchQueue_New".Translate());
                status = new Rect(status.x, status.y + 24f, status.width, 22f);
            }
            GUI.color = row.StatusWarns ? ColorLibrary.RedReadable : TextColor;
            Widgets.Label(status, row.Status);

            TooltipHandler.TipRegion(rect, new TipSignal(row.Tooltip, row.Project.shortHash));
            Text.Anchor = oldAnchor;
            Text.WordWrap = oldWrap;
            GUI.color = oldColor;
        }

        // Era, then cost: the queue's order, for lists of related projects.
        public static int ByEra(ResearchProjectDef a, ResearchProjectDef b) =>
            a.techLevel != b.techLevel ? a.techLevel.CompareTo(b.techLevel) : a.CostApparent.CompareTo(b.CostApparent);

        // Space above card i of an era-sorted list: wider where a new era starts.
        public static float GapBefore(IReadOnlyList<ResearchProjectDef> projects, int i) =>
            projects[i].techLevel != projects[i - 1].techLevel ? 14f : 4f;

        // Icon and name with its tag below, for lists of related projects.
        public static void DrawCompact(Rect rect, ResearchProjectDef project, bool highlight)
        {
            Color oldColor = GUI.color;
            TextAnchor oldAnchor = Text.Anchor;
            Color color = ColorFor(project);
            if (Event.current.type == EventType.Repaint)
            {
                Background(rect, color, highlight && Mouse.IsOver(rect));
                GUI.color = color;
                Widgets.DrawBox(rect);
                GUI.color = Color.white;
                if (project.UnlockedDefs.Count > 0) Widgets.DefIcon(new Rect(rect.x + 6f, rect.center.y - 16f, 32f, 32f), project.UnlockedDefs[0]);
            }
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = TextColor;
            Rect label = new Rect(rect.x + 46f, rect.y, rect.width - 50f, rect.height);
            string tag = ResearchCatalog.Tag(project);
            if (tag.Length > 0)
            {
                Widgets.Label(new Rect(label.x, label.y + 2f, label.width, 22f), Label(project));
                GUI.color = TagColor;
                Widgets.Label(new Rect(label.x, label.y + 24f, label.width, 22f), tag);
            }
            else Widgets.Label(label, Label(project));
            Text.Anchor = oldAnchor;
            GUI.color = oldColor;
        }

        public static void Background(Rect rect, Color color, bool hovered) =>
            Widgets.DrawBoxSolid(rect, Color.Lerp(TexUI.AvailResearchColor, color, hovered ? 0.4f : 0.3f));
    }
}

using System;
using System.Collections.Generic;
using ResearchQueue.Research;
using ResearchQueue.State;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ResearchQueue.UI
{
    // The selected project's panel.
    public sealed partial class MainTabWindow_ResearchQueue
    {
        private const float PanelPadding = 12f;
        private const float ActionIconSize = 24f;
        private const float ActionsWidth = 96f;

        private static readonly Texture2D ResearchNowIcon = ContentFinder<Texture2D>.Get("UI/ResearchQueue/ResearchNow");
        private static readonly Texture2D MoveToTopIcon = ContentFinder<Texture2D>.Get("UI/ResearchQueue/MoveToTop");
        private static readonly Texture2D SnoozeIcon = ContentFinder<Texture2D>.Get("UI/ResearchQueue/Snooze");
        private static readonly Texture2D RestoreIcon = ContentFinder<Texture2D>.Get("UI/ResearchQueue/Restore");

        private readonly List<ResearchProjectDef> detailsLeadsTo = new List<ResearchProjectDef>();
        private Vector2 detailsScroll;
        private ResearchProjectDef? detailsProject;
        private int detailsRevision = -1;
        private int detailsBucket = -1;
        private string? detailsReason;
        private bool detailsCanStart;
        private float detailsHeight;
        private Vector2 leadsToScroll;
        private float leadsToHeight;
        private Vector2 descriptionScroll;
        private QueueRow? detailsRow;
        private string detailsInfo = string.Empty;
        private float descriptionWidth = -1f;
        private float descriptionHeight;
        private float infoHeight = 24f;
        private readonly List<DetailsAction> detailsActions = new List<DetailsAction>();

        // An icon action worked out once per refresh; Tip already holds the label, explanation and any disabled reason.
        private readonly struct DetailsAction
        {
            public readonly Texture2D Icon;
            public readonly string Tip;
            public readonly Action? Act;

            public DetailsAction(Texture2D icon, string label, string tip, bool enabled, string? reason, Action act)
            {
                Icon = icon;
                Tip = label.Colorize(ColoredText.TipSectionTitleColor) + "\n\n" + tip + (!enabled && reason != null ? "\n\n" + reason.Colorize(ColorLibrary.RedReadable) : string.Empty);
                Act = enabled ? act : null;
            }
        }

        // The project's title, info line and description head the panel, then Unlocks and Leads to,
        // side by side when split, otherwise stacked in one scrolling list.
        private void DrawDetails(Rect rect, ResearchQueueWorldComponent tracker, bool split)
        {
            ResearchProjectDef? project = selectedProject;
            if (project == null || ResearchCatalog.SystemOf(project) == null) return;
            if (detailsProject != project || detailsRevision != builtRevision || detailsBucket != builtBucket)
            {
                detailsProject = project;
                detailsRevision = builtRevision;
                detailsBucket = builtBucket;
                detailsReason = Reason(project, tracker);
                detailsCanStart = ResearchRequirements.CanStart(project);
                detailsRow = DetailsRow(project, tracker);
                detailsInfo = InfoLine(detailsRow);
                descriptionWidth = -1f;
                BuildActions(project, tracker);
                detailsLeadsTo.Clear();
                foreach (ResearchProjectDef dependent in ResearchCatalog.Dependents(project))
                    if (!dependent.IsFinished && ResearchCatalog.Allowed(dependent)) detailsLeadsTo.Add(dependent);
                detailsLeadsTo.Sort(ResearchCatalog.ByEra);
            }

            // A framed panel: title with icon actions at its right, then one gray line: tag, cost and estimate.
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(PanelPadding);
            float contentWidth = inner.width - ResearchCard.ScrollReserve;
            DrawActions(new Rect(inner.x + contentWidth - ActionsWidth, inner.y, ActionsWidth, ColumnTitleHeight));
            ColumnTitle(new Rect(inner.x, inner.y, contentWidth - ActionsWidth - 8f, ColumnTitleHeight), ResearchCatalog.Label(project));
            if (descriptionWidth != contentWidth)
            {
                descriptionWidth = contentWidth;
                descriptionHeight = DescriptionHeight(contentWidth, project);
                infoHeight = Mathf.Max(24f, Text.CalcHeight(detailsInfo, contentWidth));
            }
            // The info line wraps when the panel is narrow.
            Rect info = new Rect(inner.x, inner.y + ColumnTitleHeight, contentWidth, infoHeight);
            Widgets.Label(info, detailsInfo);
            if (unestimated.Contains(project) && !tracker.IsExcluded(project))
                TooltipHandler.TipRegion(info, "ResearchQueue_NoEstimateTip".Translate());

            // Description and any block reason, up to a third of the panel; longer text scrolls.
            float y = info.yMax + 12f;
            Rect textOut = new Rect(inner.x, y, inner.width, Mathf.Min(descriptionHeight, inner.height / 3f));
            Widgets.BeginScrollView(textOut, ref descriptionScroll, new Rect(0f, 0f, contentWidth, descriptionHeight));
            try { DrawDescription(contentWidth, 0f, project); }
            finally { Widgets.EndScrollView(); }
            // Dividers like the queue column's: under the header, and between Unlocks and Leads to.
            float divider = textOut.yMax + 10f;
            Widgets.DrawLineHorizontal(inner.x, divider, inner.width - ResearchCard.ScrollReserve, DividerColor);
            Rect lists = new Rect(inner.x, divider + 10f, inner.width, inner.yMax - divider - 10f);

            if (split)
            {
                float columnWidth = (lists.width - ColumnGap) / 2f;
                float dividerX = lists.x + columnWidth + ColumnGap / 2f;
                Widgets.DrawLine(new Vector2(dividerX, lists.y), new Vector2(dividerX, lists.yMax), DividerColor, 1f);
                ListColumn(new Rect(lists.x, lists.y, columnWidth, lists.height), "ResearchQueue_Unlocks".Translate(),
                    project.UnlockedDefs.Count == 0 ? "ResearchQueue_UnlocksNone".Translate() : null, ref detailsScroll, ref detailsHeight, w => DrawUnlocks(w, 0f, project));
                ListColumn(new Rect(lists.xMax - columnWidth, lists.y, columnWidth, lists.height), "ResearchQueue_LeadsTo".Translate(),
                    detailsLeadsTo.Count == 0 ? "ResearchQueue_LeadsToNone".Translate() : null, ref leadsToScroll, ref leadsToHeight, w => DrawLeadsTo(w, 0f, project));
                return;
            }

            Rect view = new Rect(0f, 0f, lists.width - ResearchCard.ScrollReserve, Mathf.Max(detailsHeight, lists.height));
            Widgets.BeginScrollView(lists, ref detailsScroll, view);
            try
            {
                float listY = 0f;
                if (project.UnlockedDefs.Count > 0)
                {
                    listY = Heading(view.width, listY, "ResearchQueue_Unlocks".Translate());
                    listY = DrawUnlocks(view.width, listY, project) + 12f;
                }
                if (detailsLeadsTo.Count > 0)
                {
                    listY = Heading(view.width, listY, "ResearchQueue_LeadsTo".Translate());
                    listY = DrawLeadsTo(view.width, listY, project);
                }
                detailsHeight = listY;
            }
            finally
            {
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.EndScrollView();
            }
        }

        // The project's own queue card when it has one, so the info line matches it; otherwise one built the same way.
        private QueueRow DetailsRow(ResearchProjectDef project, ResearchQueueWorldComponent tracker)
        {
            if (currentRow?.Project == project) return currentRow;
            foreach (QueueRow row in rows)
                if (row.Project == project && row.Position > 0) return row;
            bool blocked = !project.IsFinished && !ResearchRequirements.CanStart(project);
            string status = project.IsFinished ? "ResearchQueue_Completed".Translate()
                : tracker.IsExcluded(project) ? (tracker.IsSnoozed(project) ? "ResearchQueue_Snoozed" : "ResearchQueue_PrerequisiteSnoozed").Translate()
                : blocked ? "ResearchQueue_Blocked".Translate()
                : estimates.TryGetValue(project, out string? estimate) ? estimate : "ResearchQueue_NoEstimate".Translate();
            return MakeRow(project, 0, blocked, status, blocked);
        }

        // Gold tag, then cost, own time and estimate in gray, separated by dots; a blocked or snoozed status is red.
        private string InfoLine(QueueRow row)
        {
            List<string> parts = new List<string>();
            if (row.Tag.Length > 0) parts.Add(row.Tag.Colorize(ResearchCard.TagColor));
            parts.Add("ResearchQueue_InfoCost".Translate(row.Cost).Colorize(Color.gray));
            if (row.Duration != null) parts.Add("ResearchQueue_InfoTakes".Translate(row.Duration).ToString().Colorize(Color.gray));
            bool estimate = estimates.TryGetValue(row.Project, out string? value) && value == row.Status;
            string status = estimate ? "ResearchQueue_InfoDone".Translate(row.Status).ToString() : row.Status;
            parts.Add(status.Colorize(row.StatusWarns ? ColorLibrary.RedReadable : Color.gray));
            return string.Join(" · ".Colorize(Color.gray), parts);
        }

        private float DescriptionHeight(float width, ResearchProjectDef project)
        {
            Text.Font = GameFont.Small;
            float height = 0f;
            if (!project.description.NullOrEmpty()) height += Text.CalcHeight(project.description, width) + 10f;
            if (detailsReason != null) height += Text.CalcHeight(detailsReason, width) + 10f;
            return height;
        }

        // Description and any block reason; cost and estimate are on the info line above.
        private float DrawDescription(float width, float y, ResearchProjectDef project)
        {
            if (!project.description.NullOrEmpty()) y = Paragraph(width, y, project.description);
            if (detailsReason != null)
            {
                GUI.color = ColorLibrary.RedReadable;
                y = Paragraph(width, y, detailsReason);
                GUI.color = Color.white;
            }
            return y;
        }

        // A headed list; the scroll bar's space is always kept so the content's edge never moves.
        private static void ListColumn(Rect rect, string heading, string? empty, ref Vector2 scroll, ref float contentHeight, Func<float, float> draw)
        {
            Widgets.BeginGroup(rect);
            try
            {
                float top = Heading(rect.width, 0f, heading);
                Rect outRect = new Rect(0f, top, rect.width, rect.height - top);
                if (empty != null)
                {
                    GUI.color = Color.gray;
                    Widgets.Label(outRect, empty);
                    return;
                }
                Rect view = new Rect(0f, 0f, outRect.width - ResearchCard.ScrollReserve, contentHeight);
                Widgets.BeginScrollView(outRect, ref scroll, view);
                try { contentHeight = draw(view.width); }
                finally { Widgets.EndScrollView(); }
            }
            finally
            {
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.EndGroup();
            }
        }

        // Small icon buttons right of the title, listed right to left: Snooze, Move to top, Research now; or Restore for an excluded project.
        private void BuildActions(ResearchProjectDef project, ResearchQueueWorldComponent tracker)
        {
            detailsActions.Clear();
            if (tracker.IsExcluded(project) && !project.IsFinished)
            {
                detailsActions.Add(new DetailsAction(RestoreIcon, "ResearchQueue_Restore".Translate(), "ResearchQueue_RestoreTip".Translate(),
                    tracker.IsSnoozed(project), detailsReason, () => tracker.Restore(project)));
                return;
            }
            detailsActions.Add(new DetailsAction(SnoozeIcon, "ResearchQueue_Snooze".Translate(), "ResearchQueue_SnoozeTip".Translate(),
                !project.IsFinished, "ResearchQueue_Completed".Translate(), () => ConfirmSnooze(project, tracker)));
            int index = tracker.StateOf(project)?.Queue.IndexOf(project.defName) ?? -1;
            string? nextReason = null;
            if (project.IsFinished) nextReason = "ResearchQueue_Completed".Translate();
            else if (Find.ResearchManager.IsCurrentProject(project)) nextReason = "InProgress".Translate();
            else if (index == 0) nextReason = "ResearchQueue_AlreadyTop".Translate();
            else if (index < 0 || !detailsCanStart) nextReason = detailsReason ?? "ResearchQueue_OtherRequirements".Translate();
            detailsActions.Add(new DetailsAction(MoveToTopIcon, "ResearchQueue_MoveToTop".Translate(), "ResearchQueue_MoveToTopTip".Translate(),
                nextReason == null, nextReason, () => tracker.MoveToFront(project)));
            string? nowReason = null;
            if (project.IsFinished) nowReason = "ResearchQueue_Completed".Translate();
            else if (Find.ResearchManager.IsCurrentProject(project)) nowReason = "InProgress".Translate();
            else if (!detailsCanStart) nowReason = detailsReason ?? "ResearchQueue_OtherRequirements".Translate();
            detailsActions.Add(new DetailsAction(ResearchNowIcon, "ResearchQueue_ResearchNow".Translate(), "ResearchQueue_ResearchNowTip".Translate(),
                nowReason == null, nowReason, () => tracker.ResearchNow(project)));
        }

        // White like the settings cog, highlighting on hover. Disabled: faint and inert.
        private void DrawActions(Rect rect)
        {
            Rect button = new Rect(rect.xMax - ActionIconSize, rect.center.y - ActionIconSize / 2f, ActionIconSize, ActionIconSize);
            foreach (DetailsAction action in detailsActions)
            {
                TooltipHandler.TipRegion(button, action.Tip);
                if (action.Act == null)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.2f);
                    GUI.DrawTexture(button, action.Icon);
                    GUI.color = Color.white;
                }
                else if (Widgets.ButtonImage(button, action.Icon, Color.white, GenUI.MouseoverColor)) action.Act();
                button.x -= ActionIconSize + 12f;
            }
        }

        private static float Paragraph(float width, float y, string text)
        {
            float height = Text.CalcHeight(text, width);
            Widgets.Label(new Rect(0f, y, width, height), text);
            return y + height + 10f;
        }

        // Unfinished projects this one is a prerequisite for, as small cards grouped by era; clicking one selects it, switching queue or view as needed.
        private float DrawLeadsTo(float width, float y, ResearchProjectDef selected)
        {
            for (int i = 0; i < detailsLeadsTo.Count; i++)
            {
                ResearchProjectDef project = detailsLeadsTo[i];
                if (i > 0) y += ResearchCard.GapBefore(detailsLeadsTo, i);
                Rect row = new Rect(0f, y, width, ResearchCard.Height);
                ResearchCard.DrawCompact(row, project, true);
                y += row.height;
                if (ResearchCatalog.Masked(project)) continue;
                TooltipHandler.TipRegion(row, new TipSignal(() => OtherPrerequisites(project, selected), project.shortHash ^ 0x5a17));
                if (Widgets.ButtonInvisible(row, false))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    selectedProject = project;
                }
            }
            return y;
        }

        private static string OtherPrerequisites(ResearchProjectDef dependent, ResearchProjectDef shown)
        {
            List<string> others = new List<string>();
            foreach (ResearchProjectDef prerequisite in ResearchCatalog.Prerequisites(dependent))
                if (prerequisite != shown && !prerequisite.IsFinished) others.Add(ResearchCatalog.ProjectLabel(prerequisite));
            return others.Count == 0 ? "ResearchQueue_LastPrerequisite".Translate().ToString() : "ResearchQueue_AlsoRequires".Translate(string.Join(", ", others));
        }

        private float DrawUnlocks(float width, float y, ResearchProjectDef project)
        {
            foreach (Def unlock in project.UnlockedDefs)
            {
                Rect row = new Rect(0f, y, width, 30f);
                if (MatchesUnlock(unlock)) Widgets.DrawTextHighlight(row);
                Widgets.DrawHighlightIfMouseover(row);
                Widgets.DefIcon(new Rect(row.x + 2f, row.y + 2f, 26f, 26f), unlock);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(row.x + 36f, row.y, row.width - 36f, row.height), ResearchCatalog.Label(unlock));
                Text.Anchor = TextAnchor.UpperLeft;
                if (!unlock.description.NullOrEmpty()) TooltipHandler.TipRegion(row, unlock.description);
                if (Widgets.ButtonInvisible(row)) Find.WindowStack.Add(new Dialog_InfoCard(unlock));
                y += 32f;
            }
            return y;
        }

        private static float Heading(float width, float y, string text)
        {
            GUI.color = Color.gray;
            Widgets.Label(new Rect(0f, y, width, 24f), text);
            GUI.color = Color.white;
            return y + 26f;
        }
    }
}

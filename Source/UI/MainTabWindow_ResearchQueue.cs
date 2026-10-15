using System;
using System.Collections.Generic;
using ResearchQueue.Core;
using ResearchQueue.Research;
using ResearchQueue.State;
using RimWorld;
using UnityEngine;
using Verse;

namespace ResearchQueue.UI
{
    // Replaces the Research tab. It subclasses the vanilla window because the game casts the tab window to it
    // (alerts, the void monolith, the entity codex, info card links); those calls set CurTab or selectedProject, which this window follows.
    public sealed partial class MainTabWindow_ResearchQueue : MainTabWindow_Research
    {
        private const float RowHeight = 54f;
        private const float NumberWidth = 44f;
        private const float StatusWidth = 185f;
        private const float RefreshTicks = 250;

        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private readonly List<TabRecord> systemTabs = new List<TabRecord>();
        private readonly List<QueueRow> rows = new List<QueueRow>();
        private readonly List<string> rowIds = new List<string>();
        private readonly Dictionary<ResearchProjectDef, string> estimates = new Dictionary<ResearchProjectDef, string>();
        private readonly HashSet<string> newIds = new HashSet<string>();
        private readonly Dictionary<ResearchSystem, Vector2>[] scrolls = { new Dictionary<ResearchSystem, Vector2>(), new Dictionary<ResearchSystem, Vector2>() };
        private readonly Action<int, int> onReordered;
        private ResearchSystem? system;
        private ResearchSystemQueueState? state;
        private QueueRow? currentRow;
        private bool showingSnoozed;
        private bool rebuild = true;
        private int builtRevision = -1;
        private int builtBucket = -1;
        private int reorderGroup = -1;
        private bool scrollToSelection;
        private ResearchProjectDef? syncedSelection;
        private ResearchTabDef? syncedTab;

        public MainTabWindow_ResearchQueue()
        {
            onReordered = OnReordered;
        }

        private ResearchSystem System => system ??= ResearchCatalog.Standard;

        protected override float Margin => 12f;
        public override Vector2 RequestedTabSize => new Vector2(Verse.UI.screenWidth, Verse.UI.screenHeight - 35f);
        public override Vector2 InitialSize => RequestedTabSize;

        public override void PreOpen()
        {
            base.PreOpen();
            search.Reset();
            showingSnoozed = false;
            rebuild = true;
            syncedTab = CurTab;
            ResearchSystem? opened = selectedProject == null ? null : ResearchCatalog.SystemOf(selectedProject);
            if (opened != null) system = opened;
            SelectProject(opened != null ? selectedProject : System.CurrentProject());
        }

        public override void PostClose()
        {
            base.PostClose();
            ResearchQueueWorldComponent.Get()?.ClearNewMarkers();
        }

        public override void DoWindowContents(Rect inRect)
        {
            ResearchQueueWorldComponent? tracker = ResearchQueueWorldComponent.Get();
            if (tracker == null) return;
            if (tracker.Dirty) tracker.Refresh();
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            FollowVanillaSelection(tracker);

            Rect content = inRect;
            if (ResearchCatalog.Systems.Count > 1)
            {
                content.yMin += TabDrawer.TabHeight;
                Widgets.DrawMenuSection(content);
                DrawSystemTabs(content);
            }
            else Widgets.DrawMenuSection(content);
            content = content.ContractedBy(10f);

            state = tracker.StateOf(System);
            RebuildIfNeeded(tracker);
            DrawToolbar(new Rect(content.x, content.y, content.width, 30f));

            float detailsWidth = Mathf.Clamp(content.width * 0.27f, 270f, 430f);
            Rect left = new Rect(content.x, content.y + 40f, content.width - detailsWidth - 20f, content.height - 40f);
            Rect right = new Rect(left.xMax + 20f, left.y, detailsWidth, left.height);
            DrawCurrent(new Rect(left.x, left.y, left.width, 74f), tracker);
            DrawQueue(new Rect(left.x, left.y + 84f, left.width, left.height - 84f), tracker);
            DrawDetails(right, tracker);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // Codex and hyperlinks call Select, alerts and the monolith set CurTab; a changed selection wins over a changed tab.
        private void FollowVanillaSelection(ResearchQueueWorldComponent tracker)
        {
            if (selectedProject != syncedSelection)
            {
                syncedSelection = selectedProject;
                syncedTab = CurTab;
                ResearchSystem? target = selectedProject == null ? null : ResearchCatalog.SystemOf(selectedProject);
                if (target == null)
                {
                    SelectProject(System.CurrentProject());
                    return;
                }
                system = target;
                showingSnoozed = tracker.IsExcluded(selectedProject!);
                scrollToSelection = true;
                rebuild = true;
            }
            else if (CurTab != syncedTab)
            {
                syncedTab = CurTab;
                ResearchSystem? target = SystemForTab(CurTab);
                if (target != null) SelectSystem(target);
            }
        }

        private static ResearchSystem? SystemForTab(ResearchTabDef? tab)
        {
            if (tab == null) return null;
            foreach (ResearchSystem candidate in ResearchCatalog.Systems)
            {
                if (tab == ResearchTabDefOf.Anomaly ? candidate.Category != null : candidate.IsGravtech && ResearchCatalog.HasExtension(tab, "GravtechResearchExtension"))
                    return candidate;
            }
            return tab == ResearchTabDefOf.Main ? ResearchCatalog.Standard : null;
        }

        private void SelectSystem(ResearchSystem target)
        {
            system = target;
            showingSnoozed = false;
            reorderGroup = -1;
            rebuild = true;
            SelectProject(target.CurrentProject());
        }

        private void SelectProject(ResearchProjectDef? project)
        {
            selectedProject = project;
            syncedSelection = project;
            detailsScroll = Vector2.zero;
            detailsProject = null;
        }

        private void DrawSystemTabs(Rect content)
        {
            if (systemTabs.Count != ResearchCatalog.Systems.Count)
            {
                systemTabs.Clear();
                foreach (ResearchSystem candidate in ResearchCatalog.Systems)
                    systemTabs.Add(new TabRecord(candidate.Label, () => SelectSystem(candidate), () => System == candidate));
            }
            TabDrawer.DrawTabs(content, systemTabs);
        }

        private void DrawToolbar(Rect rect)
        {
            float searchWidth = Mathf.Min(300f, rect.width * 0.3f);
            search.OnGUI(new Rect(rect.x, rect.y + 3f, searchWidth, 24f), () => rebuild = true);
            Rect queueButton = new Rect(rect.x + searchWidth + 12f, rect.y, 140f, 30f);
            Rect snoozedButton = new Rect(queueButton.xMax + 6f, rect.y, 140f, 30f);
            if (!showingSnoozed) Widgets.DrawHighlightSelected(queueButton);
            else Widgets.DrawHighlightSelected(snoozedButton);
            if (Widgets.ButtonText(queueButton, "ResearchQueue_Queue".Translate()) && showingSnoozed) SetSnoozedView(false);
            if (Widgets.ButtonText(snoozedButton, "ResearchQueue_Snoozed".Translate()) && !showingSnoozed) SetSnoozedView(true);
            ResearchQueueSettings settings = ResearchQueueMod.Settings;
            Rect format = new Rect(rect.xMax - 220f, rect.y, 220f, 30f);
            if (Widgets.ButtonText(format, (settings.ShowCompletionDate ? "ResearchQueue_ShowingDate" : "ResearchQueue_ShowingDays").Translate()))
            {
                settings.ShowCompletionDate = !settings.ShowCompletionDate;
                ResearchQueueMod.Instance.WriteSettings();
                rebuild = true;
            }
            TooltipHandler.TipRegion(format, "ResearchQueue_EstimateFormatTip".Translate());
        }

        private void SetSnoozedView(bool snoozed)
        {
            showingSnoozed = snoozed;
            reorderGroup = -1;
            rebuild = true;
        }

        // Estimates move with the current project's progress, so rows also rebuild every few seconds of game time.
        private void RebuildIfNeeded(ResearchQueueWorldComponent tracker)
        {
            int bucket = (int)(Find.TickManager.TicksGame / RefreshTicks);
            if (!rebuild && builtRevision == tracker.Revision && builtBucket == bucket) return;
            rebuild = false;
            builtRevision = tracker.Revision;
            builtBucket = bucket;
            rows.Clear();
            rowIds.Clear();
            estimates.Clear();
            newIds.Clear();
            newIds.UnionWith(state!.NewIds);

            float rate = QueueRules.Average(state.Rates);
            float work = 0f;
            currentRow = null;
            ResearchProjectDef? current = System.CurrentProject();
            if (current != null)
            {
                work = Remaining(current);
                string estimate = Estimate(work, rate);
                estimates[current] = estimate;
                currentRow = MakeRow(current, 0, false, estimate);
            }

            if (showingSnoozed)
            {
                string snoozed = "ResearchQueue_Snoozed".Translate();
                foreach (ResearchProjectDef project in System.Projects)
                    if (!project.IsFinished && tracker.IsExcluded(project) && Matches(project)) rows.Add(MakeRow(project, 0, false, snoozed));
                return;
            }

            // Filtered-out entries still count toward later estimates and positions.
            int position = 0;
            foreach (string id in state.Queue)
            {
                ResearchProjectDef? project = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(id);
                if (project == null) continue;
                position++;
                bool blocked = !ResearchRequirements.CanStart(project);
                string status;
                if (blocked) status = "ResearchQueue_Blocked".Translate();
                else
                {
                    work += Remaining(project);
                    status = Estimate(work, rate);
                    estimates[project] = status;
                }
                if (!Matches(project)) continue;
                rows.Add(MakeRow(project, position, blocked, status));
                rowIds.Add(id);
            }
        }

        private QueueRow MakeRow(ResearchProjectDef project, int position, bool blocked, string status) =>
            new QueueRow(project, position, blocked, newIds.Contains(project.defName), status, () => Tooltip(project));

        private bool Matches(ResearchProjectDef project) => !search.filter.Active || search.filter.Matches(ResearchCard.Label(project));

        private static float Remaining(ResearchProjectDef project) => Mathf.Max(0f, project.CostApparent - project.ProgressApparent);

        private static string Estimate(float work, float rate)
        {
            float days = QueueRules.CompletionDays(work, rate);
            if (days < 0f || days > 100000f) return "ResearchQueue_Unknown".Translate();
            if (!ResearchQueueMod.Settings.ShowCompletionDate) return "ResearchQueue_DaysEstimate".Translate(days.ToString("0.0"));
            Map? map = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            if (map == null) return "ResearchQueue_DaysEstimate".Translate(days.ToString("0.0"));
            long ticks = Find.TickManager.TicksAbs + (long)(days * GenDate.TicksPerDay);
            return "ResearchQueue_DateEstimate".Translate(GenDate.DateFullStringAt(ticks, Find.WorldGrid.LongLatOf(map.Tile)));
        }

        private static string Tooltip(ResearchProjectDef project)
        {
            string text = ResearchCard.Label(project);
            if (!project.description.NullOrEmpty()) text += "\n\n" + project.description;
            ResearchQueueWorldComponent? tracker = ResearchQueueWorldComponent.Get();
            string? reason = tracker == null ? null : Reason(project, tracker);
            return reason == null ? text : text + "\n\n" + reason;
        }

        private static string? Reason(ResearchProjectDef project, ResearchQueueWorldComponent tracker)
        {
            if (project.IsFinished) return null;
            if (tracker.IsExcluded(project))
            {
                if (tracker.IsSnoozed(project)) return "ResearchQueue_ExplicitSnooze".Translate();
                List<string> roots = tracker.SnoozedPrerequisites(project).ConvertAll(ResearchCard.Label);
                return "ResearchQueue_SnoozedPrerequisite".Translate(string.Join(", ", roots));
            }
            return ResearchRequirements.CanStart(project) || Find.ResearchManager.IsCurrentProject(project) ? null : ResearchRequirements.BlockReason(project);
        }

        private void DrawCurrent(Rect rect, ResearchQueueWorldComponent tracker)
        {
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f), "ResearchQueue_Current".Translate());
            Rect row = new Rect(rect.x, rect.y + 26f, rect.width, ResearchCard.Height);
            if (currentRow == null)
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(row, "ResearchQueue_Idle".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }
            DrawRow(row, currentRow, tracker, false);
        }

        private void DrawQueue(Rect rect, ResearchQueueWorldComponent tracker)
        {
            Dictionary<ResearchSystem, Vector2> scrollSet = scrolls[showingSnoozed ? 1 : 0];
            Vector2 scroll = scrollSet.TryGetValue(System, out Vector2 saved) ? saved : Vector2.zero;
            Rect view = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(rect.height, rows.Count * RowHeight));
            if (scrollToSelection)
            {
                scrollToSelection = false;
                int index = rows.FindIndex(r => r.Project == selectedProject);
                if (index >= 0) scroll.y = Mathf.Clamp(index * RowHeight - rect.height / 2f, 0f, Mathf.Max(0f, view.height - rect.height));
            }
            Widgets.BeginScrollView(rect, ref scroll, view);
            try
            {
                if (rows.Count == 0)
                    Widgets.Label(new Rect(0f, 0f, view.width, 60f), (showingSnoozed ? "ResearchQueue_NoSnoozed" : "ResearchQueue_NoQueued").Translate());
                bool canReorder = !showingSnoozed && !search.filter.Active && rows.Count > 1;
                if (Event.current.type == EventType.Repaint)
                    reorderGroup = canReorder ? ReorderableWidget.NewGroup(onReordered, ReorderableDirection.Vertical, view, RowHeight - ResearchCard.Height) : -1;
                else if (!canReorder) reorderGroup = -1;
                int first = QueueRules.FirstVisibleRow(rows.Count, scroll.y, RowHeight);
                int end = QueueRules.EndVisibleRow(rows.Count, scroll.y, rect.height, RowHeight);
                for (int i = first; i < end; i++)
                    DrawRow(new Rect(0f, i * RowHeight, view.width, ResearchCard.Height), rows[i], tracker, true);
                // Every row registers on repaint, after the cards, so drop indexes match the list and drag highlights draw on top.
                if (reorderGroup >= 0 && Event.current.type == EventType.Repaint)
                    for (int i = 0; i < rows.Count; i++)
                        ReorderableWidget.Reorderable(reorderGroup, CardRect(new Rect(0f, i * RowHeight, view.width, ResearchCard.Height), true));
            }
            finally { Widgets.EndScrollView(); }
            scrollSet[System] = scroll;
        }

        private void OnReordered(int from, int boundary)
        {
            ResearchQueueWorldComponent? tracker = ResearchQueueWorldComponent.Get();
            if (tracker == null || state == null) return;
            tracker.MoveVisible(state, rowIds, from, boundary);
        }

        private static Rect CardRect(Rect row, bool numbered)
        {
            float x = numbered ? NumberWidth : 0f;
            return new Rect(row.x + x, row.y, Mathf.Max(150f, row.width - x - StatusWidth), row.height);
        }

        private void DrawRow(Rect rect, QueueRow row, ResearchQueueWorldComponent tracker, bool numbered)
        {
            Rect card = CardRect(rect, numbered);
            if (numbered && row.Position > 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(rect.x, rect.y, NumberWidth, rect.height), row.Position.ToString());
            }
            if (numbered && reorderGroup >= 0 && Event.current.type != EventType.Repaint) ReorderableWidget.Reorderable(reorderGroup, card);
            ResearchCard.Draw(card, row, row.Project == selectedProject);

            Rect status = new Rect(card.xMax + 12f, rect.y, rect.xMax - card.xMax - 12f, rect.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            if (row.IsNew)
            {
                GUI.color = ResearchCard.TagColor;
                Widgets.Label(new Rect(status.x, status.y, status.width, 24f), "ResearchQueue_New".Translate());
                GUI.color = Color.white;
                Widgets.Label(new Rect(status.x, status.y + 24f, status.width, 24f), row.Status);
            }
            else Widgets.Label(status, row.Status);
            if (row.Blocked) TooltipHandler.TipRegion(status, new TipSignal(row.Tooltip, row.Project.shortHash));
            Text.Anchor = TextAnchor.UpperLeft;

            if (Event.current.type != EventType.MouseDown || !Mouse.IsOver(card)) return;
            if (Event.current.button == 0) SelectProject(row.Project);
            else if (Event.current.button == 1) OpenMenu(row, tracker);
            Event.current.Use();
        }

        private void OpenMenu(QueueRow row, ResearchQueueWorldComponent tracker)
        {
            ResearchProjectDef project = row.Project;
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            if (showingSnoozed && row.Position == 0 && tracker.IsExcluded(project))
                options.Add(new FloatMenuOption("ResearchQueue_Restore".Translate(), tracker.IsSnoozed(project) ? () => tracker.Restore(project) : (Action?)null));
            else
            {
                options.Add(new FloatMenuOption("ResearchQueue_Snooze".Translate(), () => ConfirmSnooze(project, tracker)));
                if (row.Position > 0)
                    options.Add(new FloatMenuOption("ResearchQueue_Next".Translate(), CanMoveToFront(row) ? () => tracker.MoveToFront(project) : (Action?)null));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static bool CanMoveToFront(QueueRow row) => row.Position > 1 && !row.Blocked;

        private static void ConfirmSnooze(ResearchProjectDef project, ResearchQueueWorldComponent tracker)
        {
            List<ResearchProjectDef> dependents = tracker.QueuedDependents(project);
            if (dependents.Count == 0 || !ResearchQueueMod.Settings.ConfirmDependentSnooze)
            {
                tracker.Snooze(project);
                return;
            }
            string message = "ResearchQueue_ConfirmSnooze".Translate(ResearchCard.Label(project)) + "\n\n" +
                string.Join("\n", dependents.ConvertAll(p => "  - " + ResearchCard.Label(p)));
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(message, () => tracker.Snooze(project)));
        }
    }
}

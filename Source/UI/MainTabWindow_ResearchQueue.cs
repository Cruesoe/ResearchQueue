using System;
using System.Collections.Generic;
using ResearchQueue.Core;
using ResearchQueue.Research;
using ResearchQueue.State;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ResearchQueue.UI
{
    // Replaces the Research tab. It subclasses the vanilla window because the game casts the tab window to it
    // (alerts, the void monolith, the entity codex, info card links); those calls set CurTab or selectedProject, which this window follows.
    [StaticConstructorOnStartup]
    public sealed partial class MainTabWindow_ResearchQueue : MainTabWindow_Research
    {
        private const float RowHeight = 54f;
        private const float NumberWidth = 44f;
        private const float HeaderHeight = 26f;
        private const float ColumnTitleHeight = 36f;
        private const float ColumnContentTop = 42f;
        private const float ColumnGap = 20f;
        private const float DetailsWidth = 380f;
        private const float LeadsToWidth = 360f;
        private const float MinQueueWidth = 620f;
        private const float RefreshTicks = 250;
        private static readonly Texture2D SettingsIcon = ContentFinder<Texture2D>.Get("UI/ResearchQueue/Settings");
        // Vanilla's menu section border color (private in Widgets), so dividers match the details panel's frame.
        private static readonly Color DividerColor = new ColorInt(135, 135, 135).ToColor;
        private static readonly Color DropLineColor = new Color(1f, 0.8f, 0.2f);

        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private readonly List<TabRecord> systemTabs = new List<TabRecord>();
        private readonly List<QueueRow> rows = new List<QueueRow>();
        private readonly List<string> rowIds = new List<string>();
        private readonly Dictionary<ResearchProjectDef, string> estimates = new Dictionary<ResearchProjectDef, string>();
        private readonly HashSet<string> newIds = new HashSet<string>();
        private readonly HashSet<ResearchProjectDef> unestimated = new HashSet<ResearchProjectDef>();
        private readonly Dictionary<ResearchSystem, Vector2>[] scrolls = { new Dictionary<ResearchSystem, Vector2>(), new Dictionary<ResearchSystem, Vector2>() };
        private readonly Action<int, int> onReordered;
        private ResearchSystem? system;
        private ResearchSystemQueueState? state;
        private QueueRow? currentRow;
        private int queuedCount;
        private string? queueTotal;
        private int snoozedCount;
        private bool showingSnoozed;
        private bool rebuild = true;
        private bool builtShowDates;
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
        public override Vector2 RequestedTabSize => new Vector2(1520f, Verse.UI.screenHeight - 35f);

        public override void PreOpen()
        {
            base.PreOpen();
            // Polling is hourly while research runs, so opening the tab refreshes to show anything unlocked since.
            ResearchQueueWorldComponent.Get()?.RequestRefresh();
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
            if (ResearchCatalog.Systems.Count > 1) content.yMin += TabDrawer.TabHeight;
            Widgets.DrawMenuSection(content);
            if (ResearchCatalog.Systems.Count > 1) DrawSystemTabs(content);
            content = content.ContractedBy(10f);

            state = tracker.StateOf(System);
            RebuildIfNeeded(tracker);
            DrawToolbar(new Rect(content.x, content.y, content.width, 30f));

            // Queue | details; the details split into Unlocks and Leads to side by side when there is room, otherwise stack.
            float top = content.y + 42f, height = content.height - 42f;
            bool split = content.width - DetailsWidth - LeadsToWidth - ColumnGap * 2f >= MinQueueWidth;
            float detailsWidth = split ? DetailsWidth + ColumnGap + LeadsToWidth : Mathf.Clamp(content.width * 0.33f, 270f, 400f);
            Rect left = new Rect(content.x, top, content.width - detailsWidth - ColumnGap, height);
            // The details panel starts at the line under Current research, level with the queue.
            float panelTop = top + ColumnContentTop + ResearchCard.Height + 14f;
            Rect details = new Rect(left.xMax + ColumnGap, panelTop, detailsWidth, left.yMax - panelTop);
            float y = DrawCurrent(left, tracker);
            DrawQueue(new Rect(left.x, y, left.width, left.yMax - y), tracker);
            DrawDetails(details, tracker, split);
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
                detailsScroll = Vector2.zero;
                leadsToScroll = Vector2.zero;
                descriptionScroll = Vector2.zero;
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
            leadsToScroll = Vector2.zero;
            descriptionScroll = Vector2.zero;
            detailsProject = null;
        }

        private void DrawSystemTabs(Rect content)
        {
            if (systemTabs.Count != ResearchCatalog.Systems.Count)
            {
                systemTabs.Clear();
                foreach (ResearchSystem candidate in ResearchCatalog.Systems)
                    systemTabs.Add(new TabRecord(candidate.Label, () => SelectSystem(candidate), () => System == candidate));
                ResearchQueueWorldComponent? tracker = ResearchQueueWorldComponent.Get();
                if (tracker != null) UpdateTabLabels(tracker);
            }
            TabDrawer.DrawTabs(content, systemTabs);
        }

        // Each tab counts its current and queued projects, and greys out when it has none.
        private void UpdateTabLabels(ResearchQueueWorldComponent tracker)
        {
            for (int i = 0; i < systemTabs.Count && i < ResearchCatalog.Systems.Count; i++)
            {
                ResearchSystem candidate = ResearchCatalog.Systems[i];
                int count = candidate.CurrentProject() != null ? 1 : 0;
                foreach (string id in tracker.StateOf(candidate).Queue)
                    if (DefDatabase<ResearchProjectDef>.GetNamedSilentFail(id) != null) count++;
                systemTabs[i].label = "ResearchQueue_TabCount".Translate(candidate.Label, count);
                systemTabs[i].labelColor = count == 0 ? Color.gray : (Color?)null;
            }
        }

        private void DrawToolbar(Rect rect)
        {
            float searchWidth = Mathf.Min(300f, rect.width * 0.3f);
            search.OnGUI(new Rect(rect.x, rect.y + 3f, searchWidth, 24f), () => rebuild = true);
            Rect toggle = new Rect(rect.x + searchWidth + 12f, rect.y, 180f, 30f);
            string label = showingSnoozed ? "ResearchQueue_ShowQueue".Translate(queuedCount) : "ResearchQueue_ShowSnoozed".Translate(snoozedCount);
            if (Widgets.ButtonText(toggle, label)) SetSnoozedView(!showingSnoozed);
            TooltipHandler.TipRegion(toggle, (showingSnoozed ? "ResearchQueue_ShowQueueTip" : "ResearchQueue_ShowSnoozedTip").Translate());

            // Right to left: settings, the vanilla tree, then Node Research when it is loaded.
            Rect button = new Rect(rect.xMax - 28f, rect.y + 1f, 28f, 28f);
            if (Widgets.ButtonImage(button, SettingsIcon, Color.white, GenUI.MouseoverColor, true, "ResearchQueue_SettingsTip".Translate()))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                Find.WindowStack.Add(new Dialog_ModSettings(ResearchQueueMod.Instance));
            }
            button.x -= 36f;
            if (Widgets.ButtonImage(button, ResearchScreens.VanillaIcon, Color.white, GenUI.MouseoverColor, true, "ResearchQueue_OpenVanillaTip".Translate()))
                ResearchScreens.OpenVanilla(this);
            if (ResearchScreens.NodeIcon == null) return;
            button.x -= 36f;
            if (Widgets.ButtonImage(button, ResearchScreens.NodeIcon, Color.white, GenUI.MouseoverColor, true, "ResearchQueue_OpenNodeTip".Translate()))
                ResearchScreens.OpenNode(this);
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
            if (!rebuild && builtRevision == tracker.Revision && builtBucket == bucket && !SettingsChanged()) return;
            rebuild = false;
            builtRevision = tracker.Revision;
            builtBucket = bucket;
            builtShowDates = ResearchQueueMod.Settings.ShowCompletionDate;
            UpdateTabLabels(tracker);
            rows.Clear();
            rowIds.Clear();
            estimates.Clear();
            unestimated.Clear();
            string noEstimate = "ResearchQueue_NoEstimate".Translate();
            newIds.Clear();
            newIds.UnionWith(state!.NewIds);

            float rate = QueueRules.Average(state.Rates);
            float work = 0f;
            currentRow = null;
            ResearchProjectDef? current = System.CurrentProject();
            if (current != null)
            {
                work = Remaining(current);
                currentRow = MakeRow(current, 0, false, Record(current, Estimate(work, rate)) ?? noEstimate, false);
            }

            snoozedCount = 0;
            string snoozedLabel = "ResearchQueue_Snoozed".Translate();
            string prerequisiteLabel = "ResearchQueue_PrerequisiteSnoozed".Translate();
            foreach (ResearchProjectDef project in System.Projects)
            {
                if (project.IsFinished || !tracker.IsExcluded(project) || !ResearchCatalog.Allowed(project)) continue;
                snoozedCount++;
                if (showingSnoozed && Matches(project))
                    rows.Add(MakeRow(project, 0, false, tracker.IsSnoozed(project) ? snoozedLabel : prerequisiteLabel, false));
            }

            // Filtered-out entries still count toward later estimates and positions.
            queuedCount = 0;
            string blockedLabel = "ResearchQueue_Blocked".Translate();
            foreach (string id in state.Queue)
            {
                ResearchProjectDef? project = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(id);
                if (project == null) continue;
                queuedCount++;
                if (showingSnoozed) continue;
                bool blocked = !ResearchRequirements.CanStart(project);
                string status = blockedLabel;
                if (!blocked)
                {
                    work += Remaining(project);
                    status = Record(project, Estimate(work, rate)) ?? noEstimate;
                }
                if (!Matches(project)) continue;
                rows.Add(MakeRow(project, queuedCount, blocked, status, blocked));
                rowIds.Add(id);
            }

            // Time to finish the current project and every startable queued one at the measured rate.
            float totalDays = QueueRules.CompletionDays(work, rate);
            queueTotal = totalDays > 0f && totalDays < 30000f ? ((int)(totalDays * GenDate.TicksPerDay)).ToStringTicksToPeriod(allowSeconds: false) : null;
            search.noResultsMatched = search.filter.Active && rows.Count == 0;
        }

        private bool SettingsChanged() => builtShowDates != ResearchQueueMod.Settings.ShowCompletionDate;

        private QueueRow MakeRow(ResearchProjectDef project, int position, bool blocked, string status, bool warns) =>
            new QueueRow(project, position, blocked, newIds.Contains(project.defName), status, warns,
                () => Tooltip(project) + "\n\n" + (position > 0 ? "ResearchQueue_QueuedCardHint" : "ResearchQueue_CardHint").Translate().Colorize(ColoredText.SubtleGrayColor));

        // As vanilla: the name or anything it unlocks; undiscovered projects never match.
        private bool Matches(ResearchProjectDef project) =>
            !search.filter.Active || (!ResearchCatalog.Masked(project) && (search.filter.Matches(ResearchCatalog.Label(project)) || project.UnlockedDefs.Any(MatchesUnlock)));

        private bool MatchesUnlock(Def unlock) => search.filter.Active && search.filter.Matches(unlock.label);

        private static float Remaining(ResearchProjectDef project) => Mathf.Max(0f, project.CostApparent - project.ProgressApparent);

        private string? Record(ResearchProjectDef project, string? estimate)
        {
            if (estimate != null) estimates[project] = estimate;
            else unestimated.Add(project);
            return estimate;
        }

        // Null until a research rate has been measured.
        private static string? Estimate(float work, float rate)
        {
            float days = QueueRules.CompletionDays(work, rate);
            if (days < 0f || days > 100000f) return null;
            if (!ResearchQueueMod.Settings.ShowCompletionDate) return "ResearchQueue_DaysEstimate".Translate(days.ToString("0.0"));
            Map? map = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            if (map == null) return "ResearchQueue_DaysEstimate".Translate(days.ToString("0.0"));
            long ticks = Find.TickManager.TicksAbs + (long)(days * GenDate.TicksPerDay);
            return "ResearchQueue_DateEstimate".Translate(GenDate.DateShortStringAt(ticks, Find.WorldGrid.LongLatOf(map.Tile)));
        }

        private string Tooltip(ResearchProjectDef project)
        {
            if (ResearchCatalog.Masked(project)) return ResearchCatalog.ProjectLabel(project);
            string text = ResearchCatalog.Label(project);
            if (!project.description.NullOrEmpty()) text += "\n\n" + project.description;
            ResearchQueueWorldComponent? tracker = ResearchQueueWorldComponent.Get();
            string? reason = tracker == null ? null : Reason(project, tracker);
            if (reason != null) text += "\n\n" + reason;
            if (unestimated.Contains(project))
                text += "\n\n" + "ResearchQueue_NoEstimateTip".Translate();
            return text;
        }

        private static string? Reason(ResearchProjectDef project, ResearchQueueWorldComponent tracker)
        {
            if (project.IsFinished) return null;
            if (tracker.IsExcluded(project))
            {
                if (tracker.IsSnoozed(project)) return "ResearchQueue_ExplicitSnooze".Translate();
                List<string> roots = tracker.SnoozedPrerequisites(project).ConvertAll(ResearchCatalog.Label);
                return "ResearchQueue_SnoozedPrerequisite".Translate(string.Join(", ", roots));
            }
            return ResearchRequirements.CanStart(project) || Find.ResearchManager.IsCurrentProject(project) ? null : ResearchRequirements.BlockReason(project);
        }

        // Current research sits in its own section, divided from the queue below; returns where the queue starts.
        private float DrawCurrent(Rect left, ResearchQueueWorldComponent tracker)
        {
            ColumnTitle(left, "ResearchQueue_Current".Translate());
            Rect row = new Rect(left.x, left.y + ColumnContentTop, left.width - ResearchCard.ScrollReserve, ResearchCard.Height);
            // Dev mode only, as vanilla's "Debug: Finish now": completes the current project; the queue then starts the next.
            if (Prefs.DevMode && currentRow != null)
            {
                Rect finish = new Rect(row.xMax - 140f, left.y + (ColumnTitleHeight - 28f) / 2f, 140f, 28f);
                if (Widgets.ButtonText(finish, "ResearchQueue_DevFinish".Translate())) Find.ResearchManager.FinishProject(currentRow.Project);
            }
            if (currentRow != null) DrawRow(row, currentRow, tracker);
            else
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = Color.gray;
                Widgets.Label(CardRect(row), "ResearchQueue_Idle".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            float divider = row.yMax + 14f;
            Widgets.DrawLineHorizontal(left.x, divider, left.width, DividerColor);
            float y = divider + 10f;
            string header = showingSnoozed ? "ResearchQueue_SnoozedHeader".Translate(snoozedCount)
                : queueTotal != null ? "ResearchQueue_QueueHeaderTotal".Translate(queuedCount, queueTotal) : "ResearchQueue_QueueHeader".Translate(queuedCount);
            Heading(new Rect(left.x, y, left.width, HeaderHeight), header);
            return y + HeaderHeight + 4f;
        }

        private static void Heading(Rect rect, string text)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect, text);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // Shared title at the top of each column; each column's content starts ColumnContentTop below it.
        private static void ColumnTitle(Rect column, string text)
        {
            Text.Font = GameFont.Medium;
            Text.WordWrap = false;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(column.x, column.y, column.width, ColumnTitleHeight), text.Truncate(column.width));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = true;
            Text.Font = GameFont.Small;
        }

        private void DrawQueue(Rect rect, ResearchQueueWorldComponent tracker)
        {
            Dictionary<ResearchSystem, Vector2> scrollSet = scrolls[showingSnoozed ? 1 : 0];
            Vector2 scroll = scrollSet.TryGetValue(System, out Vector2 saved) ? saved : Vector2.zero;
            Rect view = new Rect(0f, 0f, rect.width - ResearchCard.ScrollReserve, Mathf.Max(rect.height, rows.Count * RowHeight));
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
                {
                    GUI.color = Color.gray;
                    Widgets.Label(CardRect(new Rect(0f, 0f, view.width, 60f)), (showingSnoozed ? "ResearchQueue_NoSnoozed" : "ResearchQueue_NoQueued").Translate());
                    GUI.color = Color.white;
                }
                bool canReorder = !showingSnoozed && !search.filter.Active && rows.Count > 1;
                if (Event.current.type == EventType.Repaint)
                    reorderGroup = canReorder ? ReorderableWidget.NewGroup(onReordered, ReorderableDirection.Vertical, view, RowHeight - ResearchCard.Height) : -1;
                else if (!canReorder) reorderGroup = -1;
                int first = QueueRules.FirstVisibleRow(rows.Count, scroll.y, RowHeight);
                int end = QueueRules.EndVisibleRow(rows.Count, scroll.y, rect.height, RowHeight);
                for (int i = first; i < end; i++)
                    DrawRow(new Rect(0f, i * RowHeight, view.width, ResearchCard.Height), rows[i], tracker);
                // Every row registers on repaint, after the cards, so drop indexes match the list and drag highlights draw on top.
                if (reorderGroup >= 0 && Event.current.type == EventType.Repaint)
                    for (int i = 0; i < rows.Count; i++)
                        ReorderableWidget.Reorderable(reorderGroup, CardRect(new Rect(0f, i * RowHeight, view.width, ResearchCard.Height)));
                if (reorderGroup >= 0 && Event.current.type == EventType.Repaint && ReorderableWidget.Dragging &&
                    ReorderableWidget.GetDraggedIndex >= 0 && ReorderableWidget.GetDraggedFromGroupID == reorderGroup)
                    DrawDropLine(view.width);
            }
            finally { Widgets.EndScrollView(); }
            scrollSet[System] = scroll;
        }

        // Gold drop marker over the game's faint white one, at the same gap: above the nearest card's centre lands before it.
        private void DrawDropLine(float width)
        {
            float gap = RowHeight - ResearchCard.Height;
            float mouseY = Event.current.mousePosition.y;
            int index = Mathf.Clamp(Mathf.FloorToInt(mouseY / RowHeight), 0, rows.Count - 1);
            bool before = mouseY < index * RowHeight + ResearchCard.Height / 2f;
            float y = before ? index * RowHeight - gap / 2f : index * RowHeight + ResearchCard.Height + gap / 2f;
            Rect card = CardRect(new Rect(0f, 0f, width, 0f));
            Widgets.DrawBoxSolid(new Rect(card.x - 4f, y - 2f, card.width + 8f, 4f), DropLineColor);
        }

        private void OnReordered(int from, int boundary)
        {
            ResearchQueueWorldComponent? tracker = ResearchQueueWorldComponent.Get();
            if (tracker == null || state == null) return;
            tracker.MoveVisible(state, rowIds, from, boundary);
        }

        // Every card, current research included, starts after the position column so they line up.
        private static Rect CardRect(Rect row) => new Rect(row.x + NumberWidth, row.y, Mathf.Max(260f, row.width - NumberWidth), row.height);

        private void DrawRow(Rect rect, QueueRow row, ResearchQueueWorldComponent tracker)
        {
            Rect card = CardRect(rect);
            if (row.Position > 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(rect.x, rect.y, NumberWidth, rect.height), row.Position.ToString());
                Text.Anchor = TextAnchor.UpperLeft;
            }
            if (row.Position > 0 && reorderGroup >= 0 && Event.current.type != EventType.Repaint) ReorderableWidget.Reorderable(reorderGroup, card);
            ResearchCard.Draw(card, row, row.Project == selectedProject);

            if (row.Hidden || Event.current.type != EventType.MouseDown || !Mouse.IsOver(card)) return;
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
                {
                    options.Add(new FloatMenuOption("ResearchQueue_MoveToTop".Translate(), row.Position > 1 && !row.Blocked ? () => tracker.MoveToFront(project) : (Action?)null));
                    options.Add(new FloatMenuOption("ResearchQueue_ResearchNow".Translate(), !row.Blocked ? () => tracker.ResearchNow(project) : (Action?)null));
                }
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void ConfirmSnooze(ResearchProjectDef project, ResearchQueueWorldComponent tracker)
        {
            List<ResearchProjectDef> dependents = tracker.BlockedDependents(project);
            if (dependents.Count == 0 || !ResearchQueueMod.Settings.ConfirmDependentSnooze)
            {
                tracker.Snooze(project);
                return;
            }
            Find.WindowStack.Add(new Dialog_ConfirmSnooze(project, dependents, () => tracker.Snooze(project)));
        }
    }
}

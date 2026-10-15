using System;
using System.Collections.Generic;
using System.Linq;
using ResearchQueue.Core;
using ResearchQueue.State;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ResearchQueue.UI
{
    public sealed class MainTabWindow_ResearchQueue : MainTabWindow
    {
        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private readonly Dictionary<string, Vector2> scrollBySystem = new Dictionary<string, Vector2>();
        private Vector2 detailsScroll;
        private string selectedSystem = ResearchSystems.Standard;
        private string? selectedId;
        private bool showingSnoozed;
        private int reorderGroup = -1;
        private int cachedRevision = -1;
        private string cachedSystem = string.Empty;
        private Dictionary<string, float> estimates = new Dictionary<string, float>();
        private int viewRevision = -1;
        private string viewSystem = string.Empty;
        private bool viewSnoozed;
        private List<QueueViewEntry> viewEntries = new List<QueueViewEntry>();
        private string[] viewIds = Array.Empty<string>();
        private HashSet<string> newIds = new HashSet<string>();
        private ResearchProjectDef? viewActive;
        public int LastDrawnRowCount { get; private set; }
        public int ViewBuildCount { get; private set; }

        protected override float Margin => 12f;
        public override Vector2 InitialSize => new Vector2(Verse.UI.screenWidth, Verse.UI.screenHeight - 35f);
        public override Vector2 RequestedTabSize => InitialSize;

        public MainTabWindow_ResearchQueue()
        {
            def = MainButtonDefOf.Research;
            doCloseX = false;
            doCloseButton = false;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            showingSnoozed = false;
            reorderGroup = -1;
            cachedRevision = -1;
            viewRevision = -1;
        }

        public override void PostClose()
        {
            ResearchQueueWorldComponent.Get()?.ClearNewMarkers();
            base.PostClose();
        }

        public override void DoWindowContents(Rect canvas)
        {
            Text.Font = GameFont.Small;
            ResearchQueueWorldComponent? tracker = ResearchQueueWorldComponent.Get();
            if (tracker == null) return;
            if (tracker.Queues.Count == 0) tracker.RefreshQueues();
            float y = DrawSystemTabs(canvas, tracker);
            DrawToolbar(new Rect(0f, y, canvas.width, 32f));
            RefreshView(tracker);
            if (cachedRevision != tracker.Revision || cachedSystem != selectedSystem)
            {
                estimates = tracker.Estimates(selectedSystem);
                cachedRevision = tracker.Revision;
                cachedSystem = selectedSystem;
            }
            y += 44f;
            float detailsWidth = Mathf.Clamp(canvas.width * 0.27f, 270f, 430f);
            float queueWidth = canvas.width - detailsWidth - 20f;
            ResearchProjectDef? active = viewActive;
            Widgets.Label(new Rect(0f, y, queueWidth, 24f), "ResearchQueue_Current".Translate());
            y += 26f;
            if (active != null)
            {
                DrawProject(new Rect(0f, y, queueWidth, 48f), active, tracker, null, false, false);
                if (selectedId == null) selectedId = active.defName;
            }
            else Widgets.Label(new Rect(0f, y, queueWidth, 48f), "ResearchQueue_Idle".Translate());
            y += 60f;
            DrawQueue(new Rect(0f, y, queueWidth, Mathf.Max(0f, canvas.height - y)), tracker);
            DrawDetails(new Rect(queueWidth + 20f, y - 86f, detailsWidth, canvas.height - y + 86f), tracker);
        }

        private float DrawSystemTabs(Rect canvas, ResearchQueueWorldComponent tracker)
        {
            float x = 0f;
            float y = 0f;
            foreach (ResearchSystemQueueState queue in tracker.Queues)
            {
                string label = ResearchSystems.Label(queue.SystemId);
                float width = Text.CalcSize(label).x + 30f;
                if (x + width > canvas.width && x > 0f) { x = 0f; y += 36f; }
                Rect rect = new Rect(x, y, width, 30f);
                if (queue.SystemId == selectedSystem) Widgets.DrawHighlightSelected(rect);
                if (Widgets.ButtonText(rect, label))
                {
                    selectedSystem = queue.SystemId;
                    selectedId = null;
                    detailsScroll = Vector2.zero;
                    reorderGroup = -1;
                }
                x += width + 6f;
            }
            return y + 42f;
        }

        private void DrawToolbar(Rect rect)
        {
            search.OnGUI(new Rect(rect.x, rect.y, Mathf.Min(rect.width * 0.42f, 420f), 30f),
                () => viewRevision = -1, () => viewRevision = -1);
            float x = Mathf.Min(rect.width * 0.42f, 420f) + 12f;
            if (Widgets.ButtonText(new Rect(x, rect.y, 110f, 30f), "ResearchQueue_Queue".Translate()))
            { showingSnoozed = false; reorderGroup = -1; }
            if (Widgets.ButtonText(new Rect(x + 118f, rect.y, 130f, 30f), "ResearchQueue_Snoozed".Translate()))
            { showingSnoozed = true; reorderGroup = -1; }
            ResearchQueueSettings settings = ResearchQueueMod.Instance!.Settings;
            Rect format = new Rect(rect.xMax - 220f, rect.y, 220f, 30f);
            if (Widgets.ButtonText(format, (settings.ShowCompletionDate ? "ResearchQueue_Date" : "ResearchQueue_Days").Translate()))
            {
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("ResearchQueue_Days".Translate(), () => SetDate(false)),
                    new FloatMenuOption("ResearchQueue_Date".Translate(), () => SetDate(true))
                }));
            }
        }

        private static void SetDate(bool date)
        {
            ResearchQueueMod.Instance!.Settings.ShowCompletionDate = date;
            ResearchQueueMod.Instance.WriteSettings();
        }

        private void RefreshView(ResearchQueueWorldComponent tracker)
        {
            if (viewRevision == tracker.Revision && viewSystem == selectedSystem && viewSnoozed == showingSnoozed) return;
            viewEntries = new List<QueueViewEntry>();
            ResearchSystemQueueState queue = tracker.Queue(selectedSystem);
            if (showingSnoozed)
            {
                foreach (ResearchProjectDef project in tracker.Snoozed(selectedSystem))
                    if (!search.filter.Active || search.filter.Matches(ResearchCard.Label(project)))
                        viewEntries.Add(new QueueViewEntry(project, null));
            }
            else
            {
                for (int i = 0; i < queue.ProjectIds.Count; i++)
                {
                    ResearchProjectDef? project = tracker.Resolve(queue.ProjectIds[i]);
                    if (project != null && (!search.filter.Active || search.filter.Matches(ResearchCard.Label(project))))
                        viewEntries.Add(new QueueViewEntry(project, i + 1));
                }
            }
            viewIds = viewEntries.Select(e => e.Project.defName).ToArray();
            newIds = new HashSet<string>(queue.NewProjectIds);
            viewActive = tracker.Active(selectedSystem).FirstOrDefault();
            viewSystem = selectedSystem;
            viewSnoozed = showingSnoozed;
            viewRevision = tracker.Revision;
            ViewBuildCount++;
        }

        private static Rect CardRect(Rect row, bool numbered) => new Rect(row.x + (numbered ? 44f : 0f), row.y,
            Mathf.Max(150f, row.width - (numbered ? 44f : 0f) - 185f), row.height);

        private void DrawQueue(Rect rect, ResearchQueueWorldComponent tracker)
        {
            bool filtered = search.filter.Active;
            Vector2 scroll = scrollBySystem.TryGetValue(selectedSystem + showingSnoozed, out Vector2 value) ? value : Vector2.zero;
            Rect view = new Rect(0f, 0f, rect.width - 18f, Mathf.Max(rect.height, viewEntries.Count * 54f));
            Widgets.BeginScrollView(rect, ref scroll, view);
            try
            {
                if (viewEntries.Count == 0) Widgets.Label(new Rect(0f, 0f, view.width, 60f),
                    (showingSnoozed ? "ResearchQueue_NoSnoozed" : "ResearchQueue_NoQueued").Translate());
                if (!showingSnoozed && !filtered && Event.current.type == EventType.Repaint)
                {
                    string[] ids = viewIds;
                    int revision = tracker.Revision;
                    reorderGroup = ReorderableWidget.NewGroup((from, boundary) =>
                    {
                        if (tracker.Revision == revision) tracker.MoveVisible(ids, from, boundary);
                    }, ReorderableDirection.Vertical, Rect.zero, 12f);
                }
                if (showingSnoozed || filtered) reorderGroup = -1;
                int first = QueueRules.FirstVisibleRow(viewEntries.Count, scroll.y, 54f);
                int end = QueueRules.EndVisibleRow(viewEntries.Count, scroll.y, rect.height, 54f);
                LastDrawnRowCount = 0;
                bool registerAll = reorderGroup >= 0 && Event.current.type == EventType.Repaint;
                for (int i = registerAll ? 0 : first; i < (registerAll ? viewEntries.Count : end); i++)
                {
                    QueueViewEntry entry = viewEntries[i];
                    Rect row = new Rect(0f, i * 54f, view.width, ResearchCard.Height);
                    if (i >= first && i < end)
                    {
                        DrawProject(row, entry.Project, tracker, entry.Position, showingSnoozed,
                            reorderGroup >= 0 && !registerAll);
                        LastDrawnRowCount++;
                    }
                    // Register every row after its card so native drag highlights remain visible.
                    if (registerAll) ReorderableWidget.Reorderable(reorderGroup, CardRect(row, true));
                }
            }
            finally { Widgets.EndScrollView(); }
            scrollBySystem[selectedSystem + showingSnoozed] = scroll;
        }

        private void DrawProject(Rect row, ResearchProjectDef project, ResearchQueueWorldComponent tracker,
            int? position, bool snoozed, bool reorder)
        {
            bool blocked = !snoozed && !ResearchSystems.CanStart(project);
            float indexWidth = position.HasValue ? 44f : 0f;
            float etaWidth = 185f;
            Rect card = CardRect(row, position.HasValue);
            Text.Anchor = TextAnchor.MiddleLeft;
            if (position.HasValue) Widgets.Label(new Rect(row.x, row.y, indexWidth, row.height), position.Value.ToString());
            float costWidth = Mathf.Max(65f, Text.CalcSize(ResearchCard.Cost(project)).x + 12f);
            ResearchCard.Draw(card, project, selectedId == project.defName, blocked, costWidth);
            if (reorder) ReorderableWidget.Reorderable(reorderGroup, card);
            if (Event.current.type == EventType.MouseDown && Mouse.IsOver(card))
            {
                if (Event.current.button == 0) { selectedId = project.defName; detailsScroll = Vector2.zero; Event.current.Use(); }
                else if (Event.current.button == 1)
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    if (snoozed)
                    {
                        bool explicitSnooze = tracker.Queue(selectedSystem).SnoozedProjectIds.Contains(project.defName);
                        options.Add(new FloatMenuOption("ResearchQueue_Restore".Translate(), explicitSnooze ? () => tracker.Restore(project) : (Action?)null));
                    }
                    else
                    {
                        options.Add(new FloatMenuOption("ResearchQueue_Snooze".Translate(), () => ConfirmSnooze(project, tracker)));
                        if (position.HasValue) options.Add(new FloatMenuOption("ResearchQueue_Next".Translate(), !blocked ? () => tracker.Move(project, 0) : (Action?)null));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                    Event.current.Use();
                }
            }
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect estimate = new Rect(card.xMax + 12f, row.y, etaWidth - 12f, row.height);
            if (snoozed) Widgets.Label(estimate, "ResearchQueue_Snoozed".Translate());
            else if (blocked)
            {
                Widgets.Label(estimate, "ResearchQueue_Blocked".Translate());
                TooltipHandler.TipRegion(estimate, ResearchSystems.BlockReason(project));
            }
            else
            {
                bool isNew = newIds.Contains(project.defName);
                string eta = Estimate(project);
                if (isNew)
                {
                    Widgets.Label(new Rect(estimate.x, estimate.y, estimate.width, 24f), "ResearchQueue_New".Translate());
                    Widgets.Label(new Rect(estimate.x, estimate.y + 24f, estimate.width, 24f), eta);
                }
                else Widgets.Label(estimate, eta);
            }
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private string Estimate(ResearchProjectDef project)
        {
            if (!estimates.TryGetValue(project.defName, out float days) || days < 0f || float.IsInfinity(days) || float.IsNaN(days))
                return "ResearchQueue_Unknown".Translate();
            if (!ResearchQueueMod.Instance!.Settings.ShowCompletionDate)
                return "ResearchQueue_DaysEstimate".Translate(days.ToString("0.0"));
            if (days * GenDate.TicksPerDay > long.MaxValue - Find.TickManager.TicksAbs) return "ResearchQueue_Unknown".Translate();
            long ticks = Find.TickManager.TicksAbs + (long)(days * GenDate.TicksPerDay);
            Map? map = Find.CurrentMap ?? Find.Maps.FirstOrDefault();
            if (map == null) return "ResearchQueue_Unknown".Translate();
            Vector2 location = Find.WorldGrid.LongLatOf(map.Tile);
            return "~" + GenDate.DateFullStringAt(ticks, location);
        }

        private void DrawDetails(Rect rect, ResearchQueueWorldComponent tracker)
        {
            ResearchProjectDef? project = selectedId != null ? tracker.Resolve(selectedId) : null;
            if (project == null) return;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 40f), ResearchCard.Label(project));
            Text.Font = GameFont.Small;
            Rect actions = new Rect(rect.x, rect.y + 48f, rect.width, 80f);
            DrawActions(actions, project, tracker);
            rect.y += 140f;
            rect.height = Mathf.Max(0f, rect.height - 140f);
            Rect view = new Rect(0f, 0f, rect.width - 18f, DetailsHeight(project, rect.width - 18f));
            Widgets.BeginScrollView(rect, ref detailsScroll, view);
            try
            {
                float y = 0f;
                string description = project.description ?? string.Empty;
                float descriptionHeight = Text.CalcHeight(description, view.width);
                Widgets.Label(new Rect(0f, y, view.width, descriptionHeight), description);
                y += descriptionHeight + 14f;
                Widgets.Label(new Rect(0f, y, view.width, 28f), "ResearchQueue_Cost".Translate(ResearchCard.Cost(project))); y += 32f;
                Widgets.Label(new Rect(0f, y, view.width, 48f), "ResearchQueue_UnlockEstimate".Translate(Estimate(project))); y += 52f;
                bool snoozed = tracker.IsExcluded(project.defName);
                string? reason = snoozed ? tracker.SnoozeReason(project) : !ResearchSystems.CanStart(project) ? ResearchSystems.BlockReason(project) : null;
                if (reason != null)
                {
                    float h = Text.CalcHeight(reason, view.width);
                    Widgets.Label(new Rect(0f, y, view.width, h), reason); y += h + 12f;
                }
                foreach (ResearchProjectDef prerequisite in ResearchSystems.Prerequisites(project))
                {
                    GUI.color = prerequisite.IsFinished ? Color.green : Color.white;
                    Widgets.Label(new Rect(0f, y, view.width, 28f), "ResearchQueue_Requires".Translate(ResearchCard.Label(prerequisite)));
                    GUI.color = Color.white; y += 32f;
                }
                foreach (Def unlock in project.UnlockedDefs)
                {
                    Widgets.DefIcon(new Rect(0f, y, 28f, 28f), unlock);
                    Widgets.Label(new Rect(36f, y, view.width - 36f, 30f), ResearchCard.Label(unlock));
                    TooltipHandler.TipRegion(new Rect(0f, y, view.width, 30f), unlock.description ?? ResearchCard.Label(unlock));
                    y += 34f;
                }
            }
            finally { GUI.color = Color.white; Widgets.EndScrollView(); }
        }

        private void DrawActions(Rect rect, ResearchProjectDef project, ResearchQueueWorldComponent tracker)
        {
                bool snoozed = tracker.IsExcluded(project.defName);
                if (snoozed)
                {
                    bool explicitSnooze = tracker.Queue(ResearchSystems.Key(project)).SnoozedProjectIds.Contains(project.defName);
                    ActionButton(new Rect(rect.x, rect.y, rect.width, 32f), "ResearchQueue_Restore".Translate(), explicitSnooze,
                        tracker.SnoozeReason(project), () => tracker.Restore(project));
                }
                else
                {
                    ActionButton(new Rect(rect.x, rect.y, rect.width, 32f), "ResearchQueue_Snooze".Translate(), !project.IsFinished,
                        "ResearchQueue_Completed".Translate(), () => ConfirmSnooze(project, tracker));
                    bool canMove = ResearchSystems.CanStart(project) && tracker.Queue(selectedSystem).ProjectIds.Contains(project.defName);
                    ActionButton(new Rect(rect.x, rect.y + 40f, rect.width, 32f), "ResearchQueue_Next".Translate(), canMove,
                        Find.ResearchManager.IsCurrentProject(project) ? "InProgress".Translate().ToString() : ResearchSystems.BlockReason(project),
                        () => tracker.Move(project, 0));
                }
        }

        private static float DetailsHeight(ResearchProjectDef project, float width) => 360f +
            Text.CalcHeight(project.description ?? string.Empty, width) + project.UnlockedDefs.Count * 34f +
            ResearchSystems.Prerequisites(project).Count() * 32f + 160f;

        private static void ActionButton(Rect rect, string label, bool enabled, string reason, Action action)
        {
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && enabled;
            if (Widgets.ButtonText(rect, label) && enabled) action();
            GUI.enabled = oldEnabled;
            if (!enabled) TooltipHandler.TipRegion(rect, reason);
        }

        private static void ConfirmSnooze(ResearchProjectDef project, ResearchQueueWorldComponent tracker)
        {
            List<ResearchProjectDef> dependents = tracker.AffectedDependents(project);
            if (dependents.Count > 0 && ResearchQueueMod.Instance!.Settings.ConfirmDependentSnooze)
            {
                string message = "ResearchQueue_ConfirmSnooze".Translate(ResearchCard.Label(project)) + "\n\n" +
                    string.Join("\n", dependents.Select(p => "- " + ResearchCard.Label(p)));
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(message, () => tracker.Snooze(project)));
            }
            else tracker.Snooze(project);
        }
    }
}

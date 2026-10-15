using System;
using System.Collections.Generic;
using System.Linq;
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
            if (cachedRevision != tracker.Revision || cachedSystem != selectedSystem)
            {
                estimates = tracker.Estimates(selectedSystem);
                cachedRevision = tracker.Revision;
                cachedSystem = selectedSystem;
            }
            float y = DrawSystemTabs(canvas, tracker);
            DrawToolbar(new Rect(0f, y, canvas.width, 32f));
            y += 44f;
            float detailsWidth = Mathf.Clamp(canvas.width * 0.27f, 270f, 430f);
            float queueWidth = canvas.width - detailsWidth - 20f;
            ResearchProjectDef? active = tracker.Active(selectedSystem).FirstOrDefault();
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
            search.OnGUI(new Rect(rect.x, rect.y, Mathf.Min(rect.width * 0.42f, 420f), 30f));
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

        private void DrawQueue(Rect rect, ResearchQueueWorldComponent tracker)
        {
            ResearchSystemQueueState queue = tracker.Queue(selectedSystem);
            ResearchProjectDef[] projects = showingSnoozed ? tracker.Snoozed(selectedSystem).ToArray() :
                queue.ProjectIds.Select(tracker.Resolve).Where(p => p != null).Cast<ResearchProjectDef>().ToArray();
            bool filtered = search.filter.Active;
            Vector2 scroll = scrollBySystem.TryGetValue(selectedSystem + showingSnoozed, out Vector2 value) ? value : Vector2.zero;
            ResearchProjectDef[] visible = projects.Where(p => !filtered || search.filter.Matches(ResearchCard.Label(p))).ToArray();
            Rect view = new Rect(0f, 0f, rect.width - 18f, Mathf.Max(rect.height, visible.Length * 54f));
            Widgets.BeginScrollView(rect, ref scroll, view);
            try
            {
                if (visible.Length == 0) Widgets.Label(new Rect(0f, 0f, view.width, 60f),
                    (showingSnoozed ? "ResearchQueue_NoSnoozed" : "ResearchQueue_NoQueued").Translate());
                if (!showingSnoozed && !filtered && Event.current.type == EventType.Repaint)
                {
                    reorderGroup = ReorderableWidget.NewGroup((from, to) =>
                    {
                        if (from < 0 || from >= projects.Length || to < 0 || to > projects.Length) return;
                        int target = to > from ? to - 1 : to;
                        tracker.Move(projects[from], target);
                    }, ReorderableDirection.Vertical, Rect.zero, 12f);
                }
                if (showingSnoozed || filtered) reorderGroup = -1;
                for (int i = 0; i < visible.Length; i++)
                {
                    ResearchProjectDef project = visible[i];
                    int? position = showingSnoozed ? (int?)null : queue.ProjectIds.IndexOf(project.defName) + 1;
                    Rect row = new Rect(0f, i * 54f, view.width, ResearchCard.Height);
                    DrawProject(row, project, tracker, position, showingSnoozed, reorderGroup >= 0);
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
            Rect card = new Rect(row.x + indexWidth, row.y, Mathf.Max(150f, row.width - indexWidth - etaWidth), row.height);
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
                bool isNew = tracker.Queue(ResearchSystems.Key(project)).NewProjectIds.Contains(project.defName);
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
            Rect view = new Rect(0f, 0f, rect.width - 18f, DetailsHeight(project, rect.width - 18f));
            Widgets.BeginScrollView(rect, ref detailsScroll, view);
            try
            {
                float y = 0f;
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0f, y, view.width, 40f), ResearchCard.Label(project));
                Text.Font = GameFont.Small;
                y += 48f;
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
                y += 12f;
                if (snoozed)
                {
                    bool explicitSnooze = tracker.Queue(ResearchSystems.Key(project)).SnoozedProjectIds.Contains(project.defName);
                    ActionButton(new Rect(0f, y, view.width, 32f), "ResearchQueue_Restore".Translate(), explicitSnooze,
                        tracker.SnoozeReason(project), () => tracker.Restore(project));
                }
                else
                {
                    if (Widgets.ButtonText(new Rect(0f, y, view.width, 32f), "ResearchQueue_Snooze".Translate())) ConfirmSnooze(project, tracker);
                    y += 40f;
                    bool canMove = ResearchSystems.CanStart(project) && tracker.Queue(selectedSystem).ProjectIds.Contains(project.defName);
                    ActionButton(new Rect(0f, y, view.width, 32f), "ResearchQueue_Next".Translate(), canMove,
                        ResearchSystems.BlockReason(project), () => tracker.Move(project, 0));
                }
            }
            finally { GUI.color = Color.white; Widgets.EndScrollView(); }
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

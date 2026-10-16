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
    // The selected project's panel: title and actions stay fixed above a scrolling description, requirements and unlocks.
    public sealed partial class MainTabWindow_ResearchQueue
    {

        private readonly List<ResearchProjectDef> detailsLeadsTo = new List<ResearchProjectDef>();
        private Vector2 detailsScroll;
        private ResearchProjectDef? detailsProject;
        private int detailsRevision = -1;
        private int detailsBucket = -1;
        private string? detailsReason;
        private bool detailsCanStart;
        private float detailsHeight;

        private void DrawDetails(Rect rect, ResearchQueueWorldComponent tracker)
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
                detailsLeadsTo.Clear();
                foreach (ResearchProjectDef dependent in ResearchCatalog.Dependents(project))
                    if (!dependent.IsFinished) detailsLeadsTo.Add(dependent);
                detailsLeadsTo.Sort(ResearchCard.ByEra);
            }

            Text.Font = GameFont.Medium;
            Text.WordWrap = false;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 36f), ResearchCard.Label(project).Truncate(rect.width));
            Text.WordWrap = true;
            Text.Font = GameFont.Small;
            DrawActions(new Rect(rect.x, rect.y + 42f, rect.width, 32f), project, tracker);

            Rect outRect = new Rect(rect.x, rect.y + 86f, rect.width, rect.height - 86f);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(detailsHeight, outRect.height));
            Widgets.BeginScrollView(outRect, ref detailsScroll, view);
            try
            {
                float y = 0f;
                if (!project.description.NullOrEmpty()) y = Paragraph(view.width, y, project.description);
                y = Paragraph(view.width, y, "ResearchQueue_Cost".Translate(ResearchCard.Cost(project)));
                if (estimates.TryGetValue(project, out string? estimate))
                    y = Paragraph(view.width, y, "ResearchQueue_UnlockEstimate".Translate(estimate));
                else if (unestimated.Contains(project))
                    y = Paragraph(view.width, y, "ResearchQueue_UnlockEstimate".Translate("ResearchQueue_NoEstimate".Translate()) + "\n" + "ResearchQueue_NoEstimateTip".Translate());
                if (detailsReason != null)
                {
                    GUI.color = ColorLibrary.RedReadable;
                    y = Paragraph(view.width, y, detailsReason);
                    GUI.color = Color.white;
                }
                y = DrawLeadsTo(view.width, y, project);
                y = DrawUnlocks(view.width, y, project);
                detailsHeight = y;
            }
            finally
            {
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.EndScrollView();
            }
        }

        private void DrawActions(Rect rect, ResearchProjectDef project, ResearchQueueWorldComponent tracker)
        {
            if (tracker.IsExcluded(project) && !project.IsFinished)
            {
                ActionButton(rect, "ResearchQueue_Restore".Translate(), tracker.IsSnoozed(project), detailsReason, () => tracker.Restore(project));
                return;
            }
            Rect snooze = new Rect(rect.x, rect.y, (rect.width - 8f) / 2f, rect.height);
            Rect next = new Rect(snooze.xMax + 8f, rect.y, snooze.width, rect.height);
            ActionButton(snooze, "ResearchQueue_Snooze".Translate(), !project.IsFinished, "ResearchQueue_Completed".Translate(),
                () => ConfirmSnooze(project, tracker));
            int index = tracker.StateOf(project)?.Queue.IndexOf(project.defName) ?? -1;
            string? nextReason = null;
            if (project.IsFinished) nextReason = "ResearchQueue_Completed".Translate();
            else if (Find.ResearchManager.IsCurrentProject(project)) nextReason = "InProgress".Translate();
            else if (index == 0) nextReason = "ResearchQueue_AlreadyNext".Translate();
            else if (index < 0 || !detailsCanStart) nextReason = detailsReason ?? "ResearchQueue_OtherRequirements".Translate();
            ActionButton(next, "ResearchQueue_Next".Translate(), nextReason == null, nextReason, () => tracker.MoveToFront(project));
        }

        private static void ActionButton(Rect rect, string label, bool enabled, string? reason, Action action)
        {
            if (Widgets.ButtonText(rect, label, active: enabled) && enabled) action();
            if (!enabled && reason != null) TooltipHandler.TipRegion(rect, reason);
        }

        private static float Paragraph(float width, float y, string text)
        {
            float height = Text.CalcHeight(text, width);
            Widgets.Label(new Rect(0f, y, width, height), text);
            return y + height + 10f;
        }

        // Unfinished projects this one is a prerequisite for, as small cards; clicking one selects it, switching queue or view as needed.
        private float DrawLeadsTo(float width, float y, ResearchProjectDef selected)
        {
            if (detailsLeadsTo.Count == 0) return y;
            y = Heading(width, y, "ResearchQueue_LeadsTo".Translate());
            for (int i = 0; i < detailsLeadsTo.Count; i++)
            {
                ResearchProjectDef project = detailsLeadsTo[i];
                if (i > 0) y += ResearchCard.GapBefore(detailsLeadsTo, i);
                Rect row = new Rect(0f, y, width, ResearchCard.Height);
                ResearchCard.DrawCompact(row, project, true);
                TooltipHandler.TipRegion(row, new TipSignal(() => OtherPrerequisites(project, selected), project.shortHash ^ 0x5a17));
                if (Widgets.ButtonInvisible(row, false))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    selectedProject = project;
                }
                y += row.height;
            }
            return y + 12f;
        }

        private static string OtherPrerequisites(ResearchProjectDef dependent, ResearchProjectDef shown)
        {
            List<string> others = new List<string>();
            foreach (ResearchProjectDef prerequisite in ResearchCatalog.Prerequisites(dependent))
                if (prerequisite != shown && !prerequisite.IsFinished) others.Add(ResearchCard.Label(prerequisite));
            return others.Count == 0 ? "ResearchQueue_LastPrerequisite".Translate().ToString() : "ResearchQueue_AlsoRequires".Translate(string.Join(", ", others));
        }

        private static float DrawUnlocks(float width, float y, ResearchProjectDef project)
        {
            if (project.UnlockedDefs.Count == 0) return y;
            y = Heading(width, y, "ResearchQueue_Unlocks".Translate());
            foreach (Def unlock in project.UnlockedDefs)
            {
                Rect row = new Rect(0f, y, width, 30f);
                Widgets.DrawHighlightIfMouseover(row);
                Widgets.DefIcon(new Rect(row.x + 2f, row.y + 2f, 26f, 26f), unlock);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(row.x + 36f, row.y, row.width - 36f, row.height), ResearchCard.Label(unlock));
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

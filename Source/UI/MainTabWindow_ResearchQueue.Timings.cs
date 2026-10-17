using System.Collections.Generic;
using ResearchQueue.Core;
using ResearchQueue.Research;
using ResearchQueue.State;
using RimWorld;
using UnityEngine;
using Verse;

namespace ResearchQueue.UI
{
    // The timings panel above the details: research speed, current project, queue and all remaining research.
    public sealed partial class MainTabWindow_ResearchQueue
    {
        private const float TimingsWideWidth = 560f;

        private readonly List<TimingStat> timings = new List<TimingStat>();
        private float researchRate;

        // One tile; Primary tiles stay when the panel is too narrow for all of them.
        private readonly struct TimingStat
        {
            public readonly string Label;
            public readonly string Value;
            public readonly string Tip;
            public readonly bool Primary;

            public TimingStat(string label, string value, string tip, bool primary)
            {
                Label = label;
                Value = value;
                Tip = label.Colorize(ColoredText.TipSectionTitleColor) + "\n\n" + tip;
                Primary = primary;
            }
        }

        private void BuildTimings(ResearchQueueWorldComponent tracker, ResearchProjectDef? current, float currentWork, float queueWork, int blockedCount)
        {
            timings.Clear();
            string noEstimate = "ResearchQueue_NoEstimate".Translate();
            string noEstimateTip = "ResearchQueue_NoEstimateTip".Translate();
            bool measured = researchRate > 0f;

            timings.Add(new TimingStat("ResearchQueue_TimingRate".Translate(),
                measured ? "ResearchQueue_TimingRateValue".Translate(researchRate.ToString("N0")).ToString() : noEstimate,
                measured ? "ResearchQueue_TimingRateTip".Translate() : noEstimateTip, false));

            string currentTip = current == null ? "ResearchQueue_TimingCurrentIdleTip".Translate().ToString()
                : "ResearchQueue_TimingCurrentTip".Translate(ResearchCatalog.ProjectLabel(current), currentWork.ToString("N0")) + DateLine(currentWork);
            timings.Add(new TimingStat("ResearchQueue_TimingCurrent".Translate(),
                current == null ? "ResearchQueue_TimingIdle".Translate().ToString() : TimingValue(currentWork) ?? noEstimate,
                measured || current == null ? currentTip : noEstimateTip, false));

            string queueTip = "ResearchQueue_TimingQueueTip".Translate(queueWork.ToString("N0")) + DateLine(queueWork);
            if (blockedCount > 0) queueTip += "\n\n" + "ResearchQueue_TimingQueueBlocked".Translate(blockedCount);
            timings.Add(new TimingStat("ResearchQueue_TimingQueue".Translate(), TimingValue(queueWork) ?? noEstimate,
                measured ? queueTip : noEstimateTip, true));

            // Every unfinished project the difficulty allows, available or not, except snoozed ones and what they block.
            float treeWork = 0f;
            int treeCount = 0;
            foreach (ResearchProjectDef project in System.Projects)
            {
                if (project.IsFinished || !ResearchCatalog.Allowed(project) || tracker.IsExcluded(project)) continue;
                treeWork += Remaining(project);
                treeCount++;
            }
            string treeTip = "ResearchQueue_TimingTreeTip".Translate(treeCount, System.Label, treeWork.ToString("N0")) + DateLine(treeWork);
            timings.Add(new TimingStat("ResearchQueue_TimingTree".Translate(), TimingValue(treeWork) ?? noEstimate,
                measured ? treeTip : noEstimateTip, true));
        }

        // Time left as a period, or as a date when the completion date setting is on; null without a rate.
        private string? TimingValue(float work)
        {
            float days = QueueRules.CompletionDays(work, researchRate);
            if (days < 0f || days > 30000f) return null;
            if (ResearchQueueMod.Settings.ShowCompletionDate && DateAfter(days) is string date) return "ResearchQueue_DateEstimate".Translate(date);
            return "ResearchQueue_DateEstimate".Translate(((int)(days * GenDate.TicksPerDay)).ToStringTicksToPeriod(allowSeconds: false));
        }

        private string DateLine(float work)
        {
            float days = QueueRules.CompletionDays(work, researchRate);
            string? date = days < 0f || days > 30000f ? null : DateAfter(days);
            return date == null ? string.Empty : "\n\n" + "ResearchQueue_TimingFinishes".Translate(date);
        }

        private static string? DateAfter(float days)
        {
            Map? map = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            if (map == null) return null;
            long ticks = Find.TickManager.TicksAbs + (long)(days * GenDate.TicksPerDay);
            return GenDate.DateShortStringAt(ticks, Find.WorldGrid.LongLatOf(map.Tile));
        }

        // Equal tiles split by dividers, each a gray label over its value, the pair centered in the tile.
        private void DrawTimings(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(PanelPadding, 6f);
            bool wide = inner.width >= TimingsWideWidth;
            int count = 0;
            foreach (TimingStat stat in timings)
                if (wide || stat.Primary) count++;
            if (count == 0) return;
            float width = inner.width / count;
            float x = inner.x;
            const float labelHeight = 22f, valueHeight = 32f;
            float top = inner.center.y - (labelHeight + valueHeight) / 2f;
            Text.WordWrap = false;
            foreach (TimingStat stat in timings)
            {
                if (!wide && !stat.Primary) continue;
                Rect tile = new Rect(x, inner.y, width, inner.height);
                if (x > inner.x) Widgets.DrawLine(new Vector2(x, inner.y + 6f), new Vector2(x, inner.yMax - 6f), DividerColor, 1f);
                x += width;
                Widgets.DrawHighlightIfMouseover(tile);
                Rect text = tile.ContractedBy(12f, 0f);
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(text.x, top, text.width, labelHeight), stat.Label.Truncate(text.width));
                Text.Font = GameFont.Medium;
                GUI.color = Color.white;
                Widgets.Label(new Rect(text.x, top + labelHeight, text.width, valueHeight), stat.Value.Truncate(text.width));
                Text.Font = GameFont.Small;
                TooltipHandler.TipRegion(tile, stat.Tip);
            }
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = true;
        }
    }
}

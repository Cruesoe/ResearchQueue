using System;
using System.Collections.Generic;
using System.Linq;

namespace ResearchQueue.Core
{
    public sealed class QueueCandidate
    {
        public string Id { get; }
        public int Era { get; }
        public float Cost { get; }

        public QueueCandidate(string id, int era, float cost)
        {
            Id = id;
            Era = era;
            Cost = cost;
        }
    }

    public static class QueueRules
    {
        public static List<string> AppendAvailable(List<string> queue, IEnumerable<QueueCandidate> candidates,
            ISet<string> excluded, Random random)
        {
            HashSet<string> present = new HashSet<string>(queue);
            List<QueueCandidate> batch = candidates.Where(c => !excluded.Contains(c.Id) && present.Add(c.Id))
                .OrderBy(c => c.Era).ThenBy(c => c.Cost).ToList();
            int start = 0;
            while (start < batch.Count)
            {
                int end = start + 1;
                while (end < batch.Count && batch[end].Era == batch[start].Era && batch[end].Cost == batch[start].Cost)
                    end++;
                for (int i = end - 1; i > start; i--)
                {
                    int j = random.Next(start, i + 1);
                    QueueCandidate swap = batch[i];
                    batch[i] = batch[j];
                    batch[j] = swap;
                }
                start = end;
            }
            List<string> added = batch.Select(c => c.Id).ToList();
            queue.AddRange(added);
            return added;
        }

        public static HashSet<string> ExcludedBySnooze(IEnumerable<string> snoozed,
            IReadOnlyDictionary<string, List<string>> prerequisites)
        {
            HashSet<string> excluded = new HashSet<string>(snoozed);
            bool changed;
            do
            {
                changed = false;
                foreach (KeyValuePair<string, List<string>> item in prerequisites)
                    if (!excluded.Contains(item.Key) && item.Value.Any(excluded.Contains))
                        changed |= excluded.Add(item.Key);
            } while (changed);
            return excluded;
        }

        public static void Move(List<string> queue, string id, int index)
        {
            if (!queue.Remove(id)) return;
            queue.Insert(Math.Max(0, Math.Min(index, queue.Count)), id);
        }

        public static void MoveVisible(List<string> queue, IReadOnlyList<string> visible, int from, int boundary)
        {
            if (from < 0 || from >= visible.Count || boundary < 0 || boundary > visible.Count ||
                boundary == from || boundary == from + 1) return;
            string id = visible[from];
            string? before = boundary < visible.Count ? visible[boundary] : null;
            if (!queue.Contains(id) || (before != null && !queue.Contains(before))) return;
            queue.Remove(id);
            queue.Insert(before == null ? queue.Count : queue.IndexOf(before), id);
        }

        public static int FirstVisibleRow(int count, float offset, float pitch) =>
            Math.Max(0, Math.Min(count, (int)Math.Floor(Math.Max(0f, offset) / pitch)));

        public static int EndVisibleRow(int count, float offset, float height, float pitch) =>
            Math.Max(0, Math.Min(count, (int)Math.Ceiling((Math.Max(0f, offset) + Math.Max(0f, height)) / pitch)));

        public static float Average(IReadOnlyList<float> rates)
        {
            if (rates.Count == 0) return 0f;
            float total = 0f;
            for (int i = 0; i < rates.Count; i++) total += rates[i];
            return total / rates.Count;
        }

        public static void AddHourlySample(List<float> rates, float progress)
        {
            rates.Add(Math.Max(0f, progress) * 24f);
            if (rates.Count > 240) rates.RemoveRange(0, rates.Count - 240);
        }

        public static float CompletionDays(float remaining, float projectRate, float systemRate)
        {
            float rate = projectRate > 0f ? projectRate : systemRate;
            return rate > 0f ? Math.Max(0f, remaining) / rate : -1f;
        }
    }
}

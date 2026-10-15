using System;
using System.Collections.Generic;

namespace ResearchQueue.Core
{
    // Queue operations free of game types, so the offline checks can run them.
    public static class QueueRules
    {
        public const int MaxRateSamples = 240;

        // Sorts a new batch by era, then cost, and shuffles exact ties.
        public static void OrderBatch<T>(List<T> batch, Func<T, int> era, Func<T, float> cost, Random random)
        {
            batch.Sort((a, b) =>
            {
                int byEra = era(a).CompareTo(era(b));
                return byEra != 0 ? byEra : cost(a).CompareTo(cost(b));
            });
            int start = 0;
            while (start < batch.Count)
            {
                int end = start + 1;
                while (end < batch.Count && era(batch[end]) == era(batch[start]) && cost(batch[end]) == cost(batch[start]))
                    end++;
                for (int i = end - 1; i > start; i--)
                {
                    int j = random.Next(start, i + 1);
                    T swap = batch[i];
                    batch[i] = batch[j];
                    batch[j] = swap;
                }
                start = end;
            }
        }

        // Adds root and everything that depends on it, directly or indirectly.
        public static void AddWithDependents<T>(HashSet<T> result, T root, Func<T, IReadOnlyList<T>> dependents)
        {
            if (!result.Add(root)) return;
            Stack<T> pending = new Stack<T>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                IReadOnlyList<T> next = dependents(pending.Pop());
                for (int i = 0; i < next.Count; i++)
                    if (result.Add(next[i])) pending.Push(next[i]);
            }
        }

        public static void Move(List<string> queue, string id, int index)
        {
            if (!queue.Remove(id)) return;
            queue.Insert(Math.Max(0, Math.Min(index, queue.Count)), id);
        }

        // Applies a drop from a list that skips unresolved entries; boundary is the visible index the item lands before.
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

        public static void AddSample(List<float> samples, float value)
        {
            samples.Add(Math.Max(0f, value));
            if (samples.Count > MaxRateSamples) samples.RemoveRange(0, samples.Count - MaxRateSamples);
        }

        public static float Average(IReadOnlyList<float> samples)
        {
            if (samples.Count == 0) return 0f;
            float total = 0f;
            for (int i = 0; i < samples.Count; i++) total += samples[i];
            return total / samples.Count;
        }

        // Days to finish the given work at a daily rate; negative when there is no rate yet.
        public static float CompletionDays(float work, float dailyRate) =>
            dailyRate > 0f ? Math.Max(0f, work) / dailyRate : -1f;
    }
}

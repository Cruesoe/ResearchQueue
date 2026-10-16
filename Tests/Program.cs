using System;
using System.Collections.Generic;
using System.Linq;
using ResearchQueue.Core;

int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
    checks++;
}

List<(string Id, int Era, float Cost)> Batch(params (string, int, float)[] items) => items.ToList();
List<string> Ids(List<(string Id, int Era, float Cost)> batch) => batch.Select(b => b.Id).ToList();
void Order(List<(string Id, int Era, float Cost)> batch, int seed) => QueueRules.OrderBatch(batch, b => b.Era, b => b.Cost, new Random(seed));

List<(string Id, int Era, float Cost)> batch = Batch(("industrial", 4, 10f), ("expensive", 2, 300f), ("cheap", 2, 50f));
Order(batch, 1);
Check(Ids(batch).SequenceEqual(new[] { "cheap", "expensive", "industrial" }), "Batches sort by era, then cost");

List<(string Id, int Era, float Cost)> ties = Enumerable.Range(0, 20).Select(i => (i.ToString(), 2, 100f)).ToList();
Order(ties, 8);
Check(!Ids(ties).SequenceEqual(Enumerable.Range(0, 20).Select(i => i.ToString())), "Exact ties are shuffled");
Check(ties.Select(t => t.Id).Distinct().Count() == 20, "Shuffling keeps every entry");
List<(string Id, int Era, float Cost)> mixed = Batch(("b", 1, 5f), ("a", 1, 5f), ("c", 0, 9f));
Order(mixed, 3);
Check(mixed[0].Id == "c", "Shuffling stays within a tie group");

Dictionary<string, List<string>> dependents = new Dictionary<string, List<string>>
{
    ["root"] = new List<string> { "child" },
    ["child"] = new List<string> { "grandchild" },
    ["cycleA"] = new List<string> { "cycleB" },
    ["cycleB"] = new List<string> { "cycleA" }
};
IReadOnlyList<string> Next(string id) => dependents.TryGetValue(id, out List<string>? list) ? list : new List<string>();
HashSet<string> excluded = new HashSet<string>();
QueueRules.AddWithDependents(excluded, "root", Next);
Check(excluded.SetEquals(new[] { "root", "child", "grandchild" }), "Snoozing excludes every dependent");
HashSet<string> cycle = new HashSet<string>();
QueueRules.AddWithDependents(cycle, "cycleA", Next);
Check(cycle.SetEquals(new[] { "cycleA", "cycleB" }), "Dependency cycles terminate");

List<string> queue = new List<string> { "A", "B", "C" };
QueueRules.Move(queue, "C", 0);
Check(queue.SequenceEqual(new[] { "C", "A", "B" }), "Move to top moves to the front");
QueueRules.Move(queue, "missing", 0);
Check(queue.SequenceEqual(new[] { "C", "A", "B" }), "Moving an absent entry does nothing");

List<string> recovery = new List<string> { "missing", "A", "B", "C" };
QueueRules.MoveVisible(recovery, new[] { "A", "B", "C" }, 0, 2);
Check(recovery.SequenceEqual(new[] { "missing", "B", "A", "C" }), "Drops map around entries from removed mods");
QueueRules.MoveVisible(recovery, new[] { "B", "A", "C" }, 2, 0);
Check(recovery.SequenceEqual(new[] { "missing", "C", "B", "A" }), "Upward drops keep unknown entries in place");
QueueRules.MoveVisible(recovery, new[] { "C", "B", "A" }, 0, 3);
Check(recovery.SequenceEqual(new[] { "missing", "B", "A", "C" }), "A drop at the end lands last");
string[] unchanged = recovery.ToArray();
QueueRules.MoveVisible(recovery, new[] { "B", "A", "C" }, 1, 2);
Check(recovery.SequenceEqual(unchanged), "A drop next to itself does nothing");

Check(QueueRules.FirstVisibleRow(1000, 540f, 54f) == 10 && QueueRules.EndVisibleRow(1000, 540f, 810f, 54f) == 25, "Only rows in the viewport are drawn");
Check(QueueRules.EndVisibleRow(1000, 541f, 810f, 54f) == 26, "A partly visible last row is drawn");
Check(QueueRules.FirstVisibleRow(0, 0f, 54f) == 0 && QueueRules.EndVisibleRow(0, 0f, 810f, 54f) == 0, "An empty list draws nothing");

List<float> rates = new List<float>();
Check(QueueRules.Average(rates) == 0f && QueueRules.CompletionDays(100f, 0f) < 0f, "No history means an unknown estimate");
QueueRules.AddSample(rates, 240f);
QueueRules.AddSample(rates, 0f);
Check(QueueRules.Average(rates) == 120f, "Idle hours count toward the average");
QueueRules.AddSample(rates, -5f);
Check(rates.Last() == 0f, "Negative samples clamp to zero");
for (int i = 0; i < 300; i++) QueueRules.AddSample(rates, 0f);
Check(rates.Count == QueueRules.MaxRateSamples && QueueRules.Average(rates) == 0f, "History keeps the last ten days");
Check(QueueRules.CompletionDays(600f, 100f) == 6f, "Cumulative work divides by the daily rate");

Console.WriteLine($"PASS: {checks} offline queue checks.");

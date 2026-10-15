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
QueueCandidate Candidate(string id, int era, float cost) => new QueueCandidate(id, era, cost);
List<string> queue = new List<string>();
HashSet<string> excluded = new HashSet<string>();
QueueRules.AppendAvailable(queue, new[] { Candidate("industrial", 4, 10), Candidate("expensive", 2, 300), Candidate("cheap", 2, 50) }, excluded, new Random(1));
Check(queue.SequenceEqual(new[] { "cheap", "expensive", "industrial" }), "Era before total cost");
QueueRules.Move(queue, "industrial", 0);
QueueRules.AppendAvailable(queue, new[] { Candidate("newCheap", 1, 1), Candidate("cheap", 2, 50) }, excluded, new Random(2));
Check(queue.SequenceEqual(new[] { "industrial", "cheap", "expensive", "newCheap" }), "Append preserves manual priorities and removes duplicates");
string[] saved = queue.ToArray();
QueueRules.AppendAvailable(queue, Array.Empty<QueueCandidate>(), excluded, new Random(3));
Check(queue.SequenceEqual(saved), "Empty availability check preserves order");
Dictionary<string, List<string>> dependencies = new Dictionary<string, List<string>>
{
    ["root"] = new List<string>(), ["child"] = new List<string> { "root" },
    ["grandchild"] = new List<string> { "child" }, ["other"] = new List<string>(),
    ["cycleA"] = new List<string> { "cycleB" }, ["cycleB"] = new List<string> { "cycleA" }
};
HashSet<string> blocked = QueueRules.ExcludedBySnooze(new[] { "root" }, dependencies);
Check(blocked.SetEquals(new[] { "root", "child", "grandchild" }), "Snooze removes transitive dependents across systems");
Check(QueueRules.ExcludedBySnooze(new[] { "cycleA" }, dependencies).SetEquals(new[] { "cycleA", "cycleB" }), "Dependency cycles terminate");
List<string> snoozedQueue = new List<string>();
QueueRules.AppendAvailable(snoozedQueue, new[] { Candidate("root", 2, 1), Candidate("child", 2, 2), Candidate("other", 2, 3) }, blocked, new Random(1));
Check(snoozedQueue.SequenceEqual(new[] { "other" }), "Snoozed branches do not get requeued");
QueueRules.AppendAvailable(snoozedQueue, new[] { Candidate("root", 2, 1) }, new HashSet<string>(), new Random(1));
Check(snoozedQueue.SequenceEqual(new[] { "other", "root" }), "Restore appends to end");
Check(QueueRules.ExcludedBySnooze(new[] { "child" }, dependencies).Contains("grandchild"), "Independently snoozed child stays excluded after root restoration");
List<string> ties = new List<string>();
QueueRules.AppendAvailable(ties, Enumerable.Range(0, 20).Select(i => Candidate(i.ToString(), 2, 100)), excluded, new Random(8));
Check(!ties.SequenceEqual(Enumerable.Range(0, 20).Select(i => i.ToString())), "Equal era/cost candidates randomize");
Check(ties.Distinct().Count() == 20, "Tie randomization preserves all entries");
List<float> rates = new List<float>();
Check(QueueRules.Average(rates) == 0f, "Missing history has no rate");
Check(QueueRules.CompletionDays(100f, 0f, 0f) == -1f, "No positive rate gives unknown estimate");
QueueRules.AddHourlySample(rates, 10f);
Check(QueueRules.Average(rates) == 240f, "Short available history is usable");
QueueRules.AddHourlySample(rates, 0f);
Check(QueueRules.Average(rates) == 120f, "Idle hours count toward average");
Check(QueueRules.CompletionDays(240f, 0f, 120f) == 2f, "Fallback uses system rate");
Check(QueueRules.CompletionDays(240f, 240f, 120f) == 1f, "Project-specific positive rate preferred");
for (int i = 0; i < 240; i++) QueueRules.AddHourlySample(rates, 0f);
Check(rates.Count == 240 && QueueRules.Average(rates) == 0f, "History rolls off after ten days");
float first = QueueRules.CompletionDays(200f, 100f, 0f);
float second = first + QueueRules.CompletionDays(300f, 100f, 0f);
float third = second + QueueRules.CompletionDays(100f, 100f, 0f);
Check(first == 2f && second == 5f && third == 6f, "Queue estimates are cumulative");
List<string> recovery = new List<string> { "missing", "A", "B", "C" };
QueueRules.MoveVisible(recovery, new[] { "A", "B", "C" }, 0, 2);
Check(recovery.SequenceEqual(new[] { "missing", "B", "A", "C" }), "Visible drop maps around retained missing definitions");
QueueRules.MoveVisible(recovery, new[] { "B", "A", "C" }, 2, 0);
Check(recovery.SequenceEqual(new[] { "missing", "C", "B", "A" }), "Upward visible move preserves unknown names");
QueueRules.MoveVisible(recovery, new[] { "C", "B", "A" }, 0, 3);
Check(recovery.SequenceEqual(new[] { "missing", "B", "A", "C" }), "Drop at end retains hidden definitions");
string[] unchanged = recovery.ToArray();
QueueRules.MoveVisible(recovery, new[] { "B", "A", "C" }, 1, 2);
Check(recovery.SequenceEqual(unchanged), "Adjacent drop is inert");
Check(QueueRules.FirstVisibleRow(1000, 540f, 54f) == 10 && QueueRules.EndVisibleRow(1000, 540f, 810f, 54f) == 25,
    "Thousand-entry queue renders only fifteen full viewport rows");
Check(QueueRules.FirstVisibleRow(0, 0f, 54f) == 0 && QueueRules.EndVisibleRow(0, 0f, 810f, 54f) == 0,
    "Empty viewport range is safe");
Check(QueueRules.EndVisibleRow(1000, 541f, 810f, 54f) == 26, "Partly visible final row is included");
Console.WriteLine($"PASS: {checks} offline queue and rate checks.");

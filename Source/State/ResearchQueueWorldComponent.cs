using System;
using System.Collections.Generic;
using System.Linq;
using ResearchQueue.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ResearchQueue.State
{
    public sealed class ResearchQueueWorldComponent : WorldComponent
    {
        private List<ResearchSystemQueueState> queues = new List<ResearchSystemQueueState>();
        private List<ResearchRateState> rates = new List<ResearchRateState>();
        private int nextRateTick;
        private int nextCheckTick;
        private bool legacyNodeQueueImported;
        private List<string> deferredNodeProjects = new List<string>();
        private bool dirty = true;
        private int revision;
        private readonly Dictionary<string, ResearchRateState> rateCache = new Dictionary<string, ResearchRateState>();
        private readonly Dictionary<string, ResearchProjectDef> definitions = new Dictionary<string, ResearchProjectDef>();
        private readonly Dictionary<string, List<string>> dependencies = new Dictionary<string, List<string>>();
        private HashSet<string> excluded = new HashSet<string>();

        public static ResearchQueueWorldComponent? Get() => Current.Game?.World?.GetComponent<ResearchQueueWorldComponent>();
        public IReadOnlyList<ResearchSystemQueueState> Queues => queues;
        public int Revision => revision;
        public bool IsExcluded(string id) => excluded.Contains(id);
        public void RequestRefresh() { dirty = true; }

        public ResearchQueueWorldComponent(World world) : base(world) { }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            dirty = true;
            foreach (ResearchProjectDef project in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                definitions[project.defName] = project;
                dependencies[project.defName] = ResearchSystems.Prerequisites(project).Select(p => p.defName).ToList();
            }
            foreach (ResearchRateState rate in rates) rateCache[rate.Id] = rate;
            RebuildExclusions();
            ResearchQueueRuntime.ApplyWindow();
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();
            if (!ResearchQueueRuntime.Enabled || Current.ProgramState != ProgramState.Playing ||
                Faction.OfPlayerSilentFail == null || LongEventHandler.AnyEventNowOrWaiting) return;
            int tick = Find.TickManager.TicksGame;
            if (dirty || tick >= nextCheckTick)
            {
                RefreshQueues();
                nextCheckTick = tick + 250;
            }
            if (tick >= nextRateTick)
            {
                SampleRates();
                nextRateTick = tick + GenDate.TicksPerHour;
            }
        }

        public ResearchProjectDef? Resolve(string id) => definitions.TryGetValue(id, out ResearchProjectDef? value) ? value : null;

        public ResearchSystemQueueState Queue(string key)
        {
            ResearchSystemQueueState? result = queues.FirstOrDefault(q => q.SystemId == key);
            if (result == null) { result = new ResearchSystemQueueState(key); queues.Add(result); }
            return result;
        }

        public IEnumerable<ResearchProjectDef> Active(string key) => definitions.Values.Where(p =>
            ResearchSystems.Key(p) == key && !p.IsFinished && Find.ResearchManager.IsCurrentProject(p));

        public void RefreshQueues()
        {
            dirty = false;
            ResearchQueueRuntime.ApplyWindow();
            NodeResearchBridge.Prepare();
            foreach (string key in ResearchSystems.AllKeys()) Queue(key);
            RebuildExclusions();
            CaptureLegacyNodeQueue();
            if (deferredNodeProjects.Count > 0)
            {
                foreach (string id in deferredNodeProjects.ToArray())
                {
                    ResearchProjectDef? project = Resolve(id);
                    if (project?.IsFinished == true) { deferredNodeProjects.Remove(id); continue; }
                    if (project != null && ResearchSystems.CanStart(project) && !excluded.Contains(id))
                    {
                        ResearchSystemQueueState destination = Queue(ResearchSystems.Key(project));
                        if (!destination.ProjectIds.Contains(id) && !Find.ResearchManager.IsCurrentProject(project))
                        {
                            destination.ProjectIds.Add(id);
                            if (destination.Initialized && !destination.NewProjectIds.Contains(id)) destination.NewProjectIds.Add(id);
                        }
                        deferredNodeProjects.Remove(id);
                    }
                }
                legacyNodeQueueImported = true;
            }
            foreach (ResearchSystemQueueState queue in queues)
            {
                List<ResearchProjectDef> active = Active(queue.SystemId).ToList();
                foreach (ResearchProjectDef blocked in active.Where(p => !ResearchSystems.CanStart(p)).ToList())
                {
                    Find.ResearchManager.StopProject(blocked);
                    if (!queue.ProjectIds.Contains(blocked.defName)) queue.ProjectIds.Insert(0, blocked.defName);
                    active.Remove(blocked);
                }
                // Unknown definitions retain their saved positions for recovery if the mod returns.
                queue.ProjectIds.RemoveAll(id => Resolve(id)?.IsFinished == true || excluded.Contains(id) || active.Any(p => p.defName == id));
                HashSet<string> unavailable = new HashSet<string>(excluded);
                unavailable.UnionWith(active.Select(p => p.defName));
                IEnumerable<QueueCandidate> candidates = definitions.Values.Where(p => ResearchSystems.Key(p) == queue.SystemId && ResearchSystems.CanStart(p))
                    .Select(p => new QueueCandidate(p.defName, (int)p.techLevel, p.CostApparent));
                List<string> added = QueueRules.AppendAvailable(queue.ProjectIds, candidates, unavailable, new Random(Rand.Int));
                if (queue.Initialized) queue.NewProjectIds.AddRange(added.Where(id => !queue.NewProjectIds.Contains(id)));
                queue.Initialized = true;
                if (active.Count == 0)
                {
                    ResearchProjectDef? next = queue.ProjectIds.Select(Resolve).FirstOrDefault(p => p != null && ResearchSystems.CanStart(p));
                    if (next != null)
                    {
                        ResearchQueueRuntime.Start(next);
                        if (Find.ResearchManager.IsCurrentProject(next)) queue.ProjectIds.Remove(next.defName);
                    }
                }
            }
            revision++;
        }

        public void CaptureLegacyNodeQueue()
        {
            foreach (string id in NodeResearchBridge.TakeLegacyQueue())
                if (!deferredNodeProjects.Contains(id)) deferredNodeProjects.Add(id);
        }

        private void RebuildExclusions()
        {
            excluded = QueueRules.ExcludedBySnooze(queues.SelectMany(q => q.SnoozedProjectIds)
                .Where(id => Resolve(id)?.IsFinished != true), dependencies);
        }

        public List<ResearchProjectDef> AffectedDependents(ResearchProjectDef root)
        {
            HashSet<string> affected = QueueRules.ExcludedBySnooze(new[] { root.defName }, dependencies);
            HashSet<string> queued = new HashSet<string>(queues.SelectMany(q => q.ProjectIds));
            queued.UnionWith(definitions.Values.Where(p => !p.IsFinished && Find.ResearchManager.IsCurrentProject(p)).Select(p => p.defName));
            return definitions.Values.Where(p => p != root && affected.Contains(p.defName) && queued.Contains(p.defName)).ToList();
        }

        public void Snooze(ResearchProjectDef project)
        {
            List<string> snoozed = Queue(ResearchSystems.Key(project)).SnoozedProjectIds;
            if (!snoozed.Contains(project.defName)) snoozed.Add(project.defName);
            HashSet<string> affected = QueueRules.ExcludedBySnooze(new[] { project.defName }, dependencies);
            foreach (ResearchProjectDef active in definitions.Values.Where(p => !p.IsFinished &&
                affected.Contains(p.defName) && Find.ResearchManager.IsCurrentProject(p)).ToList())
                Find.ResearchManager.StopProject(active);
            RefreshQueues();
        }

        public void Restore(ResearchProjectDef project)
        {
            Queue(ResearchSystems.Key(project)).SnoozedProjectIds.Remove(project.defName);
            RefreshQueues();
        }

        public IEnumerable<ResearchProjectDef> Snoozed(string key) => definitions.Values.Where(p =>
            !p.IsFinished && ResearchSystems.Key(p) == key && excluded.Contains(p.defName));

        public string SnoozeReason(ResearchProjectDef project)
        {
            if (Queue(ResearchSystems.Key(project)).SnoozedProjectIds.Contains(project.defName))
                return "ResearchQueue_ExplicitSnooze".Translate();
            return "ResearchQueue_SnoozedDependency".Translate();
        }

        public void Move(ResearchProjectDef project, int index)
        {
            QueueRules.Move(Queue(ResearchSystems.Key(project)).ProjectIds, project.defName, index);
            revision++;
        }

        public void ClearNewMarkers()
        {
            foreach (ResearchSystemQueueState queue in queues) queue.NewProjectIds.Clear();
            revision++;
        }

        public void MoveVisible(IReadOnlyList<string> visible, int from, int boundary)
        {
            if (from < 0 || from >= visible.Count) return;
            ResearchProjectDef? project = Resolve(visible[from]);
            if (project == null) return;
            QueueRules.MoveVisible(Queue(ResearchSystems.Key(project)).ProjectIds, visible, from, boundary);
            revision++;
        }

        private ResearchRateState Rate(string id)
        {
            if (!rateCache.TryGetValue(id, out ResearchRateState? value))
            { value = new ResearchRateState(id); rates.Add(value); rateCache[id] = value; }
            return value;
        }

        private void SampleRates()
        {
            Dictionary<string, float> totals = queues.ToDictionary(q => q.SystemId, q => 0f);
            foreach (ResearchProjectDef project in definitions.Values)
            {
                ResearchRateState rate = Rate(project.defName);
                float progress = Math.Min(project.ProgressReal, project.Cost);
                float delta = rate.LastRealProgress >= 0f ? Math.Max(0f, progress - rate.LastRealProgress) : 0f;
                delta *= project.CostFactor(Faction.OfPlayer.def.techLevel);
                if (rate.Rates.Count > 0 || delta > 0f || Find.ResearchManager.IsCurrentProject(project))
                    QueueRules.AddHourlySample(rate.Rates, delta);
                rate.LastRealProgress = progress;
                string key = ResearchSystems.Key(project);
                if (totals.ContainsKey(key)) totals[key] += delta;
            }
            foreach (KeyValuePair<string, float> total in totals)
                QueueRules.AddHourlySample(Rate("System:" + total.Key).Rates, total.Value);
            revision++;
        }

        public Dictionary<string, float> Estimates(string key)
        {
            Dictionary<string, float> result = new Dictionary<string, float>();
            float cumulative = 0f;
            float average = QueueRules.Average(Rate("System:" + key).Rates);
            IEnumerable<ResearchProjectDef?> ordered = Active(key).Cast<ResearchProjectDef?>().Concat(Queue(key).ProjectIds.Select(Resolve));
            foreach (ResearchProjectDef? project in ordered)
            {
                if (project == null) continue;
                if (!ResearchSystems.CanStart(project)) { result[project.defName] = -1f; continue; }
                float duration = QueueRules.CompletionDays(project.CostApparent - project.ProgressApparent,
                    QueueRules.Average(Rate(project.defName).Rates), average);
                cumulative = duration < 0f || cumulative < 0f ? -1f : cumulative + duration;
                result[project.defName] = cumulative;
            }
            return result;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref queues, "researchQueueSystems", LookMode.Deep);
            Scribe_Collections.Look(ref rates, "researchQueueRates", LookMode.Deep);
            Scribe_Values.Look(ref nextRateTick, "researchQueueNextRateTick");
            Scribe_Values.Look(ref legacyNodeQueueImported, "researchQueueNodeImported", false);
            Scribe_Collections.Look(ref deferredNodeProjects, "researchQueueDeferredNodeProjects", LookMode.Value);
            queues ??= new List<ResearchSystemQueueState>();
            rates ??= new List<ResearchRateState>();
            deferredNodeProjects ??= new List<string>();
        }
    }
}

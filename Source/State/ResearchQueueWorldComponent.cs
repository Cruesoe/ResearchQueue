using System;
using System.Collections.Generic;
using ResearchQueue.Core;
using ResearchQueue.Research;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ResearchQueue.State
{
    // Keeps every research system's queue current and starts its next project when the slot is free.
    public sealed class ResearchQueueWorldComponent : WorldComponent
    {
        // Finishing, snoozing and restoring refresh at once; polling only catches other unlocks (benches, techprints, eras).
        // Hourly while standard research runs, faster while its slot is empty so new unlocks start promptly.
        private const int BusyRefreshInterval = GenDate.TicksPerHour;
        private const int IdleRefreshInterval = 250;

        private List<ResearchSystemQueueState> systems = new List<ResearchSystemQueueState>();
        private int nextSampleTick;

        private readonly List<ResearchSystemQueueState> active = new List<ResearchSystemQueueState>();
        private readonly Dictionary<ResearchSystem, ResearchSystemQueueState> states = new Dictionary<ResearchSystem, ResearchSystemQueueState>();
        private readonly HashSet<ResearchProjectDef> excluded = new HashSet<ResearchProjectDef>();
        private readonly Dictionary<ResearchProjectDef, float> sampleStart = new Dictionary<ResearchProjectDef, float>();
        private readonly HashSet<string> queuedBuffer = new HashSet<string>();
        private readonly List<ResearchProjectDef> batchBuffer = new List<ResearchProjectDef>();
        private int nextRefreshTick;
        private bool dirty = true;

        public ResearchQueueWorldComponent(World world) : base(world) { }

        public static ResearchQueueWorldComponent? Get() => Current.Game?.World?.GetComponent<ResearchQueueWorldComponent>();

        // Bumped whenever a queue, snooze or rate changes, so the window knows to rebuild.
        public int Revision { get; private set; }
        public bool Dirty => dirty;
        public IReadOnlyList<ResearchSystemQueueState> Active => active;

        public void RequestRefresh() => dirty = true;
        public bool IsExcluded(ResearchProjectDef project) => excluded.Contains(project);

        public ResearchSystemQueueState? StateOf(ResearchProjectDef project)
        {
            ResearchSystem? system = ResearchCatalog.SystemOf(project);
            return system != null && states.TryGetValue(system, out ResearchSystemQueueState? state) ? state : null;
        }

        public ResearchSystemQueueState StateOf(ResearchSystem system) => states[system];

        public bool IsSnoozed(ResearchProjectDef project) => StateOf(project)?.Snoozed.Contains(project.defName) == true;

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            active.Clear();
            states.Clear();
            foreach (ResearchSystem system in ResearchCatalog.Systems)
            {
                ResearchSystemQueueState? state = systems.Find(s => s.SystemId == system.Id);
                if (state == null) systems.Add(state = new ResearchSystemQueueState(system.Id));
                state.System = system;
                active.Add(state);
                states[system] = state;
            }
            ImportNodeQueue();
            RebuildExclusions();
            ResetSampling();
            dirty = true;
        }

        public override void WorldComponentTick()
        {
            int tick = Find.TickManager.TicksGame;
            if (dirty || tick >= nextRefreshTick) Refresh();
            if (tick >= nextSampleTick)
            {
                SampleRates();
                nextSampleTick = tick + GenDate.TicksPerHour;
            }
        }

        public void Refresh()
        {
            dirty = false;
            bool changed = ImportNodeQueue();
            RebuildExclusions();
            for (int i = 0; i < active.Count; i++) changed |= RefreshSystem(active[i]);
            if (changed) Revision++;
            bool idle = ResearchCatalog.Standard.CurrentProject() == null;
            nextRefreshTick = Find.TickManager.TicksGame + (idle ? IdleRefreshInterval : BusyRefreshInterval);
        }

        private bool RefreshSystem(ResearchSystemQueueState state)
        {
            ResearchSystem system = state.System!;
            List<string> queue = state.Queue;
            bool changed = false;

            // A current project that loses a requirement goes back to the head of the queue and yields its slot.
            ResearchProjectDef? current = system.CurrentProject();
            if (current != null && !ResearchRequirements.CanStart(current))
            {
                Find.ResearchManager.StopProject(current);
                queue.Remove(current.defName);
                queue.Insert(0, current.defName);
                current = null;
                changed = true;
            }

            // Unknown defNames stay for when their mod returns; blocked entries keep their place.
            for (int i = queue.Count - 1; i >= 0; i--)
            {
                ResearchProjectDef? project = Resolve(queue[i]);
                if (project != null && (project.IsFinished || project == current || excluded.Contains(project) || ResearchCatalog.SystemOf(project) != system))
                {
                    queue.RemoveAt(i);
                    changed = true;
                }
            }

            queuedBuffer.Clear();
            for (int i = 0; i < queue.Count; i++) queuedBuffer.Add(queue[i]);
            batchBuffer.Clear();
            List<ResearchProjectDef> projects = system.Projects;
            for (int i = 0; i < projects.Count; i++)
            {
                ResearchProjectDef project = projects[i];
                if (project != current && !queuedBuffer.Contains(project.defName) && !excluded.Contains(project) && ResearchRequirements.CanStart(project))
                    batchBuffer.Add(project);
            }
            if (batchBuffer.Count > 0)
            {
                QueueRules.OrderBatch(batchBuffer, p => (int)p.techLevel, p => p.CostApparent, new Random(Rand.Int));
                foreach (ResearchProjectDef project in batchBuffer)
                {
                    queue.Add(project.defName);
                    if (state.Initialized) state.NewIds.Add(project.defName);
                }
                changed = true;
            }
            state.Initialized = true;

            if (current == null)
            {
                for (int i = 0; i < queue.Count; i++)
                {
                    ResearchProjectDef? project = Resolve(queue[i]);
                    if (project == null || !ResearchRequirements.CanStart(project)) continue;
                    ResearchSelection.Start(project);
                    if (Find.ResearchManager.IsCurrentProject(project))
                    {
                        queue.RemoveAt(i);
                        if (!sampleStart.ContainsKey(project)) sampleStart[project] = project.ProgressReal;
                        changed = true;
                    }
                    break;
                }
            }
            return changed;
        }

        // Node's queue is filled only by its own window, which this mod replaces; its entries move to the front of ours.
        private bool ImportNodeQueue()
        {
            List<string>? legacy = NodeResearch.TakeQueue();
            if (legacy == null) return false;
            for (int i = legacy.Count - 1; i >= 0; i--)
            {
                ResearchProjectDef? project = Resolve(legacy[i]);
                ResearchSystemQueueState? state = project == null || project.IsFinished ? null : StateOf(project);
                if (state == null) continue;
                state.Queue.Remove(project!.defName);
                state.Queue.Insert(0, project.defName);
            }
            return true;
        }

        private void RebuildExclusions()
        {
            excluded.Clear();
            foreach (ResearchSystemQueueState state in systems)
            {
                foreach (string id in state.Snoozed)
                {
                    ResearchProjectDef? project = Resolve(id);
                    if (project != null && !project.IsFinished) QueueRules.AddWithDependents(excluded, project, ResearchCatalog.Dependents);
                }
            }
        }

        public void Snooze(ResearchProjectDef project)
        {
            ResearchSystemQueueState? state = StateOf(project);
            if (state == null) return;
            if (!state.Snoozed.Contains(project.defName)) state.Snoozed.Add(project.defName);
            RebuildExclusions();
            foreach (ResearchSystemQueueState other in active)
            {
                ResearchProjectDef? current = other.System!.CurrentProject();
                if (current != null && excluded.Contains(current)) Find.ResearchManager.StopProject(current);
            }
            Refresh();
            Revision++;
        }

        public void Restore(ResearchProjectDef project)
        {
            if (StateOf(project)?.Snoozed.Remove(project.defName) != true) return;
            Refresh();
            Revision++;
        }

        // Unfinished projects, queued or not, that snoozing this one would also exclude; by era, then cost, like the queue.
        public List<ResearchProjectDef> BlockedDependents(ResearchProjectDef project)
        {
            HashSet<ResearchProjectDef> affected = new HashSet<ResearchProjectDef>();
            QueueRules.AddWithDependents(affected, project, ResearchCatalog.Dependents);
            List<ResearchProjectDef> result = new List<ResearchProjectDef>();
            foreach (ResearchProjectDef dependent in affected)
                if (dependent != project && !dependent.IsFinished && !excluded.Contains(dependent) && ResearchCatalog.Allowed(dependent)) result.Add(dependent);
            result.Sort(ResearchCatalog.ByEra);
            return result;
        }

        // Explicitly snoozed projects that exclude this one through its prerequisites.
        public List<ResearchProjectDef> SnoozedPrerequisites(ResearchProjectDef project)
        {
            List<ResearchProjectDef> result = new List<ResearchProjectDef>();
            HashSet<ResearchProjectDef> seen = new HashSet<ResearchProjectDef>();
            Stack<ResearchProjectDef> pending = new Stack<ResearchProjectDef>();
            pending.Push(project);
            while (pending.Count > 0)
            {
                foreach (ResearchProjectDef prerequisite in ResearchCatalog.Prerequisites(pending.Pop()))
                {
                    if (!seen.Add(prerequisite) || prerequisite.IsFinished) continue;
                    if (IsSnoozed(prerequisite)) result.Add(prerequisite);
                    pending.Push(prerequisite);
                }
            }
            return result;
        }

        // Starts the project at once; the project it replaces keeps its progress and goes to the top of the queue.
        public void ResearchNow(ResearchProjectDef project)
        {
            ResearchSystemQueueState? state = StateOf(project);
            if (state == null || !ResearchRequirements.CanStart(project) || Find.ResearchManager.IsCurrentProject(project)) return;
            ResearchProjectDef? current = state.System!.CurrentProject();
            if (current != null) Find.ResearchManager.StopProject(current);
            ResearchSelection.Start(project);
            if (!Find.ResearchManager.IsCurrentProject(project))
            {
                // The game refused the switch: put the old project back so the slot isn't left empty.
                if (current != null) ResearchSelection.Start(current);
                return;
            }
            state.Queue.Remove(project.defName);
            if (current != null)
            {
                state.Queue.Remove(current.defName);
                state.Queue.Insert(0, current.defName);
            }
            if (!sampleStart.ContainsKey(project)) sampleStart[project] = project.ProgressReal;
            Revision++;
        }

        public void MoveToFront(ResearchProjectDef project)
        {
            ResearchSystemQueueState? state = StateOf(project);
            if (state == null) return;
            QueueRules.Move(state.Queue, project.defName, 0);
            Revision++;
        }

        public void MoveVisible(ResearchSystemQueueState state, IReadOnlyList<string> visible, int from, int boundary)
        {
            QueueRules.MoveVisible(state.Queue, visible, from, boundary);
            Revision++;
        }

        public void ClearNewMarkers()
        {
            bool any = false;
            foreach (ResearchSystemQueueState state in systems)
            {
                any |= state.NewIds.Count > 0;
                state.NewIds.Clear();
            }
            if (any) Revision++;
        }

        // Apparent progress tracks research work regardless of the project's era, so one rate serves the whole system.
        private void SampleRates()
        {
            for (int i = 0; i < active.Count; i++) active[i].SampledProgress = 0f;
            TechLevel level = Faction.OfPlayer.def.techLevel;
            foreach (KeyValuePair<ResearchProjectDef, float> start in sampleStart)
            {
                ResearchSystemQueueState? state = StateOf(start.Key);
                if (state == null) continue;
                float progress = Math.Min(start.Key.ProgressReal, start.Key.Cost);
                state.SampledProgress += Math.Max(0f, progress - start.Value) * start.Key.CostFactor(level);
            }
            for (int i = 0; i < active.Count; i++)
                QueueRules.AddSample(active[i].Rates, active[i].SampledProgress * GenDate.HoursPerDay);
            ResetSampling();
            Revision++;
        }

        private void ResetSampling()
        {
            sampleStart.Clear();
            for (int i = 0; i < active.Count; i++)
            {
                ResearchProjectDef? current = active[i].System!.CurrentProject();
                if (current != null) sampleStart[current] = current.ProgressReal;
            }
        }

        private static ResearchProjectDef? Resolve(string id) => DefDatabase<ResearchProjectDef>.GetNamedSilentFail(id);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref systems, "researchQueueSystems", LookMode.Deep);
            Scribe_Values.Look(ref nextSampleTick, "researchQueueNextRateTick");
            systems ??= new List<ResearchSystemQueueState>();
        }
    }
}

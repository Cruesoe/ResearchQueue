using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ResearchQueue;
using ResearchQueue.State;
using RimWorld;
using Verse;

namespace ResearchQueueFixture;

public sealed class IntegrationChecks : GameComponent
{
    private bool ran;
    private bool verifyRoundTrip;
    private List<string> expectedQueue = new();
    private List<string> expectedSnoozed = new();
    private int checks;
    private string Report => Path.Combine(GenFilePaths.SaveDataFolderPath, "Integration.txt");
    private static ResearchProjectDef Project(string id) => DefDatabase<ResearchProjectDef>.GetNamed(id);
    public IntegrationChecks(Game game) { }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref verifyRoundTrip, "verifyRoundTrip");
        Scribe_Collections.Look(ref expectedQueue, "expectedQueue", LookMode.Value);
        Scribe_Collections.Look(ref expectedSnoozed, "expectedSnoozed", LookMode.Value);
    }

    private void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        checks++;
        File.AppendAllText(Report, "PASS " + description + "\n");
    }

    private void Compatibility(ResearchQueueWorldComponent tracker)
    {
        File.WriteAllText(Report, "Research Queue optional-mod checks\n");
        tracker.RefreshQueues();
        Check(MainButtonDefOf.Research.tabWindowClass == typeof(ResearchQueue.UI.MainTabWindow_ResearchQueue), "Queue owns research window with optional mods");
        foreach (ResearchSystemQueueState q in tracker.Queues)
        {
            Check(q.ProjectIds.Distinct().Count() == q.ProjectIds.Count, "No duplicate projects: " + q.SystemId);
            Check(q.ProjectIds.All(id => tracker.Resolve(id) == null || ResearchSystems.Key(tracker.Resolve(id)!) == q.SystemId), "Projects route to their actual system: " + q.SystemId);
        }
        if (ModsConfig.IsActive("ferny.noderesearch"))
        {
            Check(DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(p => ResearchSystems.Key(p) == ResearchSystems.Standard).All(p => p.tab.defName == "Main"), "Node tab collapse preserved for advancement counts");
            Check(DefDatabase<ResearchProjectDef>.AllDefsListForReading.Any(p => p.modExtensions?.Any(e => e.GetType().Name == "EmergenceExtension") == true), "Node generated emergence projects remain present");
        }
        if (ModsConfig.IsActive("vanillaexpanded.gravship"))
        {
            List<ResearchProjectDef> grav = DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(p => ResearchSystems.Key(p) == ResearchSystems.Gravship).ToList();
            Check(grav.Count > 0 && tracker.Queues.Any(q => q.SystemId == ResearchSystems.Gravship), "Gravtech has a separate queue");
            ResearchProjectDef selected = grav.First();
            foreach (ResearchProjectDef prerequisite in ResearchSystems.Prerequisites(selected)) Find.ResearchManager.FinishProject(prerequisite, doCompletionLetter: false);
            Find.ResearchManager.gravEngineInspected = true;
            tracker.RefreshQueues();
            ResearchProjectDef standard = tracker.Active(ResearchSystems.Standard).Single();
            ResearchQueueRuntime.Start(selected);
            Check(Find.ResearchManager.IsCurrentProject(selected), "VGE selects its independent Gravtech slot");
            Check(tracker.Active(ResearchSystems.Standard).Single() == standard, "Gravtech selection preserves standard research");
            tracker.Snooze(selected);
            Check(tracker.Queue(ResearchSystems.Gravship).SnoozedProjectIds.Contains(selected.defName), "Gravtech snooze uses separate state");
            Check(tracker.Active(ResearchSystems.Standard).Single() == standard, "Gravtech snooze preserves standard active project");
        }
        File.AppendAllText(Report, "COMPLETE\n");
    }

    public override void GameComponentOnGUI()
    {
        if (ran || Current.ProgramState != ProgramState.Playing || LongEventHandler.AnyEventNowOrWaiting) return;
        ran = true;
        try
        {
            ResearchQueueWorldComponent tracker = ResearchQueueWorldComponent.Get()!;
            ResearchSystemQueueState queue = tracker.Queue(ResearchSystems.Standard);
            if (ModsConfig.IsActive("ferny.noderesearch") || ModsConfig.IsActive("vanillaexpanded.gravship"))
            {
                Compatibility(tracker);
                return;
            }
            if (verifyRoundTrip)
            {
                Check(queue.ProjectIds.SequenceEqual(expectedQueue), "Manual queue order survives reload while paused");
                Check(queue.SnoozedProjectIds.SequenceEqual(expectedSnoozed), "Explicit snoozes survive reload");
                Check(tracker.Snoozed(ResearchSystems.Standard).Any(p => p.defName == "CarpetMaking"), "Snoozed view works before any post-load tick");
                Check(tracker.Estimates(ResearchSystems.Standard).Values.Any(v => v > 0f), "Positive estimates survive reload");
                tracker.Restore(Project("CarpetMaking"));
                Check(queue.ProjectIds.Last() == "CarpetMaking", "Restore after reload appends to end");
                ResearchProjectDef standard = tracker.Active(ResearchSystems.Standard).Single();
                typeof(GameComponent_Anomaly).GetField("highestLevelReached", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(Find.Anomaly, 1);
                Check(Find.Anomaly.AnomalyStudyEnabled, "Anomaly study enabled in disposable test state");
                tracker.RefreshQueues();
                ResearchProjectDef basic = tracker.Active("Basic").Single();
                Check(basic.knowledgeCategory == KnowledgeCategoryDefOf.Basic, "Anomaly starts in its own research slot");
                Check(tracker.Active(ResearchSystems.Standard).Single() == standard, "Anomaly selection does not interrupt standard research");
                List<string> standardOrder = queue.ProjectIds.ToList();
                tracker.Snooze(basic);
                Check(tracker.Queue("Basic").SnoozedProjectIds.Contains(basic.defName), "Anomaly snoozing uses separate state");
                Check(queue.ProjectIds.SequenceEqual(standardOrder) && tracker.Active(ResearchSystems.Standard).Single() == standard, "Anomaly snoozing preserves standard queue and active project");
                tracker.Restore(basic);
                Check(!tracker.Queue("Basic").SnoozedProjectIds.Contains(basic.defName), "Anomaly restoration clears its explicit snooze");
                File.AppendAllText(Report, "COMPLETE\n");
                return;
            }

            File.WriteAllText(Report, "Research Queue integration checks\n");
            Check(Find.TickManager.Paused, "Regression save loaded paused");
            Check(queue.SnoozedProjectIds.Contains("CarpetMaking"), "Known snooze loaded from original regression save");
            Check(tracker.IsExcluded("CarpetMaking"), "Derived exclusion initialized without a game tick");
            Check(tracker.Snoozed(ResearchSystems.Standard).Any(p => p.defName == "CarpetMaking"), "Snoozed view resolves original save immediately");
            Check(tracker.Active(ResearchSystems.Standard).Single().defName == "RecurveBow", "Active research survives load");
            Check(queue.ProjectIds.Count == 29 && queue.ProjectIds[0] == "Brewing", "Pending baseline order survives load");
            tracker.Restore(Project("CarpetMaking"));
            Check(queue.ProjectIds.Last() == "CarpetMaking", "Restored research appends to existing priorities");
            Check(queue.NewProjectIds.Contains("CarpetMaking"), "Restored entry has New marker");
            tracker.Move(Project("CarpetMaking"), 0);
            Check(queue.ProjectIds[0] == "CarpetMaking", "Research next updates pending order");
            Find.ResearchManager.SetCurrentProject(Project("CarpetMaking"));
            Check(tracker.Active(ResearchSystems.Standard).Single().defName == "RecurveBow", "Normal selection cannot bypass queue");
            tracker.Snooze(Project("CarpetMaking"));
            Check(!queue.ProjectIds.Contains("CarpetMaking") && tracker.IsExcluded("CarpetMaking"), "Snooze removes pending entry");
            tracker.Restore(Project("CarpetMaking"));
            tracker.Move(Project("Hydroponics"), 0);
            List<string> before = queue.ProjectIds.ToList();
            Find.ResearchManager.FinishProject(Project("RecurveBow"), doCompletionLetter: false);
            tracker.WorldComponentTick();
            Check(tracker.Active(ResearchSystems.Standard).Single().defName == before[0], "Completion automatically starts the player's first pending project");
            List<string> preserved = before.Skip(1).ToList();
            Check(queue.ProjectIds.Take(preserved.Count).SequenceEqual(preserved), "Completion retains existing pending priorities");
            Check(!queue.ProjectIds.Contains("RecurveBow"), "Completed project stays out of pending queue");
            List<ResearchProjectDef> appended = queue.ProjectIds.Skip(preserved.Count).Select(Project).ToList();
            Check(appended.Count > 0 && appended.All(ResearchSystems.CanStart), "Completion appends newly unlocked available projects");
            Check(appended.Select(p => ((int)p.techLevel, p.CostApparent)).SequenceEqual(appended.OrderBy(p => p.techLevel).ThenBy(p => p.CostApparent).Select(p => ((int)p.techLevel, p.CostApparent))), "New completion batch sorts by era then cost");

            ResearchProjectDef interrupted = tracker.Active(ResearchSystems.Standard).Single();
            ThingDef? originalBench = interrupted.requiredResearchBuilding;
            interrupted.requiredResearchBuilding = DefDatabase<ThingDef>.GetNamed("HiTechResearchBench");
            try
            {
                tracker.RefreshQueues();
                Check(queue.ProjectIds[0] == interrupted.defName, "Blocked current project retained at head of pending queue");
                Check(tracker.Active(ResearchSystems.Standard).Single() != interrupted, "Blocked current project yields to eligible research");
                Check(tracker.Estimates(ResearchSystems.Standard)[interrupted.defName] < 0f, "Blocked project has no completion estimate");
            }
            finally { interrupted.requiredResearchBuilding = originalBench; }
            ResearchProjectDef replacement = tracker.Active(ResearchSystems.Standard).Single();
            tracker.RefreshQueues();
            Check(tracker.Active(ResearchSystems.Standard).Single() == replacement && queue.ProjectIds[0] == interrupted.defName, "Restored requirements do not interrupt replacement research");

            // Seed measured samples without waiting for real-time research in the disposable colony.
            FieldInfo ratesField = typeof(ResearchQueueWorldComponent).GetField("rates", BindingFlags.NonPublic | BindingFlags.Instance)!;
            List<ResearchRateState> rates = (List<ResearchRateState>)ratesField.GetValue(tracker)!;
            ResearchRateState systemRate = rates.Single(r => r.Id == "System:Standard");
            systemRate.Rates.Clear(); systemRate.Rates.Add(240f);
            Dictionary<string, float> estimates = tracker.Estimates(ResearchSystems.Standard);
            List<ResearchProjectDef> ordered = tracker.Active(ResearchSystems.Standard).Concat(queue.ProjectIds.Select(Project)).Where(ResearchSystems.CanStart).ToList();
            float cumulative = 0f;
            foreach (ResearchProjectDef p in ordered)
            {
                cumulative += (p.CostApparent - p.ProgressApparent) / 240f;
                Check(Math.Abs(estimates[p.defName] - cumulative) < 0.001f, "Cumulative completion estimate: " + p.defName);
            }
            tracker.Snooze(Project("CarpetMaking"));
            tracker.Move(Project(queue.ProjectIds.Last()), 0);
            tracker.ClearNewMarkers();
            Check(queue.NewProjectIds.Count == 0, "Closing-screen marker clearing preserves queue");
            expectedQueue = queue.ProjectIds.ToList();
            expectedSnoozed = queue.SnoozedProjectIds.ToList();
            verifyRoundTrip = true;
            GameDataSaveLoader.SaveGame("RegressionRoundTrip");
            File.AppendAllText(Report, "Saved regression round trip; reloading\n");
            GameDataSaveLoader.LoadGame("RegressionRoundTrip");
        }
        catch (Exception error)
        {
            File.AppendAllText(Report, "FAIL " + error + "\n");
            Log.Error("[Research Queue test fixture] " + error);
        }
    }
}

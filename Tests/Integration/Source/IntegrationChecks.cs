using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ResearchQueue;
using ResearchQueue.Core;
using ResearchQueue.State;
using ResearchQueue.UI;
using RimWorld;
using Verse;

namespace ResearchQueueFixture;

public sealed class IntegrationChecks : GameComponent
{
    private bool ran;
    private bool verifyRoundTrip;
    private List<string> expectedQueue = new();
    private List<string> expectedSnoozed = new();
    private List<string>? renderQueue;
    private MainTabWindow_ResearchQueue? renderWindow;
    private int renderFrames;
    private int renderBuilds;
    private string Report => Path.Combine(GenFilePaths.SaveDataFolderPath, "Integration.txt");
    private static ResearchProjectDef Project(string id) => DefDatabase<ResearchProjectDef>.GetNamed(id);
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)!;
    private static Dictionary<ResearchProjectDef, float> Progress => (Dictionary<ResearchProjectDef, float>)Field(typeof(ResearchManager), "progress").GetValue(Find.ResearchManager)!;
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
        File.AppendAllText(Report, "PASS " + description + "\n");
    }

    private static void Finish(ResearchProjectDef p) => Find.ResearchManager.FinishProject(p, doCompletionLetter: false);

    private static void ClearQueues(ResearchQueueWorldComponent tracker)
    {
        foreach (ResearchProjectDef p in DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(Find.ResearchManager.IsCurrentProject).ToList())
            Find.ResearchManager.StopProject(p);
        foreach (ResearchSystemQueueState q in tracker.Queues)
        {
            q.ProjectIds.Clear(); q.SnoozedProjectIds.Clear(); q.NewProjectIds.Clear(); q.Initialized = false;
        }
    }

    private static void ConfigureNode(string name, object value)
    {
        Type mod = GenTypes.GetTypeInAnyAssembly("BetterResearchMenu.BetterResearchMenuMod")!;
        object settings = Field(mod, "settings").GetValue(null)!;
        Field(settings.GetType(), name).SetValue(settings, value);
    }

    private void Baseline(ResearchQueueWorldComponent tracker)
    {
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        if (ModsConfig.IsActive("ferny.noderesearch")) ConfigureNode("autoOpenMenuOnFinish", false);
        ClearQueues(tracker);
        Find.ResearchManager.ResetAllProgress();
        foreach (string id in new[] { "ComplexClothing", "ComplexFurniture", "Stonecutting", "Electricity" }) Finish(Project(id));
        tracker.RefreshQueues();
        Check(tracker.Active(ResearchSystems.Standard).Count() == 1, "Fresh colony selects research without opening the screen");
        Check(tracker.Queue(ResearchSystems.Standard).ProjectIds.Count > 0, "Fresh colony automatically populates its queue");
    }

    private void Core(ResearchQueueWorldComponent tracker)
    {
        ResearchSystemQueueState queue = tracker.Queue(ResearchSystems.Standard);
        ResearchProjectDef carpet = Project("CarpetMaking");
        tracker.Snooze(carpet);
        Check(!queue.ProjectIds.Contains(carpet.defName) && tracker.IsExcluded(carpet.defName), "Snoozing removes and excludes research");
        tracker.Restore(carpet);
        Check(queue.ProjectIds.Last() == carpet.defName, "Restoration appends to the end");
        queue.ProjectIds.Insert(0, "ResearchQueueTest_MissingDefinition");
        string[] visible = queue.ProjectIds.Where(id => tracker.Resolve(id) != null).ToArray();
        string moved = visible[0];
        tracker.MoveVisible(visible, 0, 2);
        Check(queue.ProjectIds.IndexOf(moved) > queue.ProjectIds.IndexOf(visible[1]), "Real queue drag maps around a retained missing definition");
        Check(queue.ProjectIds[0] == "ResearchQueueTest_MissingDefinition", "Drag preserves missing definition recovery state");
        queue.ProjectIds.Remove("ResearchQueueTest_MissingDefinition");

        ResearchProjectDef bow = Project("RecurveBow");
        foreach (ResearchProjectDef p in tracker.Active(ResearchSystems.Standard).ToList())
        {
            Find.ResearchManager.StopProject(p);
            if (!queue.ProjectIds.Contains(p.defName)) queue.ProjectIds.Add(p.defName);
        }
        if (!queue.ProjectIds.Contains(bow.defName)) queue.ProjectIds.Add(bow.defName);
        tracker.Move(bow, 0); tracker.RefreshQueues();
        Check(tracker.Active(ResearchSystems.Standard).Single() == bow, "Idle selection follows player priorities");
        Find.ResearchManager.SetCurrentProject(carpet);
        Check(tracker.Active(ResearchSystems.Standard).Single() == bow, "Normal selection cannot bypass the queue");

        Field(typeof(ResearchQueueWorldComponent), "nextRateTick").SetValue(tracker, 0);
        tracker.WorldComponentTick();
        float before = bow.ProgressReal;
        Find.ResearchManager.AddProgress(bow, 24f);
        for (int i = 0; i < GenDate.TicksPerHour; i++) Find.TickManager.DoSingleTick();
        List<ResearchRateState> rates = (List<ResearchRateState>)Field(typeof(ResearchQueueWorldComponent), "rates").GetValue(tracker)!;
        ResearchRateState rate = rates.Single(r => r.Id == bow.defName);
        float expected = (bow.ProgressReal - before) * bow.CostFactor(Faction.OfPlayer.def.techLevel) * 24f;
        Check(Math.Abs(rate.Rates.Last() - expected) < 0.001f && expected > 0f, "Native hourly sampling records real progress deltas");
        for (int i = 0; i < GenDate.TicksPerHour; i++) Find.TickManager.DoSingleTick();
        Check(rate.Rates.Last() == 0f && rate.Rates.Any(v => v > 0f), "Native hourly sampling retains idle hours alongside measured work");
        Check(tracker.Estimates(ResearchSystems.Standard)[bow.defName] > 0f, "Measured history produces a completion estimate");

        tracker.Move(Project("Hydroponics"), 0);
        List<string> order = queue.ProjectIds.ToList();
        Finish(bow); Find.TickManager.DoSingleTick();
        Check(tracker.Active(ResearchSystems.Standard).Single().defName == order[0], "Completion automatically starts the next priority");
        Check(queue.ProjectIds.Take(order.Count - 1).SequenceEqual(order.Skip(1)), "Completion retains existing order before appending unlocks");
        Check(queue.ProjectIds.Contains("Greatbow"), "Completion automatically appends newly unlocked research");
        ResearchProjectDef active = tracker.Active(ResearchSystems.Standard).Single();
        Find.ResearchManager.AddProgress(active, 10f);
        tracker.Snooze(active); tracker.Restore(active);
        Check(active.ProgressReal == 10f, "Snoozing current research preserves accumulated progress");

        active = tracker.Active(ResearchSystems.Standard).Single();
        ThingDef? oldBench = active.requiredResearchBuilding;
        active.requiredResearchBuilding = DefDatabase<ThingDef>.GetNamed("HiTechResearchBench");
        try
        {
            tracker.RefreshQueues();
            Check(queue.ProjectIds[0] == active.defName && !Find.ResearchManager.IsCurrentProject(active), "Blocked current research retains priority and yields");
            Check(ResearchSystems.BlockReason(active).Contains(active.requiredResearchBuilding.LabelCap.ToString()), "Bench blocker names the required building");
        }
        finally { active.requiredResearchBuilding = oldBench; }
        ResearchProjectDef replacement = tracker.Active(ResearchSystems.Standard).Single();
        tracker.RefreshQueues();
        Check(tracker.Active(ResearchSystems.Standard).Single() == replacement, "Restored requirements do not interrupt replacement research");

        bool mechanitor = active.requiresMechanitor;
        bool inspection = active.requireGravEngineInspected;
        try
        {
            active.requiresMechanitor = true;
            if (ModsConfig.BiotechActive) Check(ResearchSystems.BlockReason(active).Contains("MissingRequiredMechanitor".Translate()), "Mechanitor blocker gives the native requirement");
            active.requireGravEngineInspected = true;
            Find.ResearchManager.gravEngineInspected = false;
            if (ModsConfig.OdysseyActive) Check(ResearchSystems.BlockReason(active).Contains("MissingGravEngineInspection".Translate()), "Inspection blocker gives the native requirement");
        }
        finally { active.requiresMechanitor = mechanitor; active.requireGravEngineInspected = inspection; }

        List<ThingDef> oldFacilities = active.requiredResearchFacilities;
        oldBench = active.requiredResearchBuilding;
        ThingDef analyzer = DefDatabase<ThingDef>.GetNamed("MultiAnalyzer");
        try
        {
            active.requiredResearchBuilding = DefDatabase<ThingDef>.GetNamed("HiTechResearchBench");
            active.requiredResearchFacilities = new List<ThingDef> { analyzer };
            Check(ResearchSystems.BlockReason(active).Contains(analyzer.LabelCap.ToString()), "Facility blocker names the required linked facility");
        }
        finally { active.requiredResearchBuilding = oldBench; active.requiredResearchFacilities = oldFacilities; }
        ResearchProjectDef? analyzed = DefDatabase<ResearchProjectDef>.AllDefsListForReading.FirstOrDefault(p => !p.AnalyzedThingsRequirementsMet);
        if (analyzed != null)
            Check(ResearchSystems.BlockReason(analyzed).Contains(analyzed.requiredAnalyzed[0].LabelCap.ToString()), "Analysis blocker names the required analyzed items");

        tracker.Snooze(carpet);
        tracker.Move(Project(queue.ProjectIds.Last()), 0);
        expectedQueue = queue.ProjectIds.ToList(); expectedSnoozed = queue.SnoozedProjectIds.ToList();
        verifyRoundTrip = true;
        GameDataSaveLoader.SaveGame("RegressionRoundTrip");
        GameDataSaveLoader.LoadGame("RegressionRoundTrip");
    }

    private void Reload(ResearchQueueWorldComponent tracker)
    {
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        ResearchSystemQueueState queue = tracker.Queue(ResearchSystems.Standard);
        Check(queue.ProjectIds.SequenceEqual(expectedQueue), "Player queue order survives reload");
        Check(queue.SnoozedProjectIds.SequenceEqual(expectedSnoozed), "Explicit snoozes survive reload");
        Check(tracker.Snoozed(ResearchSystems.Standard).Any(p => p.defName == "CarpetMaking"), "Snoozed view is ready before a post-load tick");
        Check(tracker.Estimates(ResearchSystems.Standard).Values.Any(v => v > 0f), "Measured rate history survives reload");
        tracker.Restore(Project("CarpetMaking"));
        Check(queue.ProjectIds.Last() == "CarpetMaking", "Restoration after loading appends to the end");
        if (ModsConfig.AnomalyActive)
        {
            ResearchProjectDef standard = tracker.Active(ResearchSystems.Standard).Single();
            Field(typeof(GameComponent_Anomaly), "highestLevelReached").SetValue(Find.Anomaly, 1);
            tracker.RefreshQueues();
            ResearchProjectDef basic = tracker.Active("Basic").Single();
            tracker.Snooze(basic); tracker.Restore(basic);
            Check(tracker.Active(ResearchSystems.Standard).Single() == standard, "Anomaly operations preserve Standard research");
        }
        BeginRenderTest(tracker);
    }

    private void Node(ResearchQueueWorldComponent tracker)
    {
        Faction.OfPlayer.def.techLevel = TechLevel.Neolithic;
        ConfigureNode("restrictResearchToTechLevel", true);
        ConfigureNode("enableEmergence", true);
        Type settingsType = GenTypes.GetTypeInAnyAssembly("BetterResearchMenu.BetterResearchMenuSettings")!;
        ConfigureNode("advancementTiedTo", Enum.Parse(Field(settingsType, "advancementTiedTo").FieldType, "Foundations"));
        ResearchProjectDef emergence = Project("BRM_Emergence_Medieval");
        foreach (ResearchProjectDef p in DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(p => p.defName.StartsWith("BRM_Emergence_") && p.techLevel < TechLevel.Neolithic))
            Progress[p] = p.Cost;
        List<ResearchProjectDef> foundations = DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(p => p.techLevel == TechLevel.Neolithic && p.modExtensions?.Any(e => e.GetType().Name == "ResearchFoundationExtension") == true).ToList();
        Check(foundations.Count > 0, "Installed Node Research supplies Neolithic foundations");
        foreach (ResearchProjectDef p in foundations) Progress[p] = 0f;
        Check(!ResearchSystems.CanStart(emergence), "Node emergence waits for foundations");
        Check(ResearchSystems.BlockReason(emergence).Contains("foundation"), "Node blocker explains foundation progress");
        Type state = GenTypes.GetTypeInAnyAssembly("BetterResearchMenu.State")!;
        List<string> legacy = (List<string>)Field(state, "researchQueue").GetValue(null)!;
        tracker.Queue(ResearchSystems.Standard).ProjectIds.Remove("GeothermalPower");
        legacy.Add("Pemmican"); legacy.Add("GeothermalPower");
        tracker.RefreshQueues();
        Check(legacy.Count == 0, "Node legacy queue is drained after handover");
        legacy.Add("TreeSowing");
        foreach (ResearchProjectDef p in foundations) Finish(p);
        Check(legacy.Count == 0, "Completion captures legacy entries before Node can consume them");
        tracker.RefreshQueues();
        Check(ResearchSystems.CanStart(emergence), "Completing foundations unlocks emergence through Node's rules");
        foreach (ResearchProjectDef p in tracker.Active(ResearchSystems.Standard).ToList()) tracker.Snooze(p);
        tracker.Move(emergence, 0);
        foreach (ResearchProjectDef p in tracker.Active(ResearchSystems.Standard).ToList())
        {
            Find.ResearchManager.StopProject(p);
            if (!tracker.Queue(ResearchSystems.Standard).ProjectIds.Contains(p.defName)) tracker.Queue(ResearchSystems.Standard).ProjectIds.Add(p.defName);
        }
        tracker.RefreshQueues();
        Check(tracker.Active(ResearchSystems.Standard).Single() == emergence, "Queue automatically starts prioritized emergence");
        Find.ResearchManager.AddProgress(emergence, emergence.Cost);
        Check(Faction.OfPlayer.def.techLevel == TechLevel.Medieval, "Research completion advances the era through Node");
        Faction.OfPlayer.def.techLevel = TechLevel.Industrial;
        tracker.RefreshQueues();
        Check(tracker.Queue(ResearchSystems.Standard).ProjectIds.Contains("GeothermalPower") || Find.ResearchManager.IsCurrentProject(Project("GeothermalPower")), "Deferred Node entry returns when its era becomes eligible");
        Check(tracker.Queue(ResearchSystems.Standard).NewProjectIds.Contains("GeothermalPower"), "Newly eligible deferred import receives the New marker");
    }

    private void Gravtech(ResearchQueueWorldComponent tracker)
    {
        Faction.OfPlayer.def.techLevel = TechLevel.Industrial;
        List<ResearchProjectDef> grav = DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(p => ResearchSystems.Key(p) == ResearchSystems.Gravship).ToList();
        Check(grav.Count > 1, "Installed VGE supplies a separate research system");
        foreach (ResearchProjectDef p in grav.SelectMany(ResearchSystems.Prerequisites).Distinct().Where(p => ResearchSystems.Key(p) != ResearchSystems.Gravship)) Finish(p);
        Find.ResearchManager.gravEngineInspected = true;
        tracker.RefreshQueues();
        ResearchProjectDef standard = tracker.Active(ResearchSystems.Standard).Single();
        ResearchProjectDef first = tracker.Active(ResearchSystems.Gravship).Single();
        Check(ResearchSystems.CanStart(first), "Gravtech automatically selects eligible research without a direct Start call");
        Finish(first); Find.TickManager.DoSingleTick();
        ResearchProjectDef next = tracker.Active(ResearchSystems.Gravship).Single();
        Check(next != first && first.IsFinished, "Gravtech automatically advances on completion");
        Check(tracker.Active(ResearchSystems.Standard).Single() == standard, "Gravtech completion preserves Standard research");
        tracker.Snooze(next); tracker.Restore(next);
        Check(tracker.Active(ResearchSystems.Standard).Single() == standard, "Gravtech snoozing and restoration preserve Standard research");
    }

    private void BeginRenderTest(ResearchQueueWorldComponent tracker)
    {
        ResearchSystemQueueState queue = tracker.Queue(ResearchSystems.Standard);
        renderQueue = queue.ProjectIds.ToList();
        string id = renderQueue.First(id => tracker.Resolve(id) != null);
        queue.ProjectIds.Clear(); queue.ProjectIds.AddRange(Enumerable.Repeat(id, 1000));
        tracker.Move(Project(id), 0);
        Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Research);
        renderWindow = (MainTabWindow_ResearchQueue)MainButtonDefOf.Research.TabWindow;
        ran = false;
    }

    public override void GameComponentUpdate()
    {
        if (!GenCommandLine.CommandLineArgPassed("researchQueueTests") || ran || Current.ProgramState != ProgramState.Playing || LongEventHandler.AnyEventNowOrWaiting) return;
        try
        {
            ResearchQueueWorldComponent tracker = ResearchQueueWorldComponent.Get()!;
            if (renderWindow != null)
            {
                if (renderWindow.LastDrawnRowCount == 0) return;
                if (renderFrames++ == 0) renderBuilds = renderWindow.ViewBuildCount;
                if (renderFrames < 4) return;
                Check(renderWindow.LastDrawnRowCount > 0 && renderWindow.LastDrawnRowCount < 1000, "Actual queue rendering limits a thousand-row view to its viewport");
                Check(renderWindow.ViewBuildCount == renderBuilds, "Repeated GUI passes reuse the cached queue view");
                ResearchSystemQueueState queue = tracker.Queue(ResearchSystems.Standard);
                queue.ProjectIds.Clear(); queue.ProjectIds.AddRange(renderQueue!);
                tracker.RefreshQueues();
                File.AppendAllText(Report, "COMPLETE\n"); ran = true;
                return;
            }
            ran = true;
            if (verifyRoundTrip) { Reload(tracker); return; }
            File.WriteAllText(Report, "Self-contained Research Queue integration checks\n");
            Baseline(tracker);
            if (ModsConfig.IsActive("ferny.noderesearch")) Node(tracker);
            if (ModsConfig.IsActive("vanillaexpanded.gravship")) Gravtech(tracker);
            if (ModsConfig.IsActive("ferny.noderesearch") || ModsConfig.IsActive("vanillaexpanded.gravship")) BeginRenderTest(tracker);
            else Core(tracker);
        }
        catch (Exception error)
        {
            ran = true;
            File.AppendAllText(Report, "FAIL " + error + "\n");
            Log.Error("[Research Queue test fixture] " + error);
        }
    }
}

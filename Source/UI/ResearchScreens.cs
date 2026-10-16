using System;
using System.Reflection;
using HarmonyLib;
using ResearchQueue.Research;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ResearchQueue.UI
{
    // Switches between this window and the vanilla or Node Research trees, which open as plain windows for browsing.
    // The Research tab itself always stays Research Queue, so the button and F6 come back here.
    [StaticConstructorOnStartup]
    public static class ResearchScreens
    {
        private const string NodeWindowTypeName = "BetterResearchMenu.MainTabWindow_BetterResearch";
        // Node Research's own icons, present only when it is loaded.
        private const string NodeTreeIconPath = "UI/TreeToggle";
        private const string NodeLogoIconPath = "UI/TechLevels/AllTab";

        public static readonly Type? NodeWindowType = NodeResearch.Active ? AccessTools.TypeByName(NodeWindowTypeName) : null;
        // Node collapses every project onto its own tab; Restore puts the vanilla tabs back, as its own vanilla button does.
        private static readonly MethodInfo? NodeRestoreTabs = NodeResearch.Active ? AccessTools.Method("BetterResearchMenu.Startup:Restore") : null;

        public static readonly Texture2D VanillaIcon = LoadNodeIcon(NodeTreeIconPath) ?? ContentFinder<Texture2D>.Get("UI/ResearchQueue/VanillaTree");
        public static readonly Texture2D? NodeIcon = NodeWindowType != null ? LoadNodeIcon(NodeLogoIconPath) : null;
        public static readonly Texture2D QueueIcon = ContentFinder<Texture2D>.Get("UI/ResearchQueue/ResearchQueue");

        private static Texture2D? LoadNodeIcon(string path) => NodeResearch.Active ? ContentFinder<Texture2D>.Get(path, false) : null;

        public static void OpenVanilla(Window current)
        {
            current.Close(false);
            NodeRestoreTabs?.Invoke(null, null);
            Find.WindowStack.Add(new MainTabWindow_Research { def = MainButtonDefOf.Research });
            SoundDefOf.TabOpen.PlayOneShotOnCamera();
        }

        public static void OpenNode(Window current)
        {
            if (NodeWindowType == null || !(Activator.CreateInstance(NodeWindowType) is MainTabWindow node)) return;
            current.Close(false);
            node.def = MainButtonDefOf.Research;
            Find.WindowStack.Add(node);
            SoundDefOf.TabOpen.PlayOneShotOnCamera();
        }

        public static void OpenQueue(Window current)
        {
            current.Close(false);
            Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Research, false);
            SoundDefOf.TabOpen.PlayOneShotOnCamera();
        }
    }
}

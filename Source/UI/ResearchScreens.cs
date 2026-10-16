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

        public static readonly Type? NodeWindowType = NodeResearch.Active ? AccessTools.TypeByName(NodeWindowTypeName) : null;
        // Node collapses every project onto its own tab; Restore puts the vanilla tabs back, as its own vanilla button does.
        private static readonly MethodInfo? NodeRestoreTabs = NodeResearch.Active ? AccessTools.Method("BetterResearchMenu.Startup:Restore") : null;

        public static readonly Texture2D VanillaIcon = ContentFinder<Texture2D>.Get("UI/ResearchQueue/VanillaTree");
        // Null without Node Research, which hides its button.
        public static readonly Texture2D? NodeIcon = NodeWindowType != null ? ContentFinder<Texture2D>.Get("UI/ResearchQueue/NodeResearch") : null;
        public static readonly Texture2D QueueIcon = ContentFinder<Texture2D>.Get("UI/ResearchQueue/ResearchQueue");

        // One window of each kind for the session, reopened each time so each tree keeps its scroll, zoom and tab.
        private static MainTabWindow_Research? vanillaWindow;
        private static MainTabWindow? nodeWindow;

        public static void OpenVanilla(Window current)
        {
            current.Close(false);
            NodeRestoreTabs?.Invoke(null, null);
            vanillaWindow ??= new MainTabWindow_Research { def = MainButtonDefOf.Research };
            Show(vanillaWindow);
        }

        public static void OpenNode(Window current)
        {
            if (NodeWindowType == null) return;
            nodeWindow ??= Activator.CreateInstance(NodeWindowType) as MainTabWindow;
            if (nodeWindow == null) return;
            current.Close(false);
            nodeWindow.def = MainButtonDefOf.Research;
            Show(nodeWindow);
        }

        private static void Show(Window window)
        {
            if (!Find.WindowStack.IsOpen(window)) Find.WindowStack.Add(window);
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

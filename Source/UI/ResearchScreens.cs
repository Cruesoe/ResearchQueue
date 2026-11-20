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

        // Research: Organized's Rethemed window replaces the vanilla tree while it is chosen in that mod's settings, so this button opens it instead.
        private static readonly bool OrganizedActive = ModsConfig.IsActive("cruesoe.research.organized");
        private static readonly Func<Type?>? OrganizedWindowType = OrganizedActive ? StaticDelegate<Func<Type?>>("ResearchOrganized.DisplayMode:get_ActiveWindowType") : null;
        private static readonly Func<MainTabWindow_Research?>? CreateOrganizedWindow = OrganizedActive ? StaticDelegate<Func<MainTabWindow_Research?>>("ResearchOrganized.DisplayMode:CreateWindow") : null;

        private static T? StaticDelegate<T>(string name) where T : Delegate
        {
            var method = AccessTools.Method(name);
            if (method == null || !method.IsStatic) return null;
            try { return (T)Delegate.CreateDelegate(typeof(T), method); }
            catch (ArgumentException) { return null; }
        }

        public static string VanillaTipKey => OrganizedWindowType?.Invoke() != null ? "ResearchQueue_OpenOrganizedTip" : "ResearchQueue_OpenVanillaTip";

        public static void OpenVanilla(Window current)
        {
            current.Close(false);
            NodeRestoreTabs?.Invoke(null, null);
            Type wanted = OrganizedWindowType?.Invoke() ?? typeof(MainTabWindow_Research);
            if (vanillaWindow == null || vanillaWindow.GetType() != wanted)
                vanillaWindow = (wanted != typeof(MainTabWindow_Research) ? CreateOrganizedWindow?.Invoke() : null) ?? new MainTabWindow_Research();
            vanillaWindow.def = MainButtonDefOf.Research;
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

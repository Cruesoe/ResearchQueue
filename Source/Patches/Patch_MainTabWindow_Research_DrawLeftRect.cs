using HarmonyLib;
using ResearchQueue.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace ResearchQueue.Patches
{
    // A button in the top right of the vanilla tree's info pane that returns to Research Queue.
    [HarmonyPatch(typeof(MainTabWindow_Research), "DrawLeftRect")]
    public static class Patch_MainTabWindow_Research_DrawLeftRect
    {
        private const float ButtonSize = 32f;
        // Research: Organized draws its 24px settings gear flush in that corner; this button sits 4px left of it.
        private const float OrganizedGearWidth = 28f;
        private static readonly bool OrganizedActive = ModsConfig.IsActive("cruesoe.research.organized");

        [HarmonyPostfix]
        public static void Postfix(Rect leftOutRect, MainTabWindow_Research __instance)
        {
            // Only the plain vanilla window: Research Queue and Node Research subclass it.
            if (__instance.GetType() != typeof(MainTabWindow_Research)) return;
            float right = leftOutRect.xMax - (OrganizedActive ? OrganizedGearWidth : 0f);
            Rect button = new Rect(right - ButtonSize, leftOutRect.yMin, ButtonSize, ButtonSize);
            TooltipHandler.TipRegion(button, "ResearchQueue_OpenQueueTip".Translate());
            if (!Widgets.ButtonImage(button, ResearchScreens.QueueIcon)) return;
            ResearchScreens.OpenQueue(__instance);
            Event.current.Use();
        }
    }
}

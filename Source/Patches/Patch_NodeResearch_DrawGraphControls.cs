using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ResearchQueue.UI;
using UnityEngine;
using Verse;

namespace ResearchQueue.Patches
{
    // Adds a Research Queue button to Node Research's graph controls, after its vanilla-menu button.
    // Patched by hand because Node's window type exists only when Node Research is loaded.
    [StaticConstructorOnStartup]
    public static class Patch_NodeResearch_DrawGraphControls
    {
        private const float ButtonSize = 24f;
        private const float ButtonGap = 8f;

        static Patch_NodeResearch_DrawGraphControls()
        {
            if (ResearchScreens.NodeWindowType == null) return;
            MethodInfo? original = AccessTools.Method(ResearchScreens.NodeWindowType, "DrawGraphControls");
            if (original == null)
            {
                Log.Warning("[Research Queue] Node Research's DrawGraphControls was not found; its Research Queue button is missing.");
                return;
            }
            try
            {
                new Harmony("cruesoe.researchqueue.noderesearch").Patch(original,
                    transpiler: new HarmonyMethod(typeof(Patch_NodeResearch_DrawGraphControls), nameof(Transpiler)),
                    postfix: new HarmonyMethod(typeof(Patch_NodeResearch_DrawGraphControls), nameof(Postfix)));
            }
            catch (Exception e)
            {
                Log.Warning("[Research Queue] Could not add the Research Queue button to Node Research: " + e.Message);
            }
        }

        // Moves Node's settings button one slot right to make room: the first add after the "BRM_OpenVanillaMenu" tooltip.
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool afterVanillaTip = false;
            bool shifted = false;
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (!afterVanillaTip && instruction.opcode == OpCodes.Ldstr && instruction.operand is string text && text == "BRM_OpenVanillaMenu")
                    afterVanillaTip = true;
                else if (afterVanillaTip && !shifted && instruction.opcode == OpCodes.Add)
                {
                    yield return new CodeInstruction(OpCodes.Ldc_R4, ButtonSize + ButtonGap);
                    yield return new CodeInstruction(OpCodes.Add);
                    shifted = true;
                }
            }
        }

        // Node's buttons are 24px with 8px gaps: physics, vanilla menu, then this one in the third slot.
        public static void Postfix(Rect controlAreaRect, Window __instance)
        {
            Rect button = new Rect(controlAreaRect.x + (ButtonSize + ButtonGap) * 2f, controlAreaRect.y, ButtonSize, ButtonSize);
            TooltipHandler.TipRegion(button, "ResearchQueue_OpenQueueTip".Translate());
            if (!Widgets.ButtonImage(button, ResearchScreens.QueueIcon)) return;
            ResearchScreens.OpenQueue(__instance);
            Event.current.Use();
        }
    }
}

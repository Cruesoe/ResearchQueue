using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ResearchQueue.Research
{
    // Optional Medieval Overhaul support; inert without it. Its schematic check caches one answer for every project for 250 ticks,
    // so checking many projects in a row gives them all the first one's answer. The cache is cleared whenever CanStartNow moves to another schematic project.
    [StaticConstructorOnStartup]
    public static class MedievalOverhaul
    {
        private static readonly HashSet<ResearchProjectDef> schematicProjects = new HashSet<ResearchProjectDef>();
        private static FieldInfo? cacheStaleAfterTicks;
        private static ResearchProjectDef? lastProject;

        static MedievalOverhaul()
        {
            Type? canStartNowPatch = AccessTools.TypeByName("MedievalOverhaul.Patches.ResearchProjectsDefs+ResearchProjectDef_CanStartNow");
            Type? requiredSchematic = AccessTools.TypeByName("MedievalOverhaul.RequiredSchematic");
            if (canStartNowPatch == null || requiredSchematic == null) return;

            cacheStaleAfterTicks = AccessTools.Field(canStartNowPatch, "cacheStaleAfterTicks");
            if (cacheStaleAfterTicks == null || cacheStaleAfterTicks.FieldType != typeof(int))
            {
                Log.Warning("[Research Queue] Medieval Overhaul's schematic cache was not found; schematic research may be queued or blocked wrongly.");
                return;
            }

            foreach (ResearchProjectDef project in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                if (project.modExtensions == null) continue;
                foreach (DefModExtension extension in project.modExtensions)
                {
                    if (!requiredSchematic.IsInstanceOfType(extension)) continue;
                    schematicProjects.Add(project);
                    break;
                }
            }
            if (schematicProjects.Count == 0) return;

            new Harmony("cruesoe.researchqueue.medievaloverhaul").Patch(
                AccessTools.PropertyGetter(typeof(ResearchProjectDef), nameof(ResearchProjectDef.CanStartNow)),
                prefix: new HarmonyMethod(typeof(MedievalOverhaul), nameof(CanStartNowPrefix)));
        }

        private static void CanStartNowPrefix(ResearchProjectDef __instance)
        {
            if (__instance == lastProject || !schematicProjects.Contains(__instance)) return;
            lastProject = __instance;
            cacheStaleAfterTicks!.SetValue(null, -1);
        }
    }
}

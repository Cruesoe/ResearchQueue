using HarmonyLib;
using ResearchQueue.UI;
using RimWorld;
using Verse;

namespace ResearchQueue
{
    [StaticConstructorOnStartup]
    public static class ResearchQueueStartup
    {
        private static readonly AccessTools.FieldRef<MainButtonDef, MainTabWindow> TabWindow =
            AccessTools.FieldRefAccess<MainButtonDef, MainTabWindow>("tabWindowInt");

        // Deferred until every static constructor has run, since Node Research claims the Research tab in its own.
        static ResearchQueueStartup()
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                MainButtonDef research = MainButtonDefOf.Research;
                research.tabWindowClass = typeof(MainTabWindow_ResearchQueue);
                TabWindow(research) = null!;
            });
        }
    }
}

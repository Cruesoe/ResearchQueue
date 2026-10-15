using RimWorld;
using Verse;

namespace ResearchQueue.State
{
    // The only path allowed through the SetCurrentProject gate.
    public static class ResearchSelection
    {
        public static bool Applying { get; private set; }

        public static void Start(ResearchProjectDef project)
        {
            Applying = true;
            try { Find.ResearchManager.SetCurrentProject(project); }
            finally { Applying = false; }
        }
    }
}

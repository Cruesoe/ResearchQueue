using Verse;

namespace ResearchQueue.UI
{
    public sealed class QueueViewEntry
    {
        public ResearchProjectDef Project { get; }
        public int? Position { get; }

        public QueueViewEntry(ResearchProjectDef project, int? position)
        {
            Project = project;
            Position = position;
        }
    }
}

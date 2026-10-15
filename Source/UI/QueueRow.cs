using System;
using UnityEngine;
using Verse;

namespace ResearchQueue.UI
{
    // Everything a card needs, worked out once per rebuild rather than on every GUI event.
    public sealed class QueueRow
    {
        public ResearchProjectDef Project { get; }
        public int Position { get; }
        public bool Blocked { get; }
        public bool IsNew { get; }
        // Estimate, or a short state such as Blocked; shown in the card's last column.
        public string Status { get; }
        public bool StatusWarns { get; }
        public string Label { get; }
        public string Tag { get; }
        public string Cost { get; }
        public float CostWidth { get; }
        public Color Color { get; }
        public Func<string> Tooltip { get; }

        public QueueRow(ResearchProjectDef project, int position, bool blocked, bool isNew, string status, bool statusWarns, Func<string> tooltip)
        {
            Project = project;
            Position = position;
            Blocked = blocked;
            IsNew = isNew;
            Status = status;
            StatusWarns = statusWarns;
            Tooltip = tooltip;
            Label = ResearchCard.Label(project);
            Tag = Research.ResearchCatalog.Tag(project);
            Cost = ResearchCard.Cost(project);
            CostWidth = Mathf.Max(65f, Text.CalcSize(Cost).x + 12f);
            Color = ResearchCard.ColorFor(project);
        }
    }
}

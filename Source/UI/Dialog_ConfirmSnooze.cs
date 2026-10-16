using System;
using System.Collections.Generic;
using RimWorld;
using ResearchQueue.Research;
using UnityEngine;
using Verse;

namespace ResearchQueue.UI
{
    // Vanilla confirmation size, listing the projects a snooze would block as cards; Enter confirms, Escape goes back.
    public sealed class Dialog_ConfirmSnooze : Window
    {
        private const float Width = 640f;
        private const float TitleHeight = 42f;
        private const float ButtonHeight = 35f;

        private readonly ResearchProjectDef project;
        private readonly List<ResearchProjectDef> blocked;
        private readonly Action onConfirm;
        private readonly string body;
        private readonly float[] cardY;
        private readonly float listHeight;
        private Vector2 scroll;

        public Dialog_ConfirmSnooze(ResearchProjectDef project, List<ResearchProjectDef> blocked, Action onConfirm)
        {
            this.project = project;
            this.blocked = blocked;
            this.onConfirm = onConfirm;
            body = "ResearchQueue_ConfirmSnooze".Translate();
            // Projects arrive sorted by era; each new era starts after a wider gap.
            cardY = new float[blocked.Count];
            float next = 0f;
            for (int i = 0; i < blocked.Count; i++)
            {
                if (i > 0) next += ResearchCard.GapBefore(blocked, i);
                cardY[i] = next;
                next += ResearchCard.Height;
            }
            listHeight = next;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        // Vanilla 640 x 460 at least, taller to fit every card; scrolls only when the screen can't fit them.
        public override Vector2 InitialSize
        {
            get
            {
                Text.Font = GameFont.Small;
                float bodyHeight = Text.CalcHeight(body, Width - Margin * 2f);
                float height = Margin * 2f + TitleHeight + 4f + bodyHeight + 10f + listHeight + 14f + ButtonHeight;
                return new Vector2(Width, Mathf.Clamp(height, 460f, Verse.UI.screenHeight - 80f));
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleHeight), "ResearchQueue_ConfirmSnoozeTitle".Translate(ResearchCatalog.Label(project)));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            float y = inRect.y + TitleHeight + 4f;
            float bodyHeight = Text.CalcHeight(body, inRect.width);
            Widgets.Label(new Rect(inRect.x, y, inRect.width, bodyHeight), body);
            y += bodyHeight + 10f;

            Rect outRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - ButtonHeight - 14f - y);
            bool scrolls = listHeight > outRect.height;
            Rect view = new Rect(0f, 0f, outRect.width - (scrolls ? ResearchCard.ScrollReserve : 0f), listHeight);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            for (int i = 0; i < blocked.Count; i++)
                ResearchCard.DrawCompact(new Rect(0f, cardY[i], view.width, ResearchCard.Height), blocked[i], false);
            Widgets.EndScrollView();

            float buttonWidth = (inRect.width - 20f) / 2f;
            Rect back = new Rect(inRect.x, inRect.yMax - ButtonHeight, buttonWidth, ButtonHeight);
            if (Widgets.ButtonText(back, "GoBack".Translate())) Close();
            if (Widgets.ButtonText(new Rect(back.xMax + 20f, back.y, buttonWidth, ButtonHeight), "ResearchQueue_Snooze".Translate())) Confirm();
        }

        public override void OnAcceptKeyPressed() => Confirm();

        private void Confirm()
        {
            onConfirm();
            Close();
        }
    }
}

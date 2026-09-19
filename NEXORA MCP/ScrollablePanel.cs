namespace NEXORA_MCP
{
    /// <summary>
    /// ScrollablePanel — a plain container that manually positions its child
    /// cards by a scroll offset. NO native AutoScroll / scrollbars are used:
    /// WinForms' internal scroll range breaks when scrollbars are hidden
    /// (VerticalScroll.Maximum stays ~30 even with 1456px of content), which
    /// made every AutoScroll-based approach unable to scroll. Here the wheel
    /// handler just changes the offset and repositions the children — fully
    /// deterministic.
    /// </summary>
    public class ScrollablePanel : Panel
    {
        private int _contentHeight;
        private int _offset;

        public ScrollablePanel()
        {
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Resize += (_, _) => ScrollByPixels(0);
        }

        /// <summary>Declares the full scrolling content height in pixels.</summary>
        public void SetContentHeight(int height)
        {
            _contentHeight = Math.Max(0, height);
            ScrollByPixels(0);
        }

        /// <summary>Scrolls content vertically by dy pixels (positive = down).</summary>
        public void ScrollByPixels(int dy)
        {
            if (!IsHandleCreated) return;

            int maxOffset = Math.Max(0, _contentHeight - ClientSize.Height);
            int next = Math.Clamp(_offset + dy, 0, maxOffset);
            WheelDiag.Log($"ScrollByPixels: dy={dy} from={_offset} to={next} max={maxOffset} contentH={_contentHeight} clientH={ClientSize.Height}");
            if (next == _offset) return;

            _offset = next;
            RepositionChildren();
        }

        /// <summary>Adds a card at its logical (unscrolled) Y so it follows scrolling.</summary>
        public void AddCard(ActivityLogCard card, int baseY)
        {
            card.Tag = baseY;
            card.Location = new Point(card.Location.X, baseY - _offset);
            Controls.Add(card);
        }

        private void RepositionChildren()
        {
            foreach (Control c in Controls)
            {
                if (c is ActivityLogCard card && card.Tag is int baseY)
                {
                    card.Location = new Point(card.Location.X, baseY - _offset);
                }
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Premium thin scrollbar (6px, overlay right).
            int maxOffset = Math.Max(0, _contentHeight - ClientSize.Height);
            if (maxOffset <= 0 || ClientSize.Height <= 0) return;
            float trackH = ClientSize.Height - 8;
            float thumbH = Math.Max(32, trackH * ClientSize.Height / Math.Max(1, _contentHeight));
            float thumbY = 4 + (trackH - thumbH) * _offset / maxOffset;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var trackBrush = new SolidBrush(Color.FromArgb(255, 20, 20, 20));
            g.FillRectangle(trackBrush, Width - 8, 4, 5, trackH);
            using var thumbBrush = new SolidBrush(UITheme.BorderHover);
            using var thumbPath = UITheme.RoundedRect(new Rectangle(Width - 8, (int)thumbY, 5, (int)thumbH), 3);
            g.FillPath(thumbBrush, thumbPath);
        }
    }
}
using System.Drawing.Drawing2D;

namespace NEXORA_MCP
{
    /// <summary>
    /// Compact activity-log card showing tool name, timestamp, status badge,
    /// and truncated input preview. Hover highlights + click to expand.
    /// </summary>
    public class ActivityLogCard : Control
    {
        public string ToolName { get; set; } = "";
        public string Timestamp { get; set; } = "";
        public string StatusLabel { get; set; } = "";
        public bool Success { get; set; }
        public bool IsPending { get; set; }
        public string InputPreview { get; set; } = "";
        public string FullJson { get; set; } = "";
        public string Model { get; set; } = "";

        private bool _hover;

        public ActivityLogCard()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
            Height = 86;
            BackColor = UITheme.Card;

            MouseEnter += (_, _) => { _hover = true; FocusScrollOwner(); Invalidate(); };
            MouseLeave += (_, _) => { _hover = false; Invalidate(); };
        }

        private void FocusScrollOwner()
        {
            Control? p = Parent;
            while (p != null)
            {
                if (p is ScrollablePanel sp && !sp.IsDisposed)
                {
                    // Give the scroll owner keyboard focus: the OS delivers the
                    // wheel only to the focused window, so this guarantees hover-
                    // scrolling works even if a button currently owns focus.
                    if (!sp.Focused) sp.Focus();
                    return;
                }
                p = p.Parent;
            }
        }

        private const int WM_MOUSEWHEEL = 0x020A;

        protected override void WndProc(ref Message m)
        {
            // Route the wheel scroll to the owning ScrollablePanel, so hovering
            // any card scrolls the feed regardless of focus.
            if (m.Msg == WM_MOUSEWHEEL)
            {
                int delta = unchecked((short)((long)m.WParam >> 16));
                Control? p = Parent;
                while (p != null)
                {
                    if (p is ScrollablePanel sp && !sp.IsDisposed)
                    {
                        sp.ScrollByPixels(-(delta / 120) * 50);
                        m.Result = IntPtr.Zero;
                        return;
                    }
                    p = p.Parent;
                }
            }
            base.WndProc(ref m);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            var bodyColor = _hover ? UITheme.BackTertiary : UITheme.BackSecondary;

            // Fill the WHOLE control with the body color so corners outside the
            // rounded path never show the ambient (white) BackColor.
            using (var bgBrush = new SolidBrush(bodyColor))
                g.FillRectangle(bgBrush, 0, 0, Width, Height);

            using (var path = UITheme.RoundedRect(rect, UITheme.RadiusMedium))
            {
                using var fill = new SolidBrush(bodyColor);
                g.FillPath(fill, path);
                using var pen = new Pen(_hover ? UITheme.BorderHover : UITheme.BorderDefault, 1);
                g.DrawPath(pen, path);
            }

            // Status color bar on left — widens on hover (premium micro-interaction).
            Color statusColor = IsPending ? UITheme.Warning : (Success ? UITheme.Success : UITheme.Error);
            int barW = _hover ? 4 : 3;
            using var barBrush = new SolidBrush(statusColor);
            g.FillRectangle(barBrush, 4, 14, barW, Height - 28);

            // Tool name (bold, white)
            using var nameBrush = new SolidBrush(UITheme.TextPrimary);
            g.DrawString(ToolName, UITheme.BodyBold(10), nameBrush, 16, 12);

            // Status badge
            var badgeText = IsPending ? "PENDING" : (Success ? "SUCCESS" : "ERROR");
            using var badgePen = new Pen(statusColor, 1);
            var badgeW = (int)g.MeasureString(badgeText, UITheme.BodyBold(7.5)).Width + 14;
            var badgeRect = new Rectangle(Width - badgeW - 16, 12, badgeW, 20);
            using (var badgePath = UITheme.RoundedRect(badgeRect, 5))
            {
                using var badgeFill = new SolidBrush(Color.FromArgb(40, statusColor.R, statusColor.G, statusColor.B));
                g.FillPath(badgeFill, badgePath);
                g.DrawPath(badgePen, badgePath);
            }
            using (var badgeTextBrush = new SolidBrush(statusColor))
                g.DrawString(badgeText, UITheme.BodyBold(7.5), badgeTextBrush,
                    badgeRect.X + 7, badgeRect.Y + 4);

            // Timestamp (right, small, muted)
            using var timeBrush = new SolidBrush(UITheme.TextSecondary);
            var timeW = g.MeasureString(Timestamp, UITheme.Body(8)).Width;
            g.DrawString(Timestamp, UITheme.Body(8), timeBrush, Width - badgeW - 24 - timeW, 17);

            // Input preview (bottom, muted, truncated)
            using var previewBrush = new SolidBrush(_hover ? UITheme.TextSecondary : UITheme.TextMuted);
            string preview = Truncate(InputPreview, 90);
            var previewRect = new RectangleF(16, 42, Width - 32, 36);
            g.DrawString(preview, UITheme.Body(9), previewBrush, previewRect);

            // Model (tiny, far right bottom)
            if (!string.IsNullOrEmpty(Model))
            {
                using var modelBrush = new SolidBrush(UITheme.TextMuted);
                g.DrawString(Model, UITheme.Body(7.5), modelBrush,
                    Width - (int)g.MeasureString(Model, UITheme.Body(7.5)).Width - 16, 60);
            }

            base.OnPaint(e);
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s[..max] + "...";
        }
    }
}
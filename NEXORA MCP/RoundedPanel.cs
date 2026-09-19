using System.Drawing.Drawing2D;

namespace NEXORA_MCP
{
    /// <summary>Panel with rounded corners, optional border and soft shadow.</summary>
    public class RoundedPanel : Panel
    {
        public int CornerRadius { get; set; } = 12;
        public Color BorderColor { get; set; } = UITheme.BorderDefault;
        public int BorderWidth { get; set; } = 1;
        public Color BackgroundColor { get; set; } = UITheme.BackSecondary;
        public bool DrawShadow { get; set; } = false;
        public bool Hoverable { get; set; } = false;

        private bool _hover;

        public RoundedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Padding = new Padding(UITheme.Space6);
            MouseEnter += (_, _) => { if (Hoverable) { _hover = true; Invalidate(); } };
            MouseLeave += (_, _) => { if (Hoverable) { _hover = false; Invalidate(); } };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.CompositingQuality = CompositingQuality.HighQuality;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            if (DrawShadow)
            {
                for (int i = 4; i >= 1; i--)
                {
                    float alpha = 14f * (5 - i) / 5f;
                    rect.Inflate(i, i);
                    using var path = UITheme.RoundedRect(rect, CornerRadius + i);
                    using var brush = new SolidBrush(Color.FromArgb((int)alpha, 0, 0, 0));
                    g.FillPath(brush, path);
                }
                rect = new Rectangle(0, 0, Width - 1, Height - 1);
            }

            var bg = Hoverable && _hover ? UITheme.BackTertiary : BackgroundColor;
            var border = Hoverable && _hover ? UITheme.BorderHover : BorderColor;
            using (var path = UITheme.RoundedRect(rect, CornerRadius))
            {
                using var fill = new SolidBrush(bg);
                g.FillPath(fill, path);

                // Premium: 1px hairline + subtle top highlight for depth.
                if (BorderWidth > 0)
                {
                    using var pen = new Pen(border, BorderWidth);
                    g.DrawPath(pen, path);
                }
                using var hiPen = new Pen(Color.FromArgb(14, 255, 255, 255), 1f);
                g.DrawLine(hiPen, rect.X + CornerRadius, rect.Y + 1, rect.Right - CornerRadius, rect.Y + 1);
            }

            base.OnPaint(e);
        }
    }
}
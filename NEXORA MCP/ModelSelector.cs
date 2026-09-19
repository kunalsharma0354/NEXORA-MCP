using System.Drawing.Drawing2D;

namespace NEXORA_MCP
{
    /// <summary>
    /// Premium model selector trigger. The dropdown list itself is still
    /// owned by DashboardForm (to preserve existing logic); this control
    /// provides a polished Linear-style trigger button with chevron,
    /// hover/focus states and a loading shimmer.
    /// </summary>
    public class ModelSelector : Control
    {
        private bool _hover;
        private bool _open;
        private bool _loading = true;
        private string _display = "Select model";

        public event EventHandler? TriggerClicked;

        public string DisplayText
        {
            get => _display;
            set { _display = value; Invalidate(); }
        }

        public bool DropdownOpen
        {
            get => _open;
            set { _open = value; Invalidate(); }
        }

        public bool Loading
        {
            get => _loading;
            set { _loading = value; Invalidate(); }
        }

        public ModelSelector()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            Size = new Size(280, 40);
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.DropList;
            AccessibleName = "AI model selector";

            MouseEnter += (_, _) => { _hover = true; Invalidate(); };
            MouseLeave += (_, _) => { _hover = false; Invalidate(); };
            Click += (_, _) => TriggerClicked?.Invoke(this, EventArgs.Empty);
            GotFocus += (_, _) => Invalidate();
            LostFocus += (_, _) => Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            var bg = _open ? UITheme.BackTertiary : _hover ? UITheme.BackTertiary : UITheme.BackSecondary;
            var border = _open || Focused ? UITheme.BorderFocus : _hover ? UITheme.BorderHover : UITheme.BorderDefault;

            using (var path = UITheme.RoundedRect(rect, UITheme.RadiusSmall + 3))
            {
                using var fill = new SolidBrush(bg);
                g.FillPath(fill, path);
                using var pen = new Pen(border, 1f);
                g.DrawPath(pen, path);
            }

            if (Focused)
                UITheme.DrawFocusRing(g, rect, 2);

            // Leading dot.
            bool hasSelection = !_loading && _display != "Select model";
            var dotColor = hasSelection ? UITheme.Success : UITheme.TextMuted;
            g.FillEllipse(new SolidBrush(dotColor), 14, Height / 2 - 3, 6, 6);

            // Text.
            var textColor = _loading ? UITheme.TextMuted : hasSelection ? UITheme.TextPrimary : UITheme.TextSecondary;
            using var textBrush = new SolidBrush(textColor);
            var textRect = new RectangleF(28, 0, Width - 64, Height);
            using var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
            g.DrawString(_display, UITheme.Body(10f), textBrush, textRect, sf);

            // Chevron (rotates when open).
            int cxx = Width - 20, cyy = Height / 2;
            using var chevBrush = new SolidBrush(UITheme.TextSecondary);
            if (_open)
                g.FillPolygon(chevBrush, new[] { new Point(cxx - 5, cyy + 3), new Point(cxx + 5, cyy + 3), new Point(cxx, cyy - 3) });
            else
                g.FillPolygon(chevBrush, new[] { new Point(cxx - 5, cyy - 3), new Point(cxx + 5, cyy - 3), new Point(cxx, cyy + 3) });

            base.OnPaint(e);
        }
    }
}

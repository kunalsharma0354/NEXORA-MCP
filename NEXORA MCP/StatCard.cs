using System.Drawing.Drawing2D;

namespace NEXORA_MCP
{
    /// <summary>
    /// Stat card with animated counter and optional circular progress ring.
    /// Used for Tool Calls / Success Rate / Avg Latency cards.
    /// </summary>
    public class StatCard : Control
    {
        private string _title = "";
        private string _valueText = "0";
        private Color _accent = UITheme.Success;
        private float _progress;      // 0..1
        private float _animatedProgress;
        private int _counterValue;
        private string _suffix = "";

        public string Title
        {
            get => _title;
            set { _title = value; Invalidate(); }
        }

        public string ValueText
        {
            get => _valueText;
            set { _valueText = value; Invalidate(); }
        }

        public Color Accent
        {
            get => _accent;
            set { _accent = value; Invalidate(); }
        }

        public bool ShowRing { get; set; }

        public float Progress
        {
            get => _progress;
            set
            {
                _progress = Math.Clamp(value, 0f, 1f);
                float from = _animatedProgress;
                AnimationManager.Animate(this, t =>
                {
                    _animatedProgress = from + (_progress - from) * t;
                }, 900, UITheme.EaseOutCubic);
            }
        }

        public void SetCounter(int value, string suffix = "")
        {
            _suffix = suffix;
            int from = _counterValue;
            AnimationManager.Animate(this, t =>
            {
                _counterValue = (int)Math.Round(from + (value - from) * t);
                _valueText = _counterValue.ToString() + _suffix;
            }, 800, UITheme.EaseOutCubic);
        }

        public StatCard()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            MinimumSize = new Size(150, 110);
            BackColor = UITheme.BackSecondary;
            Cursor = Cursors.Default;
            MouseEnter += (_, _) => Invalidate();
            MouseLeave += (_, _) => Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.CompositingQuality = CompositingQuality.HighQuality;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Fill the WHOLE control with the card color so the corners outside
            // the rounded path never show the ambient (white) BackColor.
            using (var bgBrush = new SolidBrush(UITheme.BackSecondary))
                g.FillRectangle(bgBrush, 0, 0, Width, Height);

            using (var path = UITheme.RoundedRect(rect, UITheme.RadiusMedium))
            {
                using var fill = new SolidBrush(UITheme.BackSecondary);
                g.FillPath(fill, path);
                bool hover = ClientRectangle.Contains(PointToClient(Cursor.Position));
                using var pen = new Pen(hover ? UITheme.BorderHover : UITheme.BorderDefault, 1);
                g.DrawPath(pen, path);
            }

            // Accent line on top — hairline 2px, inset, premium.
            using (var accentBrush = new SolidBrush(_accent))
                g.FillRectangle(accentBrush, 14, 0, Math.Max(28, Width - 28), 2);

            // Ring (success rate) on right
            if (ShowRing)
            {
                int ringSize = 68;
                int ringX = Width - ringSize - 16;
                int ringY = (Height - ringSize) / 2 + 6;

                // Track
                using (var trackPen = new Pen(UITheme.BorderDefault, 5))
                    g.DrawArc(trackPen, ringX, ringY, ringSize, ringSize, 0, 360);

                // Progress arc (from top)
                using (var progressPen = new Pen(_accent, 5)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                })
                    g.DrawArc(progressPen, ringX, ringY, ringSize, ringSize, -90, 360 * _animatedProgress);

                // Percentage text in center of ring
                using var pctBrush = new SolidBrush(UITheme.TextPrimary);
                using var pctSf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString($"{(int)(_animatedProgress * 100)}%", UITheme.Heading(14),
                    pctBrush, new RectangleF(ringX, ringY, ringSize, ringSize - 8), pctSf);
            }

            // Title
            using (var titleBrush = new SolidBrush(UITheme.TextSecondary))
                g.DrawString(_title, UITheme.BodyBold(8.5), titleBrush, 14, 16);

            // Value
            float valueX = 14;
            float valueY = 38;

            using (var valueBrush = new SolidBrush(UITheme.TextPrimary))
                g.DrawString(_valueText, UITheme.Heading(22), valueBrush, valueX, valueY);

            base.OnPaint(e);
        }
    }
}
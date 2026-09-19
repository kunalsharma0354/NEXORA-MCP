using System.Drawing.Drawing2D;

namespace NEXORA_MCP
{
    public enum ServerState { Active, Inactive, Connecting, Error }

    /// <summary>
    /// Status indicator: pulsing dot + status text. Green pulse while active,
    /// static red when inactive.
    /// </summary>
    public class StatusIndicator : Control
    {
        private ServerState _state = ServerState.Inactive;
        private float _pulseScale = 1f;
        private float _pulseAlpha = 1f;

        public ServerState State
        {
            get => _state;
            set
            {
                if (_state == value) return;
                _state = value;
                Invalidate();
            }
        }

        public string LabelText { get; set; } = "";
        public int DotSize { get; set; } = 10;

        public StatusIndicator()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

            AutoSizeText();
            AccessibleRole = AccessibleRole.StatusBar;

            var timer = new System.Windows.Forms.Timer { Interval = 40 };
            timer.Tick += (_, _) =>
            {
                if (_state != ServerState.Active && _state != ServerState.Connecting) return;
                float t = (Environment.TickCount % 2000) / 2000f;
                _pulseScale = 1.0f + 0.3f * (float)Math.Sin(Math.PI * t);
                _pulseAlpha = 0.55f + 0.45f * (float)Math.Cos(Math.PI * t);
                Invalidate();
            };
            timer.Start();
        }

        private void AutoSizeText()
        {
            AutoSize = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color dotColor = _state switch
            {
                ServerState.Active => UITheme.Success,
                ServerState.Connecting => UITheme.Warning,
                ServerState.Error => UITheme.Error,
                _ => UITheme.Error
            };

            int cx = DotSize / 2 + 8;
            int cy = Height / 2;

            // Premium pill background.
            var pill = new Rectangle(0, 2, Width - 1, Height - 5);
            using (var pillPath = UITheme.RoundedRect(pill, UITheme.RadiusFull))
            {
                using var pillFill = new SolidBrush(Color.FromArgb(255, 24, 24, 24));
                g.FillPath(pillFill, pillPath);
                using var pillPen = new Pen(UITheme.BorderDefault, 1f);
                g.DrawPath(pillPen, pillPath);
            }

            // Pulse ring
            if (_state == ServerState.Active || _state == ServerState.Connecting)
            {
                int ringSize = (int)(DotSize * _pulseScale) + 6;
                using var ringBrush = new SolidBrush(Color.FromArgb(
                    (int)(80 * _pulseAlpha), dotColor.R, dotColor.G, dotColor.B));
                g.FillEllipse(ringBrush,
                    cx - ringSize / 2, cy - ringSize / 2, ringSize, ringSize);
            }

            // Core dot
            using (var dotBrush = new SolidBrush(dotColor))
                g.FillEllipse(dotBrush, cx - DotSize / 2, cy - DotSize / 2, DotSize, DotSize);

            // Label
            if (!string.IsNullOrEmpty(LabelText))
            {
                using var brush = new SolidBrush(UITheme.TextSecondary);
                g.DrawString(LabelText, UITheme.Body(9.5f),
                    brush, cx + DotSize / 2 + 8, Height / 2 - 10);
            }

            base.OnPaint(e);
        }
    }
}
using System.Drawing.Drawing2D;

namespace NEXORA_MCP
{
    public enum ButtonStyle { Primary, Ghost, Danger }

    /// <summary>
    /// Custom button with rounded corners, hover/press scale animation,
    /// and a built-in loading spinner.
    /// </summary>
    public class AnimatedButton : Control
    {
        private ButtonStyle _style = ButtonStyle.Primary;
        private bool _hover;
        private bool _pressed;
        private bool _loading;
        private float _scale = 1f;
        private float _spinnerAngle;
        private System.Windows.Forms.Timer? _spinnerTimer;

        public event EventHandler? Clicked;

        public ButtonStyle Style
        {
            get => _style;
            set { _style = value; Invalidate(); }
        }

        public int CornerRadius { get; set; } = 10;

        public bool IsLoading
        {
            get => _loading;
            set
            {
                _loading = value;
                if (_loading)
                {
                    _spinnerTimer ??= new System.Windows.Forms.Timer { Interval = 16 };
                    _spinnerTimer.Tick += (_, _) =>
                    {
                        _spinnerAngle = (_spinnerAngle + 8) % 360;
                        Invalidate();
                    };
                    _spinnerTimer.Start();
                }
                else
                {
                    _spinnerTimer?.Stop();
                }
                Invalidate();
            }
        }

        public AnimatedButton()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

            Height = 46;
            Cursor = Cursors.Hand;
            TabStop = true;

            MouseEnter += (_, _) => { _hover = true; AnimateScale(1.015f); };
            MouseLeave += (_, _) => { _hover = false; _pressed = false; AnimateScale(1f); };
            MouseDown += (_, _) => { _pressed = true; AnimateScale(0.985f); };
            MouseUp += (_, _) => { _pressed = false; AnimateScale(_hover ? 1.015f : 1f); };
            Click += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);
            GotFocus += (_, _) => Invalidate();
            LostFocus += (_, _) => Invalidate();
        }

        private void AnimateScale(float target)
        {
            AnimationManager.Animate(this, t => { _scale = 1 + (target - 1) * t; }, UITheme.AnimFast, UITheme.EaseOutCubic);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.CompositingQuality = CompositingQuality.HighQuality;

            float w = Width * _scale;
            float h = Height * _scale;
            float x = (Width - w) / 2f;
            float y = (Height - h) / 2f;
            var rect = new RectangleF(x, y, w, h);

            using var path = UITheme.RoundedRect(Rectangle.Round(rect), CornerRadius);

            bool disabled = !Enabled;
            Color bg, fg, border = Color.Transparent;
            bool drawBorder = false;
            switch (_style)
            {
                case ButtonStyle.Primary:
                    bg = disabled ? UITheme.BorderDefault :
                          _pressed ? UITheme.AccentPress :
                          _hover ? UITheme.AccentHover : UITheme.AccentPrimary;
                    fg = disabled ? UITheme.TextMuted : UITheme.TextInverse;
                    break;
                case ButtonStyle.Ghost:
                    bg = disabled ? Color.Transparent :
                          _pressed ? UITheme.CardHover :
                          _hover ? UITheme.BackTertiary : Color.Transparent;
                    fg = disabled ? UITheme.TextMuted : UITheme.TextPrimary;
                    border = _hover ? UITheme.BorderHover : UITheme.Border;
                    drawBorder = true;
                    break;
                case ButtonStyle.Danger:
                    bg = disabled ? UITheme.BorderDefault :
                          _pressed ? Color.FromArgb(190, 50, 50) :
                          _hover ? Color.FromArgb(224, 60, 60) : UITheme.Error;
                    fg = Color.White;
                    break;
                default:
                    bg = Color.White; fg = Color.Black; break;
            }

            using (var brush = new SolidBrush(bg))
                g.FillPath(brush, path);

            if (drawBorder)
            {
                using var borderPen = new Pen(border, 1f);
                g.DrawPath(borderPen, path);
            }

            // Keyboard focus ring — premium 2px white halo.
            if (Focused && !_loading)
                UITheme.DrawFocusRing(g, Rectangle.Round(rect), 2);

            if (_loading)
            {
                DrawSpinner(g, rect);
            }
            else if (!string.IsNullOrEmpty(Text))
            {
                using var textBrush = new SolidBrush(fg);
                using var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(Text, Font, textBrush, rect, sf);
            }

            base.OnPaint(e);
        }

        private void DrawSpinner(Graphics g, RectangleF rect)
        {
            var spinnerSize = 20f;
            var cx = rect.X + rect.Width / 2;
            var cy = rect.Y + rect.Height / 2;
            var spinRect = new RectangleF(cx - spinnerSize / 2, cy - spinnerSize / 2, spinnerSize, spinnerSize);

            Color arcColor = _style == ButtonStyle.Primary ? Color.Black : Color.White;
            using var pen = new Pen(arcColor, 2.5f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawArc(pen, Rectangle.Round(spinRect), _spinnerAngle, 120);
        }
    }
}
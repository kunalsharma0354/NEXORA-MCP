using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace NEXORA_MCP
{
    /// <summary>
    /// ModernTextBox — FINAL.
    ///
    /// Text centering is done by POSITION, not P/Invoke:
    /// a single-line TextBox has a fixed height (= font PreferredHeight, ~21px).
    /// We center that TextBox vertically inside the 52px field, so typed text is
    /// always vertically centered. No multiline, no EM_SETRECTPRO.
    ///
    /// Architecture:
    ///  - UserControl BackColor = Field (black) — seamless behind the TextBox.
    ///  - TextBox (single-line, native height) vertically centered → text centered.
    ///  - Placeholder = SOLID Field-color Label over the TextBox (merges perfectly).
    ///  - Border (rounded) painted in OnPaint on the EXPOSED black background —
    ///    it is never covered because the TextBox only occupies the middle strip.
    ///  - Focus → border animates to white; error → red.
    /// </summary>
    public class ModernTextBox : UserControl
    {
        private readonly TextBox _inner;
        private readonly Label _placeholder;

        private float _borderProgress;
        private bool _focused;
        private bool _hover;
        private bool _hasError;
        private string _placeholderText = "";
        private bool _isPassword;

        public event EventHandler? EnterKeyPressed;

        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string Text
        {
            get => _inner.Text;
            set => _inner.Text = value ?? string.Empty;
        }

        public string PlaceholderText
        {
            get => _placeholderText;
            set { _placeholderText = value ?? ""; if (_placeholder != null) { _placeholder.Text = _placeholderText; UpdatePlaceholder(); } }
        }

        public bool IsPassword
        {
            get => _isPassword;
            set { _isPassword = value; if (_inner != null) _inner.UseSystemPasswordChar = value; }
        }

        public ModernTextBox()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

            // Background = Field color; seamlessly merges with the TextBox.
            BackColor = UITheme.Field;
            Height = 48;
            MinimumSize = new Size(120, 44);
            Margin = new Padding(0);
            Cursor = Cursors.IBeam;
            AccessibleRole = AccessibleRole.Text;
            MouseEnter += (_, _) => { _hover = true; Invalidate(); };
            MouseLeave += (_, _) => { _hover = false; Invalidate(); };

            // Single-line TextBox — its native height (~PreferredHeight) is
            // vertically centered inside the field via LayoutChildren().
            _inner = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = UITheme.Field,
                ForeColor = UITheme.TextPrimary,
                Font = UITheme.Body(11),
                MaxLength = 100,
                Location = new Point(6, 15),
                Size = new Size(Width - 12, 21)
            };
            _inner.GotFocus += (_, _) =>
            {
                _focused = true;
                _hasError = false;
                AnimateBorder(1f);
                UpdatePlaceholder();
            };
            _inner.LostFocus += (_, _) =>
            {
                _focused = false;
                AnimateBorder(0f);
                UpdatePlaceholder();
            };
            _inner.TextChanged += (_, _) => UpdatePlaceholder();
            _inner.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    EnterKeyPressed?.Invoke(this, EventArgs.Empty);
                }
            };

            // Placeholder: SOLID Field-color Label over the TextBox (merges with it).
            _placeholder = new Label
            {
                BackColor = UITheme.Field,
                ForeColor = UITheme.Placeholder,
                Font = UITheme.Body(11),
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor = Cursors.IBeam,
                AutoSize = false,
                TabStop = false,
                Location = new Point(7, 15),
                Size = new Size(Width - 14, 21)
            };
            _placeholder.Click += (_, _) => _inner?.Focus();

            Controls.Add(_inner);
            Controls.Add(_placeholder);

            Click += (_, _) => _inner?.Focus();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutChildren();
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            LayoutChildren();
        }

        private void LayoutChildren()
        {
            if (_inner == null) return;

            int pref = Math.Max(18, _inner.PreferredHeight); // ~21px for 11pt font
            int fieldH = Height;
            int top = Math.Max(2, (fieldH - pref) / 2);

            int innerW = Math.Max(20, Width - 12);
            _inner.Bounds = new Rectangle(6, top, innerW, pref);
            _placeholder.Bounds = new Rectangle(7, top, Math.Max(20, innerW - 2), pref);
        }

        private void UpdatePlaceholder()
        {
            if (_placeholder == null) return;
            bool show = !_focused && string.IsNullOrEmpty(_inner?.Text);
            if (_placeholder.Visible != show)
            {
                _placeholder.Visible = show;
                _placeholder.BringToFront();
            }
        }

        private void AnimateBorder(float target)
        {
            float from = _borderProgress;
            AnimationManager.Animate(this, t =>
            {
                _borderProgress = Math.Clamp(from + (target - from) * t, 0f, 1f);
                Invalidate();
            }, 160, UITheme.EaseOutCubic);
        }

        public void SetError(bool error)
        {
            _hasError = error;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int radius = UITheme.RadiusSmall;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = UITheme.RoundedRect(rect, radius);

            Color borderColor;
            if (_hasError)
                borderColor = UITheme.Error;
            else if (_focused)
                borderColor = UITheme.Lerp(UITheme.Border, UITheme.BorderHi, _borderProgress);
            else if (_hover)
                borderColor = UITheme.BorderHover;
            else
                borderColor = UITheme.Border;

            // Premium focus ring: soft outer white glow at low alpha.
            if ((_focused || _hasError) && _borderProgress > 0.05f)
            {
                using var glowPath = UITheme.RoundedRect(new Rectangle(2, 2, Width - 5, Height - 5), radius);
                using var glowPen = new Pen(Color.FromArgb((int)(28 * _borderProgress), 255, 255, 255), 3f);
                g.DrawPath(glowPen, glowPath);
            }

            // Normal state: thin hairline. Focused/error: 1.5px crisp ring.
            float width = (_focused || _hasError) && _borderProgress > 0.1f ? 1.5f : 1f;
            using var pen = new Pen(borderColor, width);
            g.DrawPath(pen, path);
        }

        public new void Focus() { _inner?.Focus(); base.Focus(); }
    }
}
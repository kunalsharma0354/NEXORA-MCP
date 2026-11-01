using System.Drawing.Drawing2D;

namespace NEXORA_MCP
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  BROWSER-MODE HEADER — a pixel copy of the dashboard's HTML top bar
    //  (web/components/TopBar.jsx + web/styles/globals.css).
    //
    //  Every metric below is transcribed from the CSS:
    //    .topbar           56px tall, #0E0E0E, 1px #2A2A2A bottom border
    //    .topbar-inner     padding 0 16px 0 24px
    //    .brand            gap 10px, dot 8px radius 2, name 16px/600, sub mono 10px
    //    .status-pill      30px tall, radius 999, border #2A2A2A, bg #111111
    //    .selector-trigger 250x34, radius 9, bg #111111
    //    .ghost-btn        34px tall, padding 0 16px, radius 8, bg #181818
    //    .win-btn          30x26, radius 6, icons 10x10 stroke 1.4
    //    .win-divider      1x20, #2A2A2A, margin 0 2px
    // ═══════════════════════════════════════════════════════════════════════════

    internal static class ChromePalette
    {
        public static readonly Color TopBarBg = Color.FromArgb(0x0E, 0x0E, 0x0E);
        public static readonly Color Card = Color.FromArgb(0x11, 0x11, 0x11);
        public static readonly Color Card2 = Color.FromArgb(0x18, 0x18, 0x18);
        public static readonly Color Hover = Color.FromArgb(0x1E, 0x1E, 0x1E);
        public static readonly Color Border = Color.FromArgb(0x2A, 0x2A, 0x2A);
        public static readonly Color BorderHi = Color.FromArgb(0x3A, 0x3A, 0x3A);
        public static readonly Color Text = Color.FromArgb(0xF2, 0xF2, 0xF2);
        public static readonly Color Text2 = Color.FromArgb(0xA8, 0xA8, 0xA8);
        public static readonly Color Muted = Color.FromArgb(0x6E, 0x6E, 0x6E);
        public static readonly Color Success = Color.FromArgb(0x00, 0xD4, 0x7E);
        public static readonly Color Warning = Color.FromArgb(0xFF, 0xB8, 0x00);
        public static readonly Color Error = Color.FromArgb(0xFF, 0x44, 0x44);
        public static readonly Color CloseHover = Color.FromArgb(0xE6, 0xFF, 0x44, 0x44);

        public static Color ForState(string? state) => state switch
        {
            "active" => Success,
            "processing" => Warning,
            "inactive" => Error,
            _ => Muted
        };

        public static Color TextForState(string? state) => state switch
        {
            "active" => Text,
            "processing" => Warning,
            "inactive" => Error,
            _ => Text2
        };
    }

    /// <summary>Fonts transcribed from the CSS type scale (px → pt at 96 DPI).</summary>
    internal static class ChromeFonts
    {
        // css 16px  / weight 600
        public static readonly Font Brand = new("Segoe UI Semibold", 12f, FontStyle.Regular, GraphicsUnit.Point);
        // css 10px mono, letter-spacing .18em
        public static readonly Font BrandSub = new("Consolas", 7.5f, FontStyle.Regular, GraphicsUnit.Point);
        // css 10.5px / weight 600, letter-spacing .14em
        public static readonly Font Pill = new("Segoe UI Semibold", 7.875f, FontStyle.Regular, GraphicsUnit.Point);
        // css 10.5px / weight 600, letter-spacing .12em
        public static readonly Font Ghost = new("Segoe UI Semibold", 7.875f, FontStyle.Regular, GraphicsUnit.Point);
        // css 12px
        public static readonly Font Trigger = new("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        // css 12.5px
        public static readonly Font ItemName = new("Segoe UI", 9.375f, FontStyle.Regular, GraphicsUnit.Point);
        // css 9.5px mono
        public static readonly Font ItemKey = new("Consolas", 7.125f, FontStyle.Regular, GraphicsUnit.Point);
    }

    /// <summary>GDI text with CSS `letter-spacing` support (WinForms has no tracking API).</summary>
    internal static class ChromeText
    {
        private const TextFormatFlags Flags =
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

        public static float Measure(string text, Font font, float tracking)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            float w = 0f;
            foreach (char ch in text)
            {
                var size = TextRenderer.MeasureText(ch.ToString(), font, new Size(int.MaxValue, int.MaxValue), Flags);
                w += size.Width + tracking;
            }
            return w - tracking;
        }

        public static float LineHeight(Font font) =>
            TextRenderer.MeasureText("Xg", font, new Size(int.MaxValue, int.MaxValue), Flags).Height;

        public static void Draw(Graphics g, string text, Font font, Color color, float x, float y, float tracking)
        {
            float cx = x;
            foreach (char ch in text)
            {
                string s = ch.ToString();
                var size = TextRenderer.MeasureText(s, font, new Size(int.MaxValue, int.MaxValue), Flags);
                TextRenderer.DrawText(g, s, font,
                    new Point((int)Math.Round(cx), (int)Math.Round(y)), color, Flags);
                cx += size.Width + tracking;
            }
        }
    }

    // ───────────────────────────────────────────────────────────────────────────
    //  BRAND — .brand-dot + .brand-name + .brand-sub
    // ───────────────────────────────────────────────────────────────────────────

    internal sealed class ChromeBrandDot : Control
    {
        private string _state = "connecting";

        public ChromeBrandDot()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            TabStop = false;
            BackColor = Color.Transparent;
            Size = new Size(9, 9);
        }

        public void SetState(string state)
        {
            if (_state == state) return;
            _state = state;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var color = ChromePalette.ForState(_state);
            float s = Math.Min(Width, Height) / 9f;
            float size = 8f * s;
            var rect = new RectangleF((Width - size) / 2f, (Height - size) / 2f, size, size);

            // CSS: box-shadow 0 0 8px rgba(color,.6) on active/processing.
            if (_state is "active" or "processing")
            {
                for (int i = 4; i >= 1; i--)
                {
                    var glowRect = RectangleF.Inflate(rect, i * 1.5f * s, i * 1.5f * s);
                    using var glowPath = UITheme.RoundedRect(glowRect, 2f + i * 1.1f);
                    using var glow = new SolidBrush(Color.FromArgb(15, color.R, color.G, color.B));
                    g.FillPath(glow, glowPath);
                }
            }

            using var path = UITheme.RoundedRect(rect, 2f * s);
            using var fill = new SolidBrush(color);
            g.FillPath(fill, path);
        }
    }

    internal sealed class ChromeLabel : Control
    {
        private float _tracking;

        public ChromeLabel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            TabStop = false;
            BackColor = Color.Transparent;
            Cursor = Cursors.Default;
        }

        public float Tracking
        {
            get => _tracking;
            set { _tracking = value; Invalidate(); }
        }

        public void AutoSizeToText()
        {
            float w = ChromeText.Measure(Text ?? "", Font, _tracking);
            float h = ChromeText.LineHeight(Font);
            Size = new Size((int)Math.Ceiling(w) + 2, (int)Math.Ceiling(h) + 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ChromeText.Draw(e.Graphics, Text ?? "", Font, ForeColor, 1, 1, _tracking);
        }
    }

    // ───────────────────────────────────────────────────────────────────────────
    //  STATUS PILL — .status-pill (+ breathe / ripple keyframes)
    // ───────────────────────────────────────────────────────────────────────────

    internal sealed class ChromeStatusPill : Control
    {
        private string _state = "connecting";
        private float _scale = 1f;

        public ChromeStatusPill()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            TabStop = false;
            BackColor = Color.Transparent;
            Cursor = Cursors.Default;
            Height = 30;

            var anim = new System.Windows.Forms.Timer { Interval = 40 };
            anim.Tick += (_, _) =>
            {
                if (_state is "active" or "processing") Invalidate();
            };
            anim.Start();
            _animTimer = anim;

            ApplyScale(1f);
        }

        // Kept alive by the control; garbage collection is never a concern here.
        private readonly System.Windows.Forms.Timer _animTimer;

        private string Label => _state switch
        {
            "active" => "ACTIVE",
            "processing" => "PROCESSING",
            "inactive" => "INACTIVE",
            _ => "CONNECTING"
        };

        public void ApplyScale(float k)
        {
            _scale = Math.Max(0.5f, k);
            int pad = (int)Math.Round(14 * _scale);
            int dot = (int)Math.Round(6 * _scale);
            int gap = (int)Math.Round(8 * _scale);
            int textW = (int)Math.Ceiling(ChromeText.Measure(Label, ChromeFonts.Pill, 1.47f));
            Width = pad * 2 + dot + gap + textW;
            Height = (int)Math.Round(30 * _scale);
            Invalidate();
        }

        public void SetState(string state)
        {
            if (_state == state) return;
            _state = state;
            ApplyScale(_scale);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = UITheme.RoundedRect(rect, Height / 2f))
            {
                using var fill = new SolidBrush(ChromePalette.Card);
                g.FillPath(fill, path);
                using var pen = new Pen(ChromePalette.Border, 1f);
                g.DrawPath(pen, path);
            }

            float s = Height / 30f;
            float pad = 14f * s;
            float dot = 6f * s;
            float cx = pad + dot / 2f;
            float cy = Height / 2f;
            var dotColor = ChromePalette.ForState(_state);

            if (_state is "active" or "processing")
            {
                float period = _state == "active" ? 2000f : 1000f;
                float phase = (Environment.TickCount % (int)period) / period;

                // @keyframes ripple — expanding 1px ring, fading out.
                float r = dot / 2f + 8.5f * s * phase;
                using (var ringPen = new Pen(Color.FromArgb((int)(110 * (1 - phase)), dotColor.R, dotColor.G, dotColor.B), 1f * s))
                    g.DrawEllipse(ringPen, cx - r, cy - r, r * 2, r * 2);

                // @keyframes breathe — opacity 1 → .35 → 1.
                int alpha = (int)(255 * (0.675f + 0.325f * Math.Cos(2 * Math.PI * phase)));
                using var dotBrush = new SolidBrush(Color.FromArgb(Math.Clamp(alpha, 80, 255), dotColor));
                g.FillEllipse(dotBrush, cx - dot / 2f, cy - dot / 2f, dot, dot);
            }
            else
            {
                using var dotBrush = new SolidBrush(dotColor);
                g.FillEllipse(dotBrush, cx - dot / 2f, cy - dot / 2f, dot, dot);
            }

            float tx = pad + dot + 8f * s;
            float th = ChromeText.LineHeight(ChromeFonts.Pill);
            ChromeText.Draw(g, Label, ChromeFonts.Pill, ChromePalette.TextForState(_state),
                tx, (Height - th) / 2f, 1.47f * s);
        }
    }

    // ───────────────────────────────────────────────────────────────────────────
    //  GHOST BUTTON — .ghost-btn
    // ───────────────────────────────────────────────────────────────────────────

    internal sealed class ChromeGhostButton : Control
    {
        private bool _hover;
        private bool _pressed;
        private float _scale = 1f;

        public event EventHandler? Clicked;

        public ChromeGhostButton(string text)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            Text = text;
            TabStop = true;
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
            Height = 34;

            MouseEnter += (_, _) => { _hover = true; Invalidate(); };
            MouseLeave += (_, _) => { _hover = false; _pressed = false; _scale = 1f; Invalidate(); };
            MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { _pressed = true; _scale = 0.97f; Invalidate(); } };
            MouseUp += (_, _) => { _pressed = false; _scale = 1f; Invalidate(); };
            Click += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);

            MeasureFor(1f);
        }

        public void MeasureFor(float k)
        {
            float textW = ChromeText.Measure(Text ?? "", ChromeFonts.Ghost, 1.26f);
            Width = (int)Math.Round(textW + 32 * k);
            Height = (int)Math.Round(34 * k);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float w = Width * _scale;
            float h = Height * _scale;
            var rect = new RectangleF((Width - w) / 2f, (Height - h) / 2f, w, h);

            var bg = _hover || _pressed ? ChromePalette.Hover : ChromePalette.Card2;
            var border = _hover || _pressed ? ChromePalette.BorderHi : ChromePalette.Border;
            var fg = _hover || _pressed ? ChromePalette.Text : ChromePalette.Text2;

            using (var path = UITheme.RoundedRect(rect, 8f * (Height / 34f)))
            {
                using var fill = new SolidBrush(bg);
                g.FillPath(fill, path);
                using var pen = new Pen(border, 1f);
                g.DrawPath(pen, path);
            }

            float tw = ChromeText.Measure(Text ?? "", ChromeFonts.Ghost, 1.26f);
            float th = ChromeText.LineHeight(ChromeFonts.Ghost);
            ChromeText.Draw(g, Text ?? "", ChromeFonts.Ghost, fg,
                rect.X + (rect.Width - tw) / 2f, rect.Y + (rect.Height - th) / 2f, 1.26f);
        }
    }

    // ───────────────────────────────────────────────────────────────────────────
    //  WINDOW BUTTONS — .win-btn (30x26, 10x10 icons, 1.4 stroke)
    // ───────────────────────────────────────────────────────────────────────────

    internal enum ChromeWinKind { Minimize, Close }

    internal sealed class ChromeWindowButton : Control
    {
        private readonly ChromeWinKind _kind;
        private bool _hover;

        public event EventHandler? Clicked;

        public ChromeWindowButton(ChromeWinKind kind)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            _kind = kind;
            TabStop = false;
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
            Size = new Size(30, 26);

            MouseEnter += (_, _) => { _hover = true; Invalidate(); };
            MouseLeave += (_, _) => { _hover = false; Invalidate(); };
            Click += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool close = _kind == ChromeWinKind.Close;
            Color bg = _hover ? (close ? ChromePalette.CloseHover : ChromePalette.Hover) : Color.Transparent;
            Color fg = _hover ? (close ? Color.White : ChromePalette.Text) : ChromePalette.Text2;

            if (bg.A > 0)
            {
                using var path = UITheme.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 6);
                using var fill = new SolidBrush(bg);
                g.FillPath(fill, path);
            }

            float s = Height / 26f;
            float cx = Width / 2f;
            float cy = Height / 2f;
            using var pen = new Pen(fg, 1.4f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round };

            if (close)
            {
                g.DrawLine(pen, cx - 4 * s, cy - 4 * s, cx + 4 * s, cy + 4 * s);
                g.DrawLine(pen, cx + 4 * s, cy - 4 * s, cx - 4 * s, cy + 4 * s);
            }
            else
            {
                g.DrawLine(pen, cx - 4 * s, cy, cx + 4 * s, cy);
            }
        }
    }

    internal sealed class ChromeDivider : Control
    {
        public ChromeDivider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            DoubleBuffered = true;
            TabStop = false;
            Size = new Size(1, 20);
            BackColor = ChromePalette.Border;
        }
    }

    // ───────────────────────────────────────────────────────────────────────────
    //  MODEL SELECTOR — .selector-trigger + .selector-drop
    // ───────────────────────────────────────────────────────────────────────────

    internal sealed class ChromeModelTrigger : Control
    {
        private bool _hover;
        private bool _open;
        private string _display = "Select model…";

        public event EventHandler? Clicked;

        public ChromeModelTrigger()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            TabStop = false;
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
            Size = new Size(250, 34);

            MouseEnter += (_, _) => { _hover = true; Invalidate(); };
            MouseLeave += (_, _) => { _hover = false; Invalidate(); };
            Click += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);
        }

        public string DisplayText
        {
            get => _display;
            set { _display = value ?? ""; Invalidate(); }
        }

        public bool DropdownOpen
        {
            get => _open;
            set { _open = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bg = _open || _hover ? ChromePalette.Hover : ChromePalette.Card;
            var border = _open || _hover ? ChromePalette.BorderHi : ChromePalette.Border;

            using (var path = UITheme.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 9))
            {
                using var fill = new SolidBrush(bg);
                g.FillPath(fill, path);
                using var pen = new Pen(border, 1f);
                g.DrawPath(pen, path);
            }

            float s = Height / 34f;
            int padL = (int)Math.Round(14 * s);
            int padR = (int)Math.Round(12 * s);
            int icon = (int)Math.Round(10 * s);
            int gap = (int)Math.Round(10 * s);

            var textRect = new Rectangle(padL, 0, Width - padL - padR - icon - gap, Height);
            TextRenderer.DrawText(g, _display, ChromeFonts.Trigger, textRect, ChromePalette.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            float cx = Width - padR - icon / 2f;
            float cy = Height / 2f;
            using var chevron = new Pen(ChromePalette.Muted, 1.4f * s)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            float a = 3.5f * s;
            float b = 2f * s;
            if (_open)
                g.DrawLines(chevron, new[]
                {
                    new PointF(cx - a, cy + b * 0.75f),
                    new PointF(cx, cy - b),
                    new PointF(cx + a, cy + b * 0.75f)
                });
            else
                g.DrawLines(chevron, new[]
                {
                    new PointF(cx - a, cy - b * 0.75f),
                    new PointF(cx, cy + b),
                    new PointF(cx + a, cy - b * 0.75f)
                });
        }
    }

    /// <summary>.selector-drop rendered as a borderless popup window.</summary>
    internal sealed class ChromeModelDropdown : Form
    {
        private const int PadX = 6;
        private const int ItemH = 48;
        private const int MaxListH = 320;

        private readonly List<(string Key, string Name)> _items;
        private readonly string _selectedKey;
        private int _hover = -1;

        public event Action<string>? ModelPicked;

        public ChromeModelDropdown(IEnumerable<(string Key, string Name)> items, string selectedKey)
        {
            _items = items.ToList();
            _selectedKey = selectedKey ?? "";

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = ChromePalette.Card;
            DoubleBuffered = true;
            Width = 300;
            int listH = Math.Min(MaxListH, _items.Count * ItemH);
            Height = PadX * 2 + Math.Max(ItemH, listH);

            using (var path = UITheme.RoundedRect(new Rectangle(0, 0, Width, Height), 10))
                Region = new Region(path);

            Deactivate += (_, _) => Close();
            MouseMove += (_, e) =>
            {
                int idx = IndexAt(e.Location);
                if (idx != _hover) { _hover = idx; Invalidate(); }
            };
            MouseLeave += (_, _) => { if (_hover != -1) { _hover = -1; Invalidate(); } };
            MouseDown += (_, e) =>
            {
                int idx = IndexAt(e.Location);
                if (idx < 0) return;
                ModelPicked?.Invoke(_items[idx].Key);
                Close();
            };
        }

        private int IndexAt(Point p)
        {
            if (p.X < PadX || p.X > Width - PadX) return -1;
            int y = p.Y - PadX;
            if (y < 0) return -1;
            int idx = y / ItemH;
            return idx < _items.Count && y < _items.Count * ItemH ? idx : -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var bg = new SolidBrush(ChromePalette.Card))
                g.FillRectangle(bg, ClientRectangle);

            // 0 12px 40px rgba(0,0,0,.55) + 0 2px 8px rgba(0,0,0,.4) — approximated
            // with nested dark rings behind the card edge.
            for (int i = 3; i >= 1; i--)
            {
                using var shadowPen = new Pen(Color.FromArgb(40, 0, 0, 0), 1f);
                using var shadowPath = UITheme.RoundedRect(
                    Rectangle.Inflate(ClientRectangle, -i, -i), 10);
                g.DrawPath(shadowPen, shadowPath);
            }

            using (var borderPath = UITheme.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 10))
            using (var pen = new Pen(ChromePalette.BorderHi, 1f))
                g.DrawPath(pen, borderPath);

            for (int i = 0; i < _items.Count; i++)
            {
                var itemRect = new Rectangle(PadX, PadX + i * ItemH, Width - PadX * 2, ItemH);
                bool selected = _items[i].Key == _selectedKey;

                if (i == _hover)
                {
                    using var hoverPath = UITheme.RoundedRect(itemRect, 7);
                    using var hoverBrush = new SolidBrush(ChromePalette.Hover);
                    g.FillPath(hoverBrush, hoverPath);
                }

                if (selected)
                {
                    using var bar = new SolidBrush(Color.White);
                    g.FillRectangle(bar, itemRect.X, itemRect.Y, 3, itemRect.Height);
                }

                TextRenderer.DrawText(g, _items[i].Name, ChromeFonts.ItemName,
                    new Point(itemRect.X + 16, itemRect.Y + 9), ChromePalette.Text,
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

                TextRenderer.DrawText(g, _items[i].Key, ChromeFonts.ItemKey,
                    new Point(itemRect.X + 16, itemRect.Y + 26), ChromePalette.Muted,
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            }
        }
    }
}

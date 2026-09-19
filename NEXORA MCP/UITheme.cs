using System.Drawing.Drawing2D;

namespace NEXORA_MCP
{
    public static class UITheme
    {
        // ─── Premium B&W palette (Linear / Vercel inspired) ───────────────
        // True-black base, refined grays, pure-white focus. No gradients.
        public static readonly Color BackPrimary   = Color.FromArgb(0x0A, 0x0A, 0x0A);
        public static readonly Color BackSecondary = Color.FromArgb(0x11, 0x11, 0x11);
        public static readonly Color BackTertiary  = Color.FromArgb(0x18, 0x18, 0x18);
        public static readonly Color BackInput     = Color.FromArgb(0x0F, 0x0F, 0x0F);

        public static readonly Color BorderDefault = Color.FromArgb(0x2A, 0x2A, 0x2A);
        public static readonly Color BorderHover   = Color.FromArgb(0x3A, 0x3A, 0x3A);
        public static readonly Color BorderFocus   = Color.FromArgb(0xFF, 0xFF, 0xFF);
        public static readonly Color BorderError   = Color.FromArgb(0xFF, 0x44, 0x44);

        public static readonly Color TextInverse   = Color.FromArgb(0x0A, 0x0A, 0x0A);
        public static readonly Color AccentPrimary = Color.FromArgb(0xFF, 0xFF, 0xFF);
        public static readonly Color AccentHover   = Color.FromArgb(0xE8, 0xE8, 0xE8);
        public static readonly Color AccentPress   = Color.FromArgb(0xCC, 0xCC, 0xCC);

        public static readonly Color ShadowAmbient  = Color.FromArgb(30, 0, 0, 0);
        public static readonly Color ShadowElevated = Color.FromArgb(60, 0, 0, 0);

        // ─── Legacy aliases (keep every existing reference compiling) ───────
        public static readonly Color Back        = Color.FromArgb(10, 10, 10);
        public static readonly Color Card        = BackSecondary;
        public static readonly Color CardHover   = BackTertiary;
        public static readonly Color Border      = BorderDefault;
        public static readonly Color BorderHi    = BorderFocus;
        public static readonly Color TextPrimary = Color.FromArgb(250, 250, 250);
        public static readonly Color TextSecondary = Color.FromArgb(160, 160, 160);
        public static readonly Color TextMuted   = Color.FromArgb(106, 106, 106);
        public static readonly Color Accent      = AccentPrimary;
        public static readonly Color Success     = Color.FromArgb(0, 212, 126);
        public static readonly Color Error       = BorderError;
        public static readonly Color Warning     = Color.FromArgb(255, 184, 0);
        public static readonly Color TopBar      = BackSecondary;
        public static readonly Color Field       = BackInput;
        public static readonly Color Placeholder = TextMuted;

        // ─── Fonts ──────────────────────────────────────────────────────────
        public static Font Display(double size)  => new("Segoe UI", (float)size, FontStyle.Bold);
        public static Font Heading(double size)  => new("Segoe UI", (float)size, FontStyle.Bold);
        public static Font Body(double size)     => new("Segoe UI", (float)size, FontStyle.Regular);
        public static Font BodyBold(double size) => new("Segoe UI", (float)size, FontStyle.Bold);
        public static Font Mono(double size)     => new("Consolas", (float)size, FontStyle.Regular);

        // ─── Easing ─────────────────────────────────────────────────────────
        public static float EaseOutCubic(float t) => 1.0f - (float)Math.Pow(1 - t, 3);
        public static float EaseInOutSine(float t) => -(float)(Math.Cos(Math.PI * t) - 1) / 2;
        public static float EaseOutExpo(float t) => t >= 1f ? 1f : 1f - (float)Math.Pow(2f, -10f * t);

        // ─── Spacing scale (4px base) ───────────────────────────────────────
        public const int Space1 = 4;
        public const int Space2 = 8;
        public const int Space3 = 12;
        public const int Space4 = 16;
        public const int Space5 = 20;
        public const int Space6 = 24;
        public const int Space8 = 32;
        public const int Space10 = 40;
        public const int Space12 = 48;
        public const int Space16 = 64;

        // ─── Radius ─────────────────────────────────────────────────────────
        public const int RadiusSmall = 6;
        public const int RadiusMedium = 10;
        public const int RadiusLarge = 14;
        public const int RadiusFull = 999;

        // ─── Animation durations ────────────────────────────────────────────
        public const int AnimFast = 120;
        public const int AnimNormal = 200;
        public const int AnimSlow = 300;

        // ─── Premium type scale (extra, old helpers below stay) ────────────
        public static Font FontDisplayLarge => new("Segoe UI Variable", 28f, FontStyle.Bold, GraphicsUnit.Pixel);
        public static Font FontDisplayMedium => new("Segoe UI Variable", 20f, FontStyle.Bold, GraphicsUnit.Pixel);
        public static Font FontHeading => new("Segoe UI Variable", 15f, FontStyle.Bold, GraphicsUnit.Pixel);
        public static Font FontBodyLarge => new("Segoe UI", 14f, FontStyle.Regular, GraphicsUnit.Pixel);
        public static Font FontBodySmall => new("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Pixel);
        public static Font FontCaption => new("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Pixel);
        public static Font FontMonoSmall => new("Consolas", 11f, FontStyle.Regular, GraphicsUnit.Pixel);

        // ─── Color lerp ─────────────────────────────────────────────────────
        public static Color Lerp(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        // ─── Rounded rect path ──────────────────────────────────────────────
        public static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2f;
            if (d <= 0 || rect.Width <= 0 || rect.Height <= 0) { path.AddRectangle(Rectangle.Round(rect)); return path; }
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void DrawFocusRing(Graphics g, Rectangle bounds, int inset = 2)
        {
            var r = Rectangle.Inflate(bounds, inset, inset);
            using var path = RoundedRect(r, RadiusSmall + inset);
            using var pen = new Pen(BorderFocus, 1.5f);
            g.DrawPath(pen, path);
        }

        public static void DrawDivider(Graphics g, int x, int y, int width)
        {
            using var pen = new Pen(BorderDefault, 1f);
            g.DrawLine(pen, x, y, x + width, y);
        }

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d > bounds.Width || d > bounds.Height) d = Math.Min(bounds.Width, bounds.Height);
            if (d <= 0) { path.AddRectangle(bounds); return path; }

            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
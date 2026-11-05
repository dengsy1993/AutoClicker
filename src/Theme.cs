using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AutoClicker
{
    /// <summary>配色 + 字体 + 绘制辅助。</summary>
    public static class Theme
    {
        public static bool Dark = true;

        // ---- 基础色 ----
        public static Color Background;
        public static Color Card;
        public static Color CardAlt;
        public static Color Elevated;
        public static Color Text;
        public static Color TextDim;
        public static Color Muted;
        public static Color Border;
        public static Color BorderStrong;

        // ---- 强调色 ----
        public static Color Accent;
        public static Color AccentHover;
        public static Color AccentDim;
        public static Color AccentText;

        // ---- 顶部 ----
        public static Color HeaderStart;
        public static Color HeaderEnd;
        public static Color HeaderText;
        public static Color HeaderMuted;

        // ---- 表格 / 输入 ----
        public static Color InputBack;
        public static Color InputBorder;
        public static Color Selection;
        public static Color GridHeader;
        public static Color GridAlt;
        public static Color Track;

        // ---- 状态 ----
        public static Color Danger;
        public static Color Success;
        public static Color Warn;

        // ---- 圆角 ----
        public const int RadiusCard = 14;
        public const int RadiusButton = 9;
        public const int RadiusInput = 8;

        public static Font TitleFont;
        public static Font SubtitleFont;
        public static Font UiFont;
        public static Font SmallFont;
        public static Font TinyFont;
        public static Font BoldFont;
        public static Font Subtitle;
        public static Font ButtonFont;
        public static Font MonoFont;

        static Theme()
        {
            TitleFont = new Font("Microsoft YaHei UI", 19F, FontStyle.Bold);
            SubtitleFont = new Font("Microsoft YaHei UI", 8.5F);
            Subtitle = new Font("Microsoft YaHei UI", 8.5F);
            UiFont = new Font("Microsoft YaHei UI", 9F);
            SmallFont = new Font("Microsoft YaHei UI", 8.5F);
            TinyFont = new Font("Microsoft YaHei UI", 8F);
            BoldFont = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
            ButtonFont = new Font("Microsoft YaHei UI", 9.5F);
            MonoFont = new Font("Consolas", 8.5F);
            Apply(true);
        }

        public static void Apply(bool dark)
        {
            Dark = dark;
            if (dark)
            {
                Background = FromHex("#0A0F1A");
                Card = FromHex("#121A2B");
                CardAlt = FromHex("#0E1626");
                Elevated = FromHex("#182338");
                Text = FromHex("#E8EDF6");
                TextDim = FromHex("#A7B4C9");
                Muted = FromHex("#7E8DA6");
                Border = FromHex("#1F2B40");
                BorderStrong = FromHex("#2A3A55");

                Accent = FromHex("#4F8EF7");
                AccentHover = FromHex("#6BA2FF");
                AccentDim = FromHex("#1B3563");
                AccentText = FromHex("#DCE9FF");

                HeaderStart = FromHex("#070C16");
                HeaderEnd = FromHex("#16234A");
                HeaderText = FromHex("#F4F7FC");
                HeaderMuted = FromHex("#8FA6C9");

                InputBack = FromHex("#0D1524");
                InputBorder = FromHex("#243450");
                Selection = FromHex("#16294A");
                GridHeader = FromHex("#101827");
                GridAlt = FromHex("#121A2B");
                Track = FromHex("#0D1524");

                Danger = FromHex("#FF6B6B");
                Success = FromHex("#3DDC97");
                Warn = FromHex("#FFC14D");
            }
            else
            {
                Background = FromHex("#F2F5FA");
                Card = FromHex("#FFFFFF");
                CardAlt = FromHex("#F7F9FC");
                Elevated = FromHex("#EEF3FA");
                Text = FromHex("#16202E");
                TextDim = FromHex("#44536B");
                Muted = FromHex("#6C7C94");
                Border = FromHex("#E3E9F2");
                BorderStrong = FromHex("#CDD8E6");

                Accent = FromHex("#2F6BEF");
                AccentHover = FromHex("#1F58D6");
                AccentDim = FromHex("#E7EFFF");
                AccentText = FromHex("#1B4FBF");

                HeaderStart = FromHex("#0B1220");
                HeaderEnd = FromHex("#1B2F63");
                HeaderText = FromHex("#FFFFFF");
                HeaderMuted = FromHex("#A9BEE0");

                InputBack = FromHex("#F7F9FC");
                InputBorder = FromHex("#D8E1EE");
                Selection = FromHex("#E4EDFF");
                GridHeader = FromHex("#F7F9FC");
                GridAlt = FromHex("#FFFFFF");
                Track = FromHex("#EDF1F7");

                Danger = FromHex("#D93B3B");
                Success = FromHex("#14875A");
                Warn = FromHex("#B26A00");
            }
        }

        public static Color FromHex(string hex)
        {
            hex = hex.TrimStart('#');
            int r = Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = Convert.ToInt32(hex.Substring(4, 2), 16);
            return Color.FromArgb(r, g, b);
        }

        public static Color Mix(Color a, Color b, double ratio)
        {
            int r = (int)Math.Round(a.R + (b.R - a.R) * ratio);
            int g = (int)Math.Round(a.G + (b.G - a.G) * ratio);
            int bl = (int)Math.Round(a.B + (b.B - a.B) * ratio);
            return Color.FromArgb(
                Math.Max(0, Math.Min(255, r)),
                Math.Max(0, Math.Min(255, g)),
                Math.Max(0, Math.Min(255, bl)));
        }

        public static Color Alpha(Color c, int alpha)
        {
            return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), c);
        }

        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = Math.Max(1, radius * 2);
            if (radius <= 0)
            {
                path.AddRectangle(r);
                return path;
            }
            if (r.Width < d || r.Height < d)
            {
                d = Math.Min(Math.Max(1, r.Width), Math.Max(1, r.Height));
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics g, Rectangle r, int radius, Color fill, Color border)
        {
            if (r.Width <= 0 || r.Height <= 0)
            {
                return;
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = RoundedRect(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), radius))
            {
                using (SolidBrush b = new SolidBrush(fill))
                {
                    g.FillPath(b, path);
                }
                if (border.A > 0)
                {
                    using (Pen p = new Pen(border, 1f))
                    {
                        g.DrawPath(p, path);
                    }
                }
            }
        }

        public static void FillRoundedGradient(Graphics g, Rectangle r, int radius, Color top, Color bottom, Color border)
        {
            if (r.Width <= 0 || r.Height <= 0)
            {
                return;
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = RoundedRect(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), radius))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(
                    new Rectangle(r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height)),
                    top,
                    bottom,
                    LinearGradientMode.Vertical))
                {
                    g.FillPath(b, path);
                }
                if (border.A > 0)
                {
                    using (Pen p = new Pen(border, 1f))
                    {
                        g.DrawPath(p, path);
                    }
                }
            }
        }

        public static void DrawText(Graphics g, string text, Font font, Color color, Rectangle bounds, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, text ?? string.Empty, font, bounds, color, flags);
        }

        /// <summary>窗口标题栏深色（Win10 1809+ / Win11）。</summary>
        public static void ApplyTitleBar(Form form, bool dark)
        {
            try
            {
                int v = dark ? 1 : 0;
                Native.DwmSetWindowAttribute(form.Handle, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, 4);
                int round = Native.DWMWCP_ROUND;
                Native.DwmSetWindowAttribute(form.Handle, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, 4);
            }
            catch
            {
            }
        }
    }

    /// <summary>按钮 / 菜单里用的小图标，纯 GDI 画，不用图片资源。</summary>
    public enum Glyph
    {
        None = 0,
        Play,
        Stop,
        Record,
        Plus,
        ChevronDown,
        Edit,
        Copy,
        Trash,
        ArrowUp,
        ArrowDown,
        Check,
        Target,
        Pause,
        Keyboard
    }

    public static class GlyphPainter
    {
        public static void Draw(Graphics g, Glyph glyph, Rectangle r, Color color)
        {
            if (glyph == Glyph.None || r.Width <= 0 || r.Height <= 0)
            {
                return;
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush b = new SolidBrush(color))
            using (Pen p = new Pen(color, 1.6f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                switch (glyph)
                {
                    case Glyph.Play:
                        {
                            PointF[] tri = new PointF[]
                            {
                                new PointF(r.X + r.Width * 0.24f, r.Y + r.Height * 0.14f),
                                new PointF(r.X + r.Width * 0.86f, r.Y + r.Height * 0.5f),
                                new PointF(r.X + r.Width * 0.24f, r.Y + r.Height * 0.86f)
                            };
                            g.FillPolygon(b, tri);
                            break;
                        }
                    case Glyph.Stop:
                        {
                            Rectangle sq = new Rectangle(
                                r.X + (int)(r.Width * 0.24f),
                                r.Y + (int)(r.Height * 0.24f),
                                (int)(r.Width * 0.52f),
                                (int)(r.Height * 0.52f));
                            using (GraphicsPath path = Theme.RoundedRect(sq, 2))
                            {
                                g.FillPath(b, path);
                            }
                            break;
                        }
                    case Glyph.Record:
                        {
                            Rectangle c = new Rectangle(
                                r.X + (int)(r.Width * 0.2f),
                                r.Y + (int)(r.Height * 0.2f),
                                (int)(r.Width * 0.6f),
                                (int)(r.Height * 0.6f));
                            g.FillEllipse(b, c);
                            break;
                        }
                    case Glyph.Plus:
                        {
                            float cx = r.X + r.Width / 2f;
                            float cy = r.Y + r.Height / 2f;
                            float half = r.Width * 0.30f;
                            g.DrawLine(p, cx - half, cy, cx + half, cy);
                            g.DrawLine(p, cx, cy - half, cx, cy + half);
                            break;
                        }
                    case Glyph.ChevronDown:
                        {
                            float cx = r.X + r.Width / 2f;
                            float cy = r.Y + r.Height / 2f;
                            float w = r.Width * 0.26f;
                            float h = r.Height * 0.16f;
                            g.DrawLine(p, cx - w, cy - h, cx, cy + h);
                            g.DrawLine(p, cx, cy + h, cx + w, cy - h);
                            break;
                        }
                    case Glyph.Check:
                        {
                            g.DrawLine(p, r.X + r.Width * 0.22f, r.Y + r.Height * 0.52f, r.X + r.Width * 0.42f, r.Y + r.Height * 0.72f);
                            g.DrawLine(p, r.X + r.Width * 0.42f, r.Y + r.Height * 0.72f, r.X + r.Width * 0.78f, r.Y + r.Height * 0.28f);
                            break;
                        }
                    case Glyph.Edit:
                        {
                            g.DrawLine(p, r.X + r.Width * 0.24f, r.Y + r.Height * 0.76f, r.X + r.Width * 0.72f, r.Y + r.Height * 0.28f);
                            g.DrawLine(p, r.X + r.Width * 0.18f, r.Y + r.Height * 0.82f, r.X + r.Width * 0.30f, r.Y + r.Height * 0.90f);
                            break;
                        }
                    case Glyph.Copy:
                        {
                            Rectangle back = new Rectangle(
                                r.X + (int)(r.Width * 0.34f),
                                r.Y + (int)(r.Height * 0.18f),
                                (int)(r.Width * 0.46f),
                                (int)(r.Height * 0.46f));
                            g.DrawRectangle(p, back);
                            Rectangle front = new Rectangle(
                                r.X + (int)(r.Width * 0.18f),
                                r.Y + (int)(r.Height * 0.34f),
                                (int)(r.Width * 0.46f),
                                (int)(r.Height * 0.46f));
                            using (SolidBrush clear = new SolidBrush(Theme.Dark ? Theme.Card : Theme.Card))
                            {
                                g.FillRectangle(clear, front);
                            }
                            g.DrawRectangle(p, front);
                            break;
                        }
                    case Glyph.Trash:
                        {
                            g.DrawLine(p, r.X + r.Width * 0.22f, r.Y + r.Height * 0.28f, r.X + r.Width * 0.78f, r.Y + r.Height * 0.28f);
                            g.DrawLine(p, r.X + r.Width * 0.38f, r.Y + r.Height * 0.20f, r.X + r.Width * 0.62f, r.Y + r.Height * 0.20f);
                            g.DrawLine(p, r.X + r.Width * 0.30f, r.Y + r.Height * 0.28f, r.X + r.Width * 0.34f, r.Y + r.Height * 0.80f);
                            g.DrawLine(p, r.X + r.Width * 0.70f, r.Y + r.Height * 0.28f, r.X + r.Width * 0.66f, r.Y + r.Height * 0.80f);
                            g.DrawLine(p, r.X + r.Width * 0.34f, r.Y + r.Height * 0.80f, r.X + r.Width * 0.66f, r.Y + r.Height * 0.80f);
                            break;
                        }
                    case Glyph.ArrowUp:
                        {
                            float cx = r.X + r.Width / 2f;
                            g.DrawLine(p, cx, r.Y + r.Height * 0.28f, cx, r.Y + r.Height * 0.74f);
                            g.DrawLine(p, cx - r.Width * 0.22f, r.Y + r.Height * 0.46f, cx, r.Y + r.Height * 0.26f);
                            g.DrawLine(p, cx + r.Width * 0.22f, r.Y + r.Height * 0.46f, cx, r.Y + r.Height * 0.26f);
                            break;
                        }
                    case Glyph.ArrowDown:
                        {
                            float cx = r.X + r.Width / 2f;
                            g.DrawLine(p, cx, r.Y + r.Height * 0.26f, cx, r.Y + r.Height * 0.72f);
                            g.DrawLine(p, cx - r.Width * 0.22f, r.Y + r.Height * 0.54f, cx, r.Y + r.Height * 0.74f);
                            g.DrawLine(p, cx + r.Width * 0.22f, r.Y + r.Height * 0.54f, cx, r.Y + r.Height * 0.74f);
                            break;
                        }
                    case Glyph.Pause:
                        {
                            float w1 = Math.Max(2f, r.Width * 0.13f);
                            g.FillRectangle(b, r.X + r.Width * 0.30f, r.Y + r.Height * 0.24f, w1, r.Height * 0.52f);
                            g.FillRectangle(b, r.X + r.Width * 0.57f, r.Y + r.Height * 0.24f, w1, r.Height * 0.52f);
                            break;
                        }
                    case Glyph.Keyboard:
                        {
                            Rectangle body = new Rectangle(
                                r.X + (int)(r.Width * 0.12f),
                                r.Y + (int)(r.Height * 0.26f),
                                (int)(r.Width * 0.76f),
                                (int)(r.Height * 0.48f));
                            using (GraphicsPath path = Theme.RoundedRect(body, 2))
                            {
                                using (Pen pp = new Pen(color, 1.2f))
                                {
                                    g.DrawPath(pp, path);
                                }
                            }
                            float dot = Math.Max(1.4f, r.Width * 0.07f);
                            g.FillEllipse(b, r.X + r.Width * 0.24f, r.Y + r.Height * 0.40f, dot, dot);
                            g.FillEllipse(b, r.X + r.Width * 0.45f, r.Y + r.Height * 0.40f, dot, dot);
                            g.FillEllipse(b, r.X + r.Width * 0.66f, r.Y + r.Height * 0.40f, dot, dot);
                            g.FillRectangle(b, r.X + r.Width * 0.32f, r.Y + r.Height * 0.56f, r.Width * 0.36f, Math.Max(1.4f, r.Height * 0.07f));
                            break;
                        }
                    case Glyph.Target:
                        {
                            g.DrawEllipse(p, r.X + r.Width * 0.2f, r.Y + r.Height * 0.2f, r.Width * 0.6f, r.Height * 0.6f);
                            g.FillEllipse(b, r.X + r.Width * 0.42f, r.Y + r.Height * 0.42f, r.Width * 0.16f, r.Height * 0.16f);
                            break;
                        }
                }
            }
        }
    }
}

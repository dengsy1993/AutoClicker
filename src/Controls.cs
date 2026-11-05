using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;

namespace AutoClicker
{
    // =====================================================================
    // 小的界面辅助
    // =====================================================================
    public static class Ui
    {
        public static Label Label(string text, int x, int y, int w, int h, Color color, Font font)
        {
            Label l = new Label();
            l.Text = text;
            l.Location = new Point(x, y);
            l.Size = new Size(w, h);
            l.ForeColor = color;
            l.Font = font;
            l.BackColor = Color.Transparent;
            l.AutoSize = false;
            l.AutoEllipsis = false;
            l.Padding = new Padding(0);
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.UseCompatibleTextRendering = false;
            return l;
        }

        public static Label Title(string text, int x, int y, int w, int h)
        {
            return Label(text, x, y, w, h, Theme.Text, Theme.BoldFont);
        }

        public static Label Muted(string text, int x, int y, int w, int h)
        {
            return Label(text, x, y, w, h, Theme.Muted, Theme.SmallFont);
        }

        public static NormalTextBox Themed(int x, int y, int w, int h, string text, bool multiline)
        {
            NormalTextBox t = new NormalTextBox();
            t.Location = new Point(x, y);
            t.Size = new Size(w, h);
            t.Text = text;
            t.Multiline = multiline;
            t.ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None;
            t.BorderStyle = BorderStyle.None;
            t.BackColor = Theme.InputBack;
            t.ForeColor = Theme.Text;
            t.Font = multiline ? Theme.MonoFont : Theme.UiFont;
            return t;
        }

        /// <summary>把一个无边框 TextBox 放进圆角输入框里，返回 TextBox 本身。</summary>
        public static T Host<T>(Control parent, T box, int x, int y, int w, int h) where T : TextBox
        {
            box.BorderStyle = BorderStyle.None;
            box.BackColor = Theme.InputBack;
            box.ForeColor = Theme.Text;
            InputFrame frame = new InputFrame(box);
            frame.Location = new Point(x, y);
            frame.Size = new Size(w, h);
            frame.BaseColor = parent.BackColor;
            parent.Controls.Add(frame);
            return box;
        }
    }

    /// <summary>圆角卡片容器。</summary>
    public class CardPanel : Panel
    {
        public int Radius = Theme.RadiusCard;
        public bool ShowBorder = true;

        public CardPanel()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            BackColor = Theme.Background;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Background);
            Theme.FillRounded(g, ClientRectangle, Radius, Theme.Card, ShowBorder ? Theme.Border : Color.Transparent);

            // 顶部一点点高光，让卡片有层次
            if (ClientRectangle.Width > 8)
            {
                using (Pen p = new Pen(Theme.Alpha(Theme.Text, Theme.Dark ? 10 : 0), 1f))
                {
                    g.DrawLine(p, Radius / 2, 1, ClientRectangle.Width - Radius / 2, 1);
                }
            }
            base.OnPaint(e);
        }
    }

    public enum ButtonVariant
    {
        Primary = 0,
        Secondary = 1,
        Ghost = 2
    }

    /// <summary>自绘圆角按钮，支持小图标和快捷键后缀。</summary>
    public class RoundButton : Button
    {
        private bool _hover;
        private bool _pressed;

        public int Radius = Theme.RadiusButton;
        public ButtonVariant Variant = ButtonVariant.Secondary;
        public Glyph Icon = Glyph.None;
        public string Suffix = string.Empty;

        /// <summary>兼容写法：true 等同于 Primary。</summary>
        public bool Accent
        {
            get { return Variant == ButtonVariant.Primary; }
            set { Variant = value ? ButtonVariant.Primary : ButtonVariant.Secondary; }
        }

        public RoundButton()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            UseVisualStyleBackColor = false;
            Font = Theme.ButtonFont;
            BackColor = Theme.Card;
            ForeColor = Theme.Text;
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _pressed = true;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color fore;
            Color suffixColor;

            if (Variant == ButtonVariant.Primary)
            {
                Color top = Theme.Accent;
                Color bottom = Theme.Mix(Theme.Accent, Color.Black, 0.18);
                if (!Enabled)
                {
                    top = Theme.Mix(Theme.Card, Theme.Text, 0.06);
                    bottom = top;
                }
                else if (_pressed)
                {
                    top = Theme.Mix(Theme.Accent, Color.Black, 0.20);
                    bottom = Theme.Mix(Theme.Accent, Color.Black, 0.32);
                }
                else if (_hover)
                {
                    top = Theme.AccentHover;
                    bottom = Theme.Accent;
                }
                Theme.FillRoundedGradient(g, ClientRectangle, Radius, top, bottom, Color.Transparent);
                fore = Enabled ? Color.White : Theme.Muted;
                suffixColor = Enabled ? Color.FromArgb(205, 255, 255, 255) : Theme.Mix(Theme.Muted, Theme.Card, 0.4);
            }
            else if (Variant == ButtonVariant.Ghost)
            {
                if (_hover || _pressed)
                {
                    Theme.FillRounded(g, ClientRectangle, Radius, Theme.Alpha(Theme.Text, _pressed ? 16 : 10), Color.Transparent);
                }
                fore = Enabled ? Theme.TextDim : Theme.Muted;
                suffixColor = Theme.Muted;
            }
            else
            {
                Color fill = Theme.CardAlt;
                Color border = Theme.Border;
                if (!Enabled)
                {
                    fill = Theme.Mix(Theme.Card, Theme.Text, 0.04);
                    border = Theme.Border;
                }
                else if (_pressed)
                {
                    fill = Theme.Mix(Theme.CardAlt, Theme.Accent, 0.22);
                    border = Theme.Alpha(Theme.Accent, 120);
                }
                else if (_hover)
                {
                    fill = Theme.Elevated;
                    border = Theme.Alpha(Theme.Accent, 90);
                }
                Theme.FillRounded(g, ClientRectangle, Radius, fill, border);
                fore = Enabled ? Theme.Text : Theme.Muted;
                suffixColor = Enabled ? Theme.Muted : Theme.Mix(Theme.Muted, Theme.Card, 0.4);
            }

            DrawContent(g, fore, suffixColor);
        }

        private void DrawContent(Graphics g, Color fore, Color suffixColor)
        {
            string main = Text ?? string.Empty;
            string suffix = Suffix ?? string.Empty;
            Font font = Height >= 38 ? Theme.ButtonFont : Theme.UiFont;

            Size textSize = TextRenderer.MeasureText(g, main, font);
            Size suffixSize = suffix.Length > 0 ? TextRenderer.MeasureText(g, suffix, Theme.SmallFont) : Size.Empty;
            int iconSize = Icon != Glyph.None ? 15 : 0;
            int gapIcon = iconSize > 0 ? 7 : 0;
            int gapSuffix = suffix.Length > 0 ? 5 : 0;

            int total = iconSize + gapIcon + textSize.Width + gapSuffix + suffixSize.Width;
            // 放不下时先丢掉图标，保证快捷键提示一定画得出来
            if (total > Width - 6 && iconSize > 0)
            {
                iconSize = 0;
                gapIcon = 0;
                total = textSize.Width + gapSuffix + suffixSize.Width;
            }
            int x = Math.Max(4, (Width - total) / 2);
            int centerY = Height / 2;

            if (iconSize > 0)
            {
                Rectangle iconRect = new Rectangle(x, centerY - iconSize / 2, iconSize, iconSize);
                GlyphPainter.Draw(g, Icon, iconRect, fore);
                x += iconSize + gapIcon;
            }

            TextRenderer.DrawText(
                g,
                main,
                font,
                new Rectangle(x, 0, textSize.Width + 2, Height),
                fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            x += textSize.Width + gapSuffix;

            if (suffix.Length > 0)
            {
                TextRenderer.DrawText(
                    g,
                    suffix,
                    Theme.SmallFont,
                    new Rectangle(x, 1, suffixSize.Width + 2, Height),
                    suffixColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }
    }

    /// <summary>
    /// 叠在别的控件上、又需要"透明"效果的小控件基类。
    /// 把父控件自己的绘制内容按坐标偏移画进来，叠在渐变 / 圆角卡片上都不会有色块。
    /// </summary>
    public class OverlayControl : Control
    {
        public OverlayControl()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
            TabStop = false;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Parent == null)
            {
                base.OnPaintBackground(e);
                return;
            }
            Graphics g = e.Graphics;
            Matrix saved = g.Transform;
            try
            {
                g.TranslateTransform(-Left, -Top);
                Rectangle parentRect = new Rectangle(0, 0, Parent.ClientSize.Width + 8, Parent.ClientSize.Height + 8);
                using (PaintEventArgs pe = new PaintEventArgs(g, parentRect))
                {
                    InvokePaintBackground(Parent, pe);
                    InvokePaint(Parent, pe);
                }
            }
            finally
            {
                g.Transform = saved;
            }
        }
    }

    /// <summary>胶囊开关。</summary>
    public class ToggleSwitch : OverlayControl
    {
        private bool _checked;
        private bool _hover;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value)
                {
                    return;
                }
                _checked = value;
                Invalidate();
                EventHandler h = CheckedChanged;
                if (h != null)
                {
                    h(this, EventArgs.Empty);
                }
            }
        }

        public ToggleSwitch()
        {
            Size = new Size(40, 22);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int h = Height - 1;
            int w = Width - 1;
            Rectangle track = new Rectangle(0, 0, w, h);

            Color trackColor;
            if (_checked)
            {
                trackColor = _hover ? Theme.AccentHover : Theme.Accent;
            }
            else
            {
                trackColor = Theme.Mix(Theme.Track, Theme.Text, Theme.Dark ? 0.16 : 0.10);
            }

            using (GraphicsPath path = Theme.RoundedRect(track, h / 2))
            {
                using (SolidBrush b = new SolidBrush(trackColor))
                {
                    g.FillPath(b, path);
                }
            }

            int knob = h - 4;
            int kx = _checked ? (w - knob - 2) : 2;
            Rectangle knobRect = new Rectangle(kx, 2, knob, knob);
            using (SolidBrush b = new SolidBrush(_checked ? Color.White : Theme.Mix(Theme.Track, Color.White, 0.88)))
            {
                g.FillEllipse(b, knobRect);
            }
            using (Pen p = new Pen(Color.FromArgb(38, 0, 0, 0)))
            {
                g.DrawEllipse(p, knobRect);
            }
        }
    }

    /// <summary>状态小圆点。</summary>
    public class StatusDot : OverlayControl
    {
        private Color _color = Theme.Muted;

        public Color DotColor
        {
            get { return _color; }
            set
            {
                _color = value;
                Invalidate();
            }
        }

        public StatusDot()
        {
            Size = new Size(12, 12);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int d = Math.Min(Width, Height) - 1;
            Rectangle r = new Rectangle((Width - d) / 2, (Height - d) / 2, d, d);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(40, _color)))
            {
                g.FillEllipse(b, new Rectangle(r.X - 3, r.Y - 3, r.Width + 6, r.Height + 6));
            }
            using (SolidBrush b = new SolidBrush(_color))
            {
                g.FillEllipse(b, r);
            }
        }
    }

    /// <summary>顶部渐变标题条。</summary>
    public class HeaderPanel : Panel
    {
        public HeaderPanel()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (LinearGradientBrush b = new LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height)),
                Theme.HeaderStart,
                Theme.HeaderEnd,
                LinearGradientMode.Horizontal))
            {
                g.FillRectangle(b, ClientRectangle);
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 右侧两个柔和的装饰圆
            using (SolidBrush b = new SolidBrush(Color.FromArgb(22, 255, 255, 255)))
            {
                g.FillEllipse(b, Width - 150, -70, 190, 190);
            }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(14, 255, 255, 255)))
            {
                g.FillEllipse(b, Width - 250, 40, 120, 120);
            }

            // 底部一条淡淡的强调线
            using (LinearGradientBrush lb = new LinearGradientBrush(
                new Rectangle(0, Height - 2, Math.Max(1, Width), 2),
                Color.FromArgb(0, Theme.Accent),
                Theme.Accent,
                LinearGradientMode.Horizontal))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(0, 0, 0, 0)))
                {
                }
                g.FillRectangle(lb, 0, Height - 2, Width, 2);
            }
        }
    }

    /// <summary>最近几条操作记录。</summary>
    public class LogPanel : Control
    {
        private readonly List<string> _lines = new List<string>();

        public LogPanel()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            BackColor = Theme.Card;
        }

        public void SetLines(IList<string> lines)
        {
            _lines.Clear();
            if (lines != null)
            {
                for (int i = 0; i < lines.Count && i < 3; i++)
                {
                    _lines.Add(lines[i]);
                }
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            Theme.FillRounded(g, ClientRectangle, 8, Theme.CardAlt, Theme.Border);
            if (_lines.Count == 0)
            {
                TextRenderer.DrawText(
                    g,
                    "暂无记录",
                    Theme.TinyFont,
                    new Rectangle(12, 4, Width - 20, Height - 8),
                    Theme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                return;
            }
            int y = 6;
            for (int i = 0; i < _lines.Count; i++)
            {
                string line = _lines[i];
                bool isError = line.IndexOf("错误", StringComparison.Ordinal) >= 0
                    || line.IndexOf("失败", StringComparison.Ordinal) >= 0
                    || line.IndexOf("出错", StringComparison.Ordinal) >= 0;
                Color c = isError ? (i == 0 ? Theme.Danger : Theme.Alpha(Theme.Danger, 170)) : (i == 0 ? Theme.TextDim : Theme.Muted);
                TextRenderer.DrawText(
                    g,
                    line,
                    Theme.TinyFont,
                    new Rectangle(12, y, Width - 20, 16),
                    c,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                y += 17;
            }
        }
    }

    /// <summary>圆角背景 + 无边框输入框的组合。</summary>
    public class InputFrame : Panel
    {
        public TextBox Inner;
        public Color BaseColor = Theme.Background;

        public InputFrame(TextBox inner)
        {
            Inner = inner;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            BackColor = Theme.Background;
            Controls.Add(inner);
            inner.GotFocus += delegate { Invalidate(); };
            inner.LostFocus += delegate { Invalidate(); };
            PositionInner();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PositionInner();
        }

        private void PositionInner()
        {
            if (Inner == null)
            {
                return;
            }

            if (Inner.Multiline)
            {
                Inner.SetBounds(11, 10, Math.Max(10, Width - 22), Math.Max(20, Height - 20));
                return;
            }

            int textHeight = Math.Max(16, Inner.Font.Height);
            int y = Math.Max(2, (Height - textHeight) / 2);
            Inner.SetBounds(11, y, Math.Max(10, Width - 22), textHeight + 2);
        }


        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BaseColor);
            bool focused = Inner != null && Inner.Focused;
            Color border = focused ? Theme.Accent : Theme.InputBorder;
            Theme.FillRounded(g, ClientRectangle, Theme.RadiusInput, Theme.InputBack, border);
        }
    }

    /// <summary>只接受数字的输入框，支持滚轮 / 上下键微调。</summary>
    public class NumericBox : TextBox
    {
        public int Min = 0;
        public int Max = 1000000;
        public int Step = 10;

        private int _value;

        public int Value
        {
            get
            {
                int v;
                if (!int.TryParse(Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                {
                    v = _value;
                }
                return Fmt.Clamp(v, Min, Max);
            }
            set
            {
                _value = Fmt.Clamp(value, Min, Max);
                if (Text != _value.ToString(CultureInfo.InvariantCulture))
                {
                    Text = _value.ToString(CultureInfo.InvariantCulture);
                }
            }
        }

        public NumericBox()
        {
            BorderStyle = BorderStyle.None;
            BackColor = Theme.InputBack;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
            TextAlign = HorizontalAlignment.Left;
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            SelectAll();
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (e.KeyChar == '-' && Min < 0)
            {
                if (SelectionStart != 0 || Text.IndexOf('-') >= 0)
                {
                    e.Handled = true;
                }
            }
            else if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
            base.OnKeyPress(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (Focused)
            {
                int dir = e.Delta > 0 ? 1 : -1;
                Value = Value + dir * Step;
            }
            base.OnMouseWheel(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Up)
            {
                Value = Value + Step;
                return true;
            }
            if (keyData == Keys.Down)
            {
                Value = Value - Step;
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Value = Value;
            base.OnLostFocus(e);
        }
    }

    /// <summary>分段选择器：等宽几段，选中那段用强调色填充。</summary>
    public class Segmented : OverlayControl
    {
        private string[] _items = new string[0];
        private int _selectedIndex;
        private int _hoverIndex = -1;

        public event EventHandler SelectedIndexChanged;

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                int v = Fmt.Clamp(value, 0, Math.Max(0, _items.Length - 1));
                if (_selectedIndex == v)
                {
                    return;
                }
                _selectedIndex = v;
                Invalidate();
                EventHandler h = SelectedIndexChanged;
                if (h != null)
                {
                    h(this, EventArgs.Empty);
                }
            }
        }

        public Segmented()
        {
            Size = new Size(200, 32);
            Cursor = Cursors.Hand;
            Font = Theme.SmallFont;
        }

        public void SetItems(params string[] items)
        {
            _items = items ?? new string[0];
            _selectedIndex = Fmt.Clamp(_selectedIndex, 0, Math.Max(0, _items.Length - 1));
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        private int IndexAt(int x)
        {
            if (_items.Length == 0)
            {
                return -1;
            }
            int seg = Width / _items.Length;
            if (seg <= 0)
            {
                return -1;
            }
            int idx = x / seg;
            return Fmt.Clamp(idx, 0, _items.Length - 1);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = IndexAt(e.X);
            if (idx != _hoverIndex)
            {
                _hoverIndex = idx;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverIndex = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int idx = IndexAt(e.X);
            if (idx >= 0)
            {
                SelectedIndex = idx;
            }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Theme.FillRounded(g, ClientRectangle, Theme.RadiusButton, Theme.Track, Theme.Border);

            if (_items.Length == 0)
            {
                return;
            }

            int seg = Width / _items.Length;
            for (int i = 0; i < _items.Length; i++)
            {
                Rectangle r = new Rectangle(i * seg + 3, 3, seg - 6, Height - 7);
                bool active = i == _selectedIndex;
                bool hover = i == _hoverIndex && !active;

                if (active)
                {
                    Theme.FillRounded(g, r, Theme.RadiusButton - 2, Theme.AccentDim, Theme.Alpha(Theme.Accent, 110));
                }
                else if (hover)
                {
                    using (GraphicsPath path = Theme.RoundedRect(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), Theme.RadiusButton - 2))
                    using (SolidBrush b = new SolidBrush(Theme.Alpha(Theme.Text, 8)))
                    {
                        g.FillPath(b, path);
                    }
                }

                Color fore = active ? (Theme.Dark ? Theme.AccentText : Theme.AccentText) : Theme.Muted;
                TextRenderer.DrawText(
                    g,
                    _items[i],
                    Font,
                    new Rectangle(r.X, r.Y - 1, r.Width, r.Height),
                    fore,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            }
        }
    }

    // =====================================================================
    // 弹出菜单
    // =====================================================================
    public class MenuPopup : Form
    {
        public class MenuEntry
        {
            public string Text;
            public string Hint;
            public Glyph Icon;
            public Action Action;
            public bool IsSeparator;
            public bool Enabled = true;
        }

        private readonly List<MenuEntry> _entries = new List<MenuEntry>();
        private int _hover = -1;
        private const int ItemHeight = 36;
        private const int Pad = 6;

        public MenuPopup()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Card;
            DoubleBuffered = true;
            Font = Theme.UiFont;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        public void Add(string text, string hint, Glyph icon, Action action)
        {
            MenuEntry e = new MenuEntry();
            e.Text = text;
            e.Hint = hint;
            e.Icon = icon;
            e.Action = action;
            _entries.Add(e);
        }

        public void AddSeparator()
        {
            MenuEntry e = new MenuEntry();
            e.IsSeparator = true;
            _entries.Add(e);
        }

        public void ShowAt(Control anchor)
        {
            ShowAtPoint(anchor.PointToScreen(new Point(0, anchor.Height + 4)), anchor.FindForm(), anchor.PointToScreen(Point.Empty));
        }

        public void ShowAtPoint(Point screen, Form owner, Point fallbackAbove)
        {
            int width = 200;
            int height = Pad;
            foreach (MenuEntry e in _entries)
            {
                height += e.IsSeparator ? 9 : ItemHeight;
            }
            height += Pad;

            Size = new Size(width, height);

            Rectangle wa = owner != null ? Screen.FromControl(owner).WorkingArea : Screen.PrimaryScreen.WorkingArea;
            if (screen.X + width > wa.Right)
            {
                screen.X = wa.Right - width - 8;
            }
            if (screen.Y + height > wa.Bottom)
            {
                screen.Y = Math.Max(wa.Top + 8, fallbackAbove.Y - height);
            }
            if (screen.X < wa.Left)
            {
                screen.X = wa.Left + 8;
            }
            Location = screen;
            Show(owner);
            BringToFront();
        }

        private int IndexAt(int y)
        {
            int y0 = Pad;
            for (int i = 0; i < _entries.Count; i++)
            {
                int h = _entries[i].IsSeparator ? 9 : ItemHeight;
                if (y >= y0 && y < y0 + h && !_entries[i].IsSeparator)
                {
                    return i;
                }
                y0 += h;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = IndexAt(e.Y);
            if (idx != _hover)
            {
                _hover = idx;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int idx = IndexAt(e.Y);
            if (idx >= 0 && _entries[idx].Enabled && _entries[idx].Action != null)
            {
                Action a = _entries[idx].Action;
                Hide();
                a();
            }
            base.OnMouseClick(e);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Hide();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Hide();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Card);
            Theme.FillRounded(g, ClientRectangle, 10, Theme.Card, Theme.BorderStrong);

            int y = Pad;
            for (int i = 0; i < _entries.Count; i++)
            {
                MenuEntry entry = _entries[i];
                if (entry.IsSeparator)
                {
                    using (Pen p = new Pen(Theme.Border))
                    {
                        g.DrawLine(p, 12, y + 4, Width - 12, y + 4);
                    }
                    y += 9;
                    continue;
                }

                Rectangle row = new Rectangle(4, y, Width - 8, ItemHeight);
                if (i == _hover)
                {
                    Theme.FillRounded(g, row, 7, Theme.Elevated, Color.Transparent);
                }

                Color fore = entry.Enabled ? Theme.Text : Theme.Muted;
                if (entry.Icon != Glyph.None)
                {
                    GlyphPainter.Draw(g, entry.Icon, new Rectangle(row.X + 10, row.Y + 11, 14, 14), fore);
                }
                TextRenderer.DrawText(
                    g,
                    entry.Text,
                    Theme.UiFont,
                    new Rectangle(row.X + 32, row.Y, row.Width - 90, row.Height),
                    fore,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

                if (!string.IsNullOrEmpty(entry.Hint))
                {
                    TextRenderer.DrawText(
                        g,
                        entry.Hint,
                        Theme.TinyFont,
                        new Rectangle(row.Right - 66, row.Y, 56, row.Height),
                        Theme.Muted,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                }
                y += ItemHeight;
            }
        }
    }

    /// <summary>深色/浅色都能看的 ComboBox。</summary>
    public class ThemedCombo : ComboBox
    {
        public ThemedCombo()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            BackColor = Theme.InputBack;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 24;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0)
            {
                return;
            }
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color back = selected ? Theme.Selection : Theme.InputBack;
            using (SolidBrush b = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(b, e.Bounds);
            }
            TextRenderer.DrawText(
                e.Graphics,
                Items[e.Index].ToString(),
                Font,
                new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height),
                Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>DataGridView 主题化辅助。</summary>
    public static class GridStyle
    {
        public static void Apply(DataGridView g)
        {
            EnableDoubleBuffer(g);

            g.BackgroundColor = Theme.CardAlt;
            g.BorderStyle = BorderStyle.None;
            g.GridColor = Theme.Mix(Theme.CardAlt, Theme.Border, 0.85);
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.RowHeadersVisible = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.AllowUserToResizeColumns = false;
            g.AllowUserToOrderColumns = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = false;
            g.ReadOnly = false;
            g.EditMode = DataGridViewEditMode.EditProgrammatically;
            g.ScrollBars = ScrollBars.Vertical;
            g.Font = Theme.UiFont;
            g.RowTemplate.Height = 38;
            g.ColumnHeadersHeight = 36;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ShowCellToolTips = true;

            g.ColumnHeadersDefaultCellStyle.BackColor = Theme.GridHeader;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Muted;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.GridHeader;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Theme.Muted;
            g.ColumnHeadersDefaultCellStyle.Font = Theme.TinyFont;
            g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 0, 0);

            g.DefaultCellStyle.BackColor = Theme.CardAlt;
            g.DefaultCellStyle.ForeColor = Theme.Text;
            g.DefaultCellStyle.SelectionBackColor = Theme.Selection;
            g.DefaultCellStyle.SelectionForeColor = Theme.Text;
            g.DefaultCellStyle.Font = Theme.UiFont;
            g.DefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
            g.AlternatingRowsDefaultCellStyle.BackColor = Theme.CardAlt;
            g.AlternatingRowsDefaultCellStyle.SelectionBackColor = Theme.Selection;
            g.AlternatingRowsDefaultCellStyle.SelectionForeColor = Theme.Text;
        }

        public static void EnableDoubleBuffer(DataGridView g)
        {
            try
            {
                PropertyInfo pi = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
                if (pi != null)
                {
                    pi.SetValue(g, true, null);
                }
            }
            catch
            {
            }
        }

        /// <summary>画一个圆角复选框。</summary>
        public static void DrawCheckbox(Graphics g, Rectangle cell, bool on, bool hot)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int size = 18;
            Rectangle box = new Rectangle(
                cell.X + (cell.Width - size) / 2,
                cell.Y + (cell.Height - size) / 2,
                size,
                size);

            if (on)
            {
                Color top = hot ? Theme.AccentHover : Theme.Accent;
                Theme.FillRoundedGradient(g, box, 5, top, Theme.Mix(Theme.Accent, Color.Black, 0.15), Color.Transparent);
                GlyphPainter.Draw(g, Glyph.Check, new Rectangle(box.X + 1, box.Y + 1, box.Width - 2, box.Height - 2), Color.White);
            }
            else
            {
                Color fill = hot ? Theme.Elevated : Theme.Mix(Theme.CardAlt, Theme.Text, Theme.Dark ? 0.04 : 0.02);
                Theme.FillRounded(g, box, 5, fill, Theme.BorderStrong);
            }
        }
    }

    /// <summary>长得跟主题一致的普通输入框。</summary>
    public class NormalTextBox : TextBox
    {
        public NormalTextBox()
        {
            BackColor = Theme.InputBack;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
            BorderStyle = BorderStyle.None;
        }
    }



    /// <summary>带标题和一句解释的"选择卡"，比分段控件更适合需要说明的选项。</summary>
    public class ChoiceCard : OverlayControl
    {
        public string TitleText = string.Empty;
        public string DescText = string.Empty;
        private bool _selected;
        private bool _hover;

        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected == value)
                {
                    return;
                }
                _selected = value;
                Invalidate();
            }
        }

        public ChoiceCard()
        {
            Size = new Size(300, 56);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color fill = _selected ? Theme.AccentDim : (_hover ? Theme.Elevated : Theme.CardAlt);
            Color border = _selected
                ? Theme.Alpha(Theme.Accent, 160)
                : (_hover ? Theme.Alpha(Theme.Accent, 80) : Theme.Border);
            Theme.FillRounded(g, ClientRectangle, 9, fill, border);

            int d = 16;
            Rectangle circle = new Rectangle(Width - d - 14, (Height - d) / 2, d, d);
            using (Pen p = new Pen(_selected ? Theme.Accent : Theme.BorderStrong, 1.6f))
            {
                g.DrawEllipse(p, circle);
            }
            if (_selected)
            {
                using (SolidBrush b = new SolidBrush(Theme.Accent))
                {
                    g.FillEllipse(b, new Rectangle(circle.X + 4, circle.Y + 4, d - 8, d - 8));
                }
            }

            Color titleColor = _selected ? (Theme.Dark ? Theme.AccentText : Theme.Accent) : Theme.Text;
            TextRenderer.DrawText(
                g,
                TitleText,
                Theme.BoldFont,
                new Rectangle(14, 7, Width - 52, 22),
                titleColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(
                g,
                DescText,
                Theme.TinyFont,
                new Rectangle(14, 29, Width - 56, 18),
                Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }
    /// <summary>
    /// 自绘只读文本视图（日志 / 预览）。
    /// 不用原生滚动条：原生 EDIT 控件套深色主题后滚轮会花屏，这个不会。
    /// 等宽字体按宽度折行，默认跟随末尾。
    /// </summary>
    public class LogView : Control
    {
        private readonly List<string> _raw = new List<string>();
        private readonly List<string> _rows = new List<string>();
        private int _scroll;
        private bool _followTail = true;
        private int _charW;

        public bool ColorizeErrors = true;
        public Color TextColor = Theme.TextDim;
        public Color ErrorColor = Theme.Danger;
        public int LineHeight = 17;

        public LogView()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            BackColor = Theme.Background;
            Font = Theme.MonoFont;
            TabStop = true;
            Cursor = Cursors.IBeam;
        }

        public void SetText(string text)
        {
            _raw.Clear();
            string t = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            string[] parts = t.Split('\n');
            for (int i = 0; i < parts.Length; i++)
            {
                _raw.Add(parts[i]);
            }
            Rebuild();
        }

        public string RawText
        {
            get { return string.Join("\r\n", _raw.ToArray()); }
        }

        public int RawLineCount
        {
            get { return _raw.Count; }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Rebuild();
        }

        private int VisibleRows
        {
            get { return Math.Max(1, (Height - 12) / Math.Max(8, LineHeight)); }
        }

        private int MaxScroll
        {
            get { return Math.Max(0, _rows.Count - VisibleRows); }
        }

        private int CharWidth()
        {
            if (_charW <= 0)
            {
                Size s = TextRenderer.MeasureText("0", Font);
                _charW = Math.Max(4, s.Width);
            }
            return _charW;
        }

        private void Rebuild()
        {
            if (_rows == null)
            {
                return;
            }
            _rows.Clear();
            int cols = Math.Max(16, (Width - 26) / CharWidth());
            for (int i = 0; i < _raw.Count; i++)
            {
                string line = _raw[i];
                if (line.Length == 0)
                {
                    _rows.Add(string.Empty);
                    continue;
                }
                for (int p = 0; p < line.Length; )
                {
                    int take = Math.Min(cols, line.Length - p);
                    if (p + take < line.Length)
                    {
                        // 尽量在空格处断开，别把单词/数字拦腰截断
                        int sp = line.LastIndexOf(' ', p + take - 1, take);
                        if (sp > p + 8)
                        {
                            take = sp - p + 1;
                        }
                    }
                    _rows.Add(line.Substring(p, take));
                    p += take;
                }
            }

            int max = Math.Max(0, _rows.Count - VisibleRows);
            _scroll = _followTail ? max : Fmt.Clamp(_scroll, 0, max);
            Invalidate();
        }

        private void ScrollBy(int lines)
        {
            int max = MaxScroll;
            _scroll = Fmt.Clamp(_scroll + lines, 0, max);
            _followTail = _scroll >= max;
            Invalidate();
        }

        /// <summary>供窗体转发滚轮消息用（WM_MOUSEWHEEL 只会送到有焦点的窗口）。</summary>
        public void ApplyWheelDelta(int delta)
        {
            ScrollBy(-(delta / 120) * 3);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ApplyWheelDelta(e.Delta);
            base.OnMouseWheel(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!Focused)
            {
                Focus();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                MenuPopup menu = new MenuPopup();
                menu.Add("复制全部", "Ctrl+C", Glyph.Copy, delegate { CopyAll(); });
                menu.ShowAtPoint(PointToScreen(e.Location), FindForm(), PointToScreen(e.Location));
            }
            base.OnMouseUp(e);
        }

        private void CopyAll()
        {
            try
            {
                if (_raw.Count > 0)
                {
                    Clipboard.SetText(RawText);
                }
            }
            catch
            {
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.C))
            {
                CopyAll();
                return true;
            }
            if (keyData == Keys.Up)
            {
                ScrollBy(-1);
                return true;
            }
            if (keyData == Keys.Down)
            {
                ScrollBy(1);
                return true;
            }
            if (keyData == Keys.PageUp)
            {
                ScrollBy(-(VisibleRows - 1));
                return true;
            }
            if (keyData == Keys.PageDown)
            {
                ScrollBy(VisibleRows - 1);
                return true;
            }
            if (keyData == Keys.Home)
            {
                _scroll = 0;
                _followTail = false;
                Invalidate();
                return true;
            }
            if (keyData == Keys.End)
            {
                _scroll = MaxScroll;
                _followTail = true;
                Invalidate();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            Theme.FillRounded(
                g,
                ClientRectangle,
                8,
                Theme.InputBack,
                Focused ? Theme.Accent : Theme.InputBorder);

            int x = 10;
            int y = 6;
            int visible = VisibleRows;
            for (int i = 0; i < visible; i++)
            {
                int idx = _scroll + i;
                if (idx < 0 || idx >= _rows.Count)
                {
                    break;
                }
                string line = _rows[idx];
                Color c = TextColor;
                if (ColorizeErrors
                    && (line.IndexOf("错误", StringComparison.Ordinal) >= 0
                        || line.IndexOf("失败", StringComparison.Ordinal) >= 0
                        || line.IndexOf("出错", StringComparison.Ordinal) >= 0))
                {
                    c = ErrorColor;
                }
                TextRenderer.DrawText(
                    g,
                    line,
                    Font,
                    new Rectangle(x, y, Width - 22, LineHeight),
                    c,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                y += LineHeight;
            }

            if (_rows.Count > visible)
            {
                int trackTop = 8;
                int trackH = Height - 16;
                int thumbH = Math.Max(24, (int)(trackH * (double)visible / _rows.Count));
                int max = Math.Max(1, _rows.Count - visible);
                int thumbTop = trackTop + (int)((trackH - thumbH) * (_scroll / (double)max));
                Theme.FillRounded(
                    g,
                    new Rectangle(Width - 9, thumbTop, 4, thumbH),
                    2,
                    Theme.Alpha(Theme.Text, 70),
                    Color.Transparent);
            }
        }
    }
}

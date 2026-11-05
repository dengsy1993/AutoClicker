using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace AutoClicker
{
    /// <summary>
    /// 输入探针：一个用来验证“合成输入到底有没有到达别的窗口”的小窗口。
    /// 把鼠标移上去点几下，或者用主程序的 F9 测试，看计数是否增加。
    /// </summary>
    public class ProbeForm : Form
    {
        public int LeftClicks;
        public int RightClicks;
        public int MiddleClicks;
        public int MiddleDowns;
        public int KeyPresses;
        public int LastX = -1;
        public int LastY = -1;
        public string LastKey = string.Empty;
        public int ActivateCount;
        /// <summary>收到的“右键抬起”消息数。正常情况下应该是 0。</summary>
        public int RightUpCount;
        public int RightDownCount;
        /// <summary>收到的滚轮消息数。</summary>
        public int WheelEvents;
        /// <summary>滚轮累计格数（正向上、负向下）。</summary>
        public int WheelNotches;

        private readonly RoundButton _reset;
        private DateTime _openedAt = DateTime.Now;

        public ProbeForm()
        {
            Text = "输入探针";
            ClientSize = new Size(420, 280);
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            TopMost = true;
            BackColor = Theme.CardAlt;
            Font = Theme.UiFont;
            KeyPreview = true;
            DoubleBuffered = true;

            _reset = new RoundButton();
            _reset.Text = "清零";
            _reset.Size = new Size(64, 28);
            _reset.Location = new Point(ClientSize.Width - 76, ClientSize.Height - 40);
            _reset.BackColor = Theme.CardAlt;
            _reset.Click += delegate
            {
                ResetCounters();
                Invalidate();
            };
            Controls.Add(_reset);
        }

        public void ResetCounters()
        {
            LeftClicks = 0;
            RightClicks = 0;
            MiddleClicks = 0;
            MiddleDowns = 0;
            KeyPresses = 0;
            LastX = -1;
            LastY = -1;
            LastKey = string.Empty;
            ActivateCount = 0;
            RightUpCount = 0;
            RightDownCount = 0;
            WheelEvents = 0;
            WheelNotches = 0;
            _openedAt = DateTime.Now;
        }

        public int TotalClicks { get { return LeftClicks + RightClicks + MiddleClicks; } }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Theme.ApplyTitleBar(this, Theme.Dark);
        }

        protected override void WndProc(ref Message m)
        {
            // 0x0205 = WM_RBUTTONUP，0x0204 = WM_RBUTTONDOWN
            // 只“抬起”不“按下”也会走到这里，而且目标程序会当成一次真实右键。
            if (m.Msg == 0x0205)
            {
                RightUpCount++;
                Invalidate();
            }
            else if (m.Msg == 0x0204)
            {
                RightDownCount++;
            }
            else if (m.Msg == 0x020A)
            {
                // WM_MOUSEWHEEL：高 16 位是有符号的滚动量，一格 = 120
                WheelEvents++;
                long wp = m.WParam.ToInt64();
                int delta = (short)((wp >> 16) & 0xFFFF);
                WheelNotches += delta / 120;
                Invalidate();
            }
            base.WndProc(ref m);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            LastX = e.X;
            LastY = e.Y;
            if (e.Button == MouseButtons.Left)
            {
                LeftClicks++;
            }
            else if (e.Button == MouseButtons.Right)
            {
                RightClicks++;
            }
            else if (e.Button == MouseButtons.Middle)
            {
                MiddleClicks++;
            }
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            KeyPresses++;
            LastKey = HotkeyText.Describe(e.KeyCode, Keys.None);
            Invalidate();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            ActivateCount++;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.CardAlt);

            using (SolidBrush b = new SolidBrush(Theme.Text))
            {
                g.DrawString("输入探针", new Font("Microsoft YaHei UI", 13F, FontStyle.Bold), b, 16, 12);
            }
            using (SolidBrush b = new SolidBrush(Theme.Muted))
            {
                g.DrawString("把鼠标放到这个窗口里点几下，看下面计数是否增加", Theme.SmallFont, b, 17, 44);
            }

            // 计数块
            DrawStat(g, 16, 72, "左键", LeftClicks.ToString(CultureInfo.InvariantCulture), Theme.Accent);
            DrawStat(g, 152, 72, "右键", RightClicks.ToString(CultureInfo.InvariantCulture), Theme.Success);
            DrawStat(g, 288, 72, "中键", MiddleClicks.ToString(CultureInfo.InvariantCulture), Theme.Muted);

            using (SolidBrush b = new SolidBrush(Theme.Muted))
            {
                string coords = LastX >= 0
                    ? string.Format(CultureInfo.InvariantCulture, "最后点击：客户区 ({0}, {1})", LastX, LastY)
                    : "最后点击：—";
                g.DrawString(coords, Theme.SmallFont, b, 17, 138);
                g.DrawString(
                    string.Format(CultureInfo.InvariantCulture, "按键次数：{0}   最后一个键：{1}", KeyPresses, LastKey.Length == 0 ? "—" : LastKey),
                    Theme.SmallFont,
                    b,
                    17,
                    158);
                g.DrawString(
                    string.Format(CultureInfo.InvariantCulture, "被激活次数：{0}", ActivateCount),
                    Theme.SmallFont,
                    b,
                    17,
                    178);
                using (SolidBrush rightBrush = new SolidBrush(RightUpCount > 0 ? Theme.Danger : Theme.Muted))
                {
                    g.DrawString(
                        string.Format(CultureInfo.InvariantCulture, "右键事件：按下 {0} / 抬起 {1}（正常应都是 0）", RightDownCount, RightUpCount),
                        Theme.SmallFont,
                        rightBrush,
                        17,
                        198);
                }
                g.DrawString(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "客户区原点：({0}, {1})   尺寸：{2} × {3}",
                        PointToScreen(Point.Empty).X,
                        PointToScreen(Point.Empty).Y,
                        ClientSize.Width,
                        ClientSize.Height),
                    Theme.SmallFont,
                    b,
                    17,
                    218);
                g.DrawString(
                    "点得动但计数不动 = 合成点击被拦了",
                    Theme.SmallFont,
                    b,
                    17,
                    238);
                using (SolidBrush wheelBrush = new SolidBrush(Theme.Muted))
                {
                    g.DrawString(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "滚轮事件：{0} 次 / 共 {1} 格",
                            WheelEvents,
                            WheelNotches),
                        Theme.SmallFont,
                        wheelBrush,
                        17,
                        258);
                }
            }
        }

        private void DrawStat(Graphics g, int x, int y, string label, string value, Color color)
        {
            Rectangle card = new Rectangle(x, y, 120, 52);
            Theme.FillRounded(g, card, 8, Theme.Card, Theme.Border);
            using (SolidBrush b = new SolidBrush(Theme.Muted))
            {
                g.DrawString(label, Theme.SmallFont, b, x + 10, y + 6);
            }
            using (SolidBrush b = new SolidBrush(color))
            {
                g.DrawString(value, new Font("Consolas", 15F, FontStyle.Bold), b, x + 10, y + 20);
            }
        }
    }
}

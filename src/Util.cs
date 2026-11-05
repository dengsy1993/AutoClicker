using System;
using System.Globalization;
using System.Windows.Forms;

namespace AutoClicker
{
    /// <summary>热键文本工具。</summary>
    public static class HotkeyText
    {
        public static string Describe(Keys keyCode, Keys modifiers)
        {
            string s = string.Empty;
            if ((modifiers & Keys.Control) != 0) s += "Ctrl+";
            if ((modifiers & Keys.Alt) != 0) s += "Alt+";
            if ((modifiers & Keys.Shift) != 0) s += "Shift+";
            s += KeyName(keyCode);
            return s;
        }

        public static string KeyName(Keys key)
        {
            // 去掉修饰位
            Keys k = key & ~Keys.Modifiers & ~Keys.Control & ~Keys.Alt & ~Keys.Shift;
            if (k == Keys.None)
            {
                k = key;
            }
            switch (k)
            {
                case Keys.Space: return "Space";
                case Keys.Enter: return "Enter";
                case Keys.Escape: return "Esc";
                case Keys.Tab: return "Tab";
                case Keys.Back: return "Backspace";
                case Keys.Delete: return "Delete";
                case Keys.Insert: return "Insert";
                case Keys.Home: return "Home";
                case Keys.End: return "End";
                case Keys.PageUp: return "PageUp";
                case Keys.PageDown: return "PageDown";
                case Keys.Up: return "↑";
                case Keys.Down: return "↓";
                case Keys.Left: return "←";
                case Keys.Right: return "→";
                case Keys.ControlKey: return "Ctrl";
                case Keys.ShiftKey: return "Shift";
                case Keys.Menu: return "Alt";
                case Keys.LWin: return "Win";
            }
            if (k >= Keys.D0 && k <= Keys.D9)
            {
                return ((int)(k - Keys.D0)).ToString(CultureInfo.InvariantCulture);
            }
            if (k >= Keys.NumPad0 && k <= Keys.NumPad9)
            {
                return "Num" + ((int)(k - Keys.NumPad0)).ToString(CultureInfo.InvariantCulture);
            }
            return k.ToString();
        }

        /// <summary>从 KeyEventArgs 抽出主键 + 修饰键。</summary>
        public static void Split(KeyEventArgs e, out Keys keyCode, out Keys modifiers)
        {
            modifiers = Keys.None;
            if (e.Control) modifiers |= Keys.Control;
            if (e.Alt) modifiers |= Keys.Alt;
            if (e.Shift) modifiers |= Keys.Shift;

            Keys k = e.KeyCode;
            if (k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu || k == Keys.LWin || k == Keys.RWin)
            {
                keyCode = Keys.None;
                return;
            }
            keyCode = k;
        }

        public static bool IsValidKey(Keys key)
        {
            return key != Keys.None && key != Keys.ControlKey && key != Keys.ShiftKey && key != Keys.Menu;
        }
    }

    /// <summary>界面文本小工具。</summary>
    public static class Fmt
    {
        /// <summary>等待毫秒 -> “≈ 0.12 秒 · 8.3 次/秒”。</summary>
        public static string Wait(int ms)
        {
            if (ms <= 0)
            {
                return "无等待";
            }
            double sec = ms / 1000.0;
            string a = "≈ " + sec.ToString("0.00", CultureInfo.InvariantCulture) + " 秒";
            if (ms > 0)
            {
                double hz = 1000.0 / ms;
                a += " · " + hz.ToString(hz >= 10 ? "0" : "0.0", CultureInfo.InvariantCulture) + " 次/秒";
            }
            return a;
        }

        public static string Seconds(int ms)
        {
            return "≈ " + (ms / 1000.0).ToString("0.00", CultureInfo.InvariantCulture) + " 秒";
        }

        public static string Time(DateTime t)
        {
            return t.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        public static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public static MouseInputMethod ParseInputMethod(string s)
        {
            return string.Equals(s, "mouse_event", StringComparison.OrdinalIgnoreCase)
                ? MouseInputMethod.MouseEvent
                : MouseInputMethod.SendInput;
        }

        public static string InputMethodName(MouseInputMethod m)
        {
            return m == MouseInputMethod.MouseEvent ? "mouse_event（兼容旧程序）" : "SendInput（推荐）";
        }
    }
}

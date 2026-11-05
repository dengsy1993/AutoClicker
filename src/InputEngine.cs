using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AutoClicker
{
    /// <summary>
    /// 输入执行层。
    /// 设计原则：
    ///  1. 移动用 SendInput 绝对坐标（VirtualDesktop 归一化），失败再退回 SetCursorPos；
    ///  2. 每次移动后都用 GetCursorPos 校验，校验失败会明确报错，不会“假装成功”；
    ///  3. 点击用 SendInput（可选 mouse_event 兼容模式），按下/抬起分离，可配置按住时长；
    ///  4. 所有返回值 + 错误码写进 AppLog，方便定位。
    /// </summary>
    public static class InputEngine
    {
        private static int InputSize
        {
            get { return Marshal.SizeOf(typeof(Native.INPUT)); }
        }

        // ------------------------------------------------------------------
        // 启动自检：本进程到底有没有权限/能力控制鼠标
        // ------------------------------------------------------------------

        /// <summary>
        /// 无损自检：把光标设置到它当前所在的位置（不会真的移动），
        /// 返回 false 说明本进程被系统/安全软件限制，后面所有点击都不会生效。
        /// </summary>
        public static bool CanControlCursor(out string detail)
        {
            detail = string.Empty;
            Native.POINT p;
            if (!Native.GetCursorPos(out p))
            {
                detail = "GetCursorPos 失败";
                return false;
            }
            bool ok = Native.SetCursorPos(p.X, p.Y);
            if (ok)
            {
                detail = "SetCursorPos 可用";
                return true;
            }
            int err = Marshal.GetLastWin32Error();
            detail = "SetCursorPos 返回失败（Win32Error=" + err + "）";
            return false;
        }

        // ------------------------------------------------------------------
        // 移动
        // ------------------------------------------------------------------

        /// <summary>把鼠标移动到屏幕物理坐标。返回是否真的移动到了目标点附近。</summary>
        public static bool MoveTo(int x, int y, out string detail)
        {
            detail = string.Empty;
            if (!Native.IsPointOnScreen(x, y))
            {
                Native.RECT vs = Native.GetVirtualScreen();
                detail = string.Format(
                    CultureInfo.InvariantCulture,
                    "坐标超出屏幕范围：({0},{1})，虚拟屏幕为 [{2},{3}] - [{4},{5}]",
                    x,
                    y,
                    vs.Left,
                    vs.Top,
                    vs.Right,
                    vs.Bottom);
                return false;
            }

            string method = "SendInput";
            bool ok = MoveBySendInput(x, y, out detail);
            if (!ok)
            {
                method = "SetCursorPos";
                string why = detail;
                ok = Native.SetCursorPos(x, y);
                int err = ok ? 0 : Marshal.GetLastWin32Error();
                detail = string.Format(
                    CultureInfo.InvariantCulture,
                    "SendInput 移动失败（{0}），改用 SetCursorPos 返回 {1}{2}",
                    why,
                    ok ? "成功" : "失败",
                    ok ? string.Empty : " Win32Error=" + err);
            }

            Native.POINT p;
            bool got = Native.GetCursorPos(out p);
            int dx = got ? p.X - x : int.MaxValue;
            int dy = got ? p.Y - y : int.MaxValue;
            bool landed = got && Math.Abs(dx) <= 2 && Math.Abs(dy) <= 2;

            detail = string.Format(
                CultureInfo.InvariantCulture,
                "{0} -> ({1},{2}) 光标实际=({3},{4}) 偏差=({5},{6}) {7}",
                method,
                x,
                y,
                got ? p.X.ToString(CultureInfo.InvariantCulture) : "?",
                got ? p.Y.ToString(CultureInfo.InvariantCulture) : "?",
                got ? dx.ToString(CultureInfo.InvariantCulture) : "?",
                got ? dy.ToString(CultureInfo.InvariantCulture) : "?",
                landed ? "OK" : (ok ? "落点不符" : "失败"));

            return landed;
        }

        private static bool MoveBySendInput(int x, int y, out string detail)
        {
            detail = string.Empty;
            Native.RECT vs = Native.GetVirtualScreen();
            int w = Math.Max(1, vs.Width - 1);
            int h = Math.Max(1, vs.Height - 1);

            double nx = (x - vs.Left) * 65535.0 / w;
            double ny = (y - vs.Top) * 65535.0 / h;
            int ax = (int)Math.Round(nx, MidpointRounding.AwayFromZero);
            int ay = (int)Math.Round(ny, MidpointRounding.AwayFromZero);

            Native.INPUT input = new Native.INPUT();
            input.type = Native.INPUT_MOUSE;
            input.U.mi.dx = ax;
            input.U.mi.dy = ay;
            input.U.mi.mouseData = 0;
            input.U.mi.dwFlags = Native.MOUSEEVENTF_MOVE | Native.MOUSEEVENTF_ABSOLUTE | Native.MOUSEEVENTF_VIRTUALDESK;
            input.U.mi.time = 0;
            input.U.mi.dwExtraInfo = IntPtr.Zero;

            Native.INPUT[] arr = new Native.INPUT[] { input };
            uint sent = Native.SendInput(1, arr, InputSize);
            if (sent != 1)
            {
                detail = "SendInput 返回 " + sent + " Win32Error=" + Marshal.GetLastWin32Error();
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------------
        // 点击
        // ------------------------------------------------------------------

        public static bool Click(MouseBtn button, int holdMs, MouseInputMethod method, out string detail)
        {
            detail = string.Empty;
            int hold = Math.Max(1, holdMs);
            if (method == MouseInputMethod.MouseEvent)
            {
                return ClickByMouseEvent(button, hold, out detail);
            }
            return ClickBySendInput(button, hold, out detail);
        }

        private static bool ClickBySendInput(MouseBtn button, int hold, out string detail)
        {
            detail = string.Empty;
            uint downFlag;
            uint upFlag;
            ButtonFlags(button, out downFlag, out upFlag);

            Native.INPUT down = MakeMouse(downFlag);
            Native.INPUT up = MakeMouse(upFlag);

            // 按下与抬起分开两次 SendInput：中间可以真正“按住”。
            uint r1 = Native.SendInput(1, new Native.INPUT[] { down }, InputSize);
            if (r1 != 1)
            {
                detail = "按下 SendInput 返回 " + r1 + " Win32Error=" + Marshal.GetLastWin32Error();
                return false;
            }
            SetButtonPressed(button, true);

            bool upOk = false;
            try
            {
                if (hold > 0)
                {
                    Thread.Sleep(hold);
                }
            }
            finally
            {
                // 不管中间发生什么（包括线程被中止），这个键一定要抬起来
                uint r2 = Native.SendInput(1, new Native.INPUT[] { up }, InputSize);
                upOk = r2 == 1;
                if (upOk)
                {
                    SetButtonPressed(button, false);
                }
            }

            if (!upOk)
            {
                detail = "抬起 SendInput 失败，键仍处于按下状态";
                return false;
            }
            detail = "SendInput 按下/抬起 OK（按住 " + hold + "ms）";
            return true;
        }

        private static bool ClickByMouseEvent(MouseBtn button, int hold, out string detail)
        {
            detail = string.Empty;
            uint downFlag;
            uint upFlag;
            ButtonFlags(button, out downFlag, out upFlag);
            Native.mouse_event(downFlag, 0, 0, 0, UIntPtr.Zero);
            SetButtonPressed(button, true);
            try
            {
                if (hold > 0)
                {
                    Thread.Sleep(hold);
                }
            }
            finally
            {
                Native.mouse_event(upFlag, 0, 0, 0, UIntPtr.Zero);
                SetButtonPressed(button, false);
            }
            detail = "mouse_event 按下/抬起 OK（按住 " + hold + "ms）";
            return true;
        }

        // 记录哪些键真的被我们按下去了，松键时只松这些。
        // 不加这个判断的话，"无条件下发一次 RIGHTUP" 在目标程序里就是一个真实的右键
        // （实测会收到 WM_RBUTTONUP + WM_CONTEXTMENU，弹出右键菜单）。
        private static volatile bool _leftDown;
        private static volatile bool _rightDown;
        private static volatile bool _middleDown;
        private static volatile bool _ctrlDown;
        private static volatile bool _shiftDown;
        private static volatile bool _altDown;

        private static void SetButtonPressed(MouseBtn button, bool down)
        {
            switch (button)
            {
                case MouseBtn.Right:
                    _rightDown = down;
                    break;
                case MouseBtn.Middle:
                    _middleDown = down;
                    break;
                default:
                    _leftDown = down;
                    break;
            }
        }

        /// <summary>
        /// 发滚轮。notches 正数向上、负数向下，一格 = 120。
        /// 滚轮不需要"按下/抬起"，一次 SendInput 就够了。
        /// </summary>
        public static bool SendWheel(int notches, out string detail)
        {
            detail = string.Empty;
            if (notches == 0)
            {
                detail = "滚动量为 0";
                return false;
            }

            Native.INPUT input = new Native.INPUT();
            input.type = Native.INPUT_MOUSE;
            input.U.mi.dx = 0;
            input.U.mi.dy = 0;
            input.U.mi.mouseData = unchecked((uint)(notches * 120));
            input.U.mi.dwFlags = Native.MOUSEEVENTF_WHEEL;
            input.U.mi.time = 0;
            input.U.mi.dwExtraInfo = IntPtr.Zero;

            uint sent = Native.SendInput(1, new Native.INPUT[] { input }, InputSize);
            if (sent != 1)
            {
                detail = "滚轮 SendInput 返回 " + sent + " Win32Error=" + Marshal.GetLastWin32Error();
                return false;
            }

            detail = "滚轮 向" + (notches > 0 ? "上" : "下") + " " + Math.Abs(notches) + " 格";
            return true;
        }
        private static void ButtonFlags(MouseBtn button, out uint down, out uint up)
        {
            switch (button)
            {
                case MouseBtn.Right:
                    down = Native.MOUSEEVENTF_RIGHTDOWN;
                    up = Native.MOUSEEVENTF_RIGHTUP;
                    break;
                case MouseBtn.Middle:
                    down = Native.MOUSEEVENTF_MIDDLEDOWN;
                    up = Native.MOUSEEVENTF_MIDDLEUP;
                    break;
                default:
                    down = Native.MOUSEEVENTF_LEFTDOWN;
                    up = Native.MOUSEEVENTF_LEFTUP;
                    break;
            }
        }

        private static Native.INPUT MakeMouse(uint flags)
        {
            Native.INPUT input = new Native.INPUT();
            input.type = Native.INPUT_MOUSE;
            input.U.mi.dx = 0;
            input.U.mi.dy = 0;
            input.U.mi.mouseData = 0;
            input.U.mi.dwFlags = flags;
            input.U.mi.time = 0;
            input.U.mi.dwExtraInfo = IntPtr.Zero;
            return input;
        }

        // ------------------------------------------------------------------
        // 键盘
        // ------------------------------------------------------------------

        public static bool SendKey(Keys keyCode, Keys modifiers, int holdMs, out string detail)
        {
            detail = string.Empty;
            if (keyCode == Keys.None)
            {
                detail = "没有按键";
                return false;
            }

            List<Native.INPUT> seq = new List<Native.INPUT>();
            if ((modifiers & Keys.Control) != 0)
            {
                seq.Add(MakeKey(Keys.ControlKey, false, false));
                _ctrlDown = true;
            }
            if ((modifiers & Keys.Alt) != 0)
            {
                seq.Add(MakeKey(Keys.Menu, false, false));
                _altDown = true;
            }
            if ((modifiers & Keys.Shift) != 0)
            {
                seq.Add(MakeKey(Keys.ShiftKey, false, false));
                _shiftDown = true;
            }

            bool extended = IsExtendedKey((int)keyCode);
            seq.Add(MakeKey(keyCode, false, extended));

            int downCount = seq.Count;
            uint sent = Native.SendInput((uint)downCount, seq.ToArray(), InputSize);
            if (sent != downCount)
            {
                detail = "按键按下 SendInput 返回 " + sent + "/" + downCount + " Win32Error=" + Marshal.GetLastWin32Error();
                ReleaseModifiers();
                return false;
            }

            bool upOk = false;
            try
            {
                Thread.Sleep(Math.Max(8, holdMs));
            }
            finally
            {
                List<Native.INPUT> ups = new List<Native.INPUT>();
                ups.Add(MakeKey(keyCode, true, extended));
                if (_shiftDown) ups.Add(MakeKey(Keys.ShiftKey, true, false));
                if (_altDown) ups.Add(MakeKey(Keys.Menu, true, false));
                if (_ctrlDown) ups.Add(MakeKey(Keys.ControlKey, true, false));

                uint sentUp = Native.SendInput((uint)ups.Count, ups.ToArray(), InputSize);
                upOk = sentUp == ups.Count;
                if (upOk)
                {
                    _ctrlDown = false;
                    _altDown = false;
                    _shiftDown = false;
                }
                else
                {
                    detail = "按键抬起 SendInput 返回 " + sentUp + "/" + ups.Count;
                }
            }

            if (!upOk)
            {
                return false;
            }
            detail = "按下/抬起 OK: " + HotkeyText.Describe(keyCode, modifiers);
            return true;
        }

        private static Native.INPUT MakeKey(Keys key, bool up, bool extended)
        {
            ushort vk = (ushort)key;
            Native.INPUT input = new Native.INPUT();
            input.type = Native.INPUT_KEYBOARD;
            input.U.ki.wVk = vk;
            input.U.ki.wScan = (ushort)Native.MapVirtualKey(vk, 0);
            input.U.ki.dwFlags = (up ? Native.KEYEVENTF_KEYUP : 0u) | (extended ? Native.KEYEVENTF_EXTENDEDKEY : 0u);
            input.U.ki.time = 0;
            input.U.ki.dwExtraInfo = IntPtr.Zero;
            return input;
        }

        public static bool IsExtendedKey(int vk)
        {
            switch (vk)
            {
                case 0xA3: // VK_RCONTROL
                case 0xA5: // VK_RMENU
                case 0x2D: // INSERT
                case 0x2E: // DELETE
                case 0x24: // HOME
                case 0x23: // END
                case 0x21: // PRIOR
                case 0x22: // NEXT
                case 0x25: // LEFT
                case 0x26: // UP
                case 0x27: // RIGHT
                case 0x28: // DOWN
                case 0x90: // NUMLOCK
                case 0x6F: // DIVIDE
                case 0x5B: // LWIN
                case 0x5C: // RWIN
                case 0x5D: // APPS
                case 0x2C: // SNAPSHOT
                    return true;
                default:
                    return false;
            }
        }

        private static void ReleaseModifiers()
        {
            try
            {
                List<Native.INPUT> ups = new List<Native.INPUT>();
                if (_shiftDown) ups.Add(MakeKey(Keys.ShiftKey, true, false));
                if (_ctrlDown) ups.Add(MakeKey(Keys.ControlKey, true, false));
                if (_altDown) ups.Add(MakeKey(Keys.Menu, true, false));
                if (ups.Count == 0)
                {
                    _ctrlDown = false;
                    _shiftDown = false;
                    _altDown = false;
                    return;
                }
                Native.SendInput((uint)ups.Count, ups.ToArray(), InputSize);
                _ctrlDown = false;
                _shiftDown = false;
                _altDown = false;
            }
            catch
            {
            }
        }

        /// <summary>
        /// 松开“确实被我们按下、但还没抬起”的键（异常退出 / 紧急停止时用）。
        /// 只松真的按下过的键：无条件下发一个 RIGHTUP，在目标程序看来就是一个真实右键。
        /// </summary>
        public static void ReleaseAll()
        {
            try
            {
                List<Native.INPUT> ups = new List<Native.INPUT>();
                if (_shiftDown) ups.Add(MakeKey(Keys.ShiftKey, true, false));
                if (_ctrlDown) ups.Add(MakeKey(Keys.ControlKey, true, false));
                if (_altDown) ups.Add(MakeKey(Keys.Menu, true, false));
                if (_leftDown) ups.Add(MakeMouse(Native.MOUSEEVENTF_LEFTUP));
                if (_rightDown) ups.Add(MakeMouse(Native.MOUSEEVENTF_RIGHTUP));
                if (_middleDown) ups.Add(MakeMouse(Native.MOUSEEVENTF_MIDDLEUP));

                if (ups.Count == 0)
                {
                    return;
                }

                AppLog.Write("松开残留按键：" + ups.Count + " 个");
                Native.SendInput((uint)ups.Count, ups.ToArray(), InputSize);
                _leftDown = false;
                _rightDown = false;
                _middleDown = false;
                _ctrlDown = false;
                _shiftDown = false;
                _altDown = false;
            }
            catch
            {
            }
        }
    }
}

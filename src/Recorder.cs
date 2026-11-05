using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoClicker
{
    /// <summary>全局热键注册。</summary>
    public static class Hotkeys
    {
        public const int IdStart = 1;
        public const int IdCapture = 2;
        public const int IdStop = 3;
        public const int IdTest = 4;
        public const int IdRecord = 5;

        /// <summary>注册一个热键。按键为虚拟键码（F6 = 0x75）。</summary>
        public static bool Register(IntPtr hwnd, int id, uint vk)
        {
            try
            {
                return Native.RegisterHotKey(hwnd, id, Native.MOD_NOREPEAT, vk);
            }
            catch
            {
                return false;
            }
        }

        public static void Unregister(IntPtr hwnd, int id)
        {
            try
            {
                Native.UnregisterHotKey(hwnd, id);
            }
            catch
            {
            }
        }

        public static void UnregisterAll(IntPtr hwnd)
        {
            Unregister(hwnd, IdStart);
            Unregister(hwnd, IdCapture);
            Unregister(hwnd, IdStop);
            Unregister(hwnd, IdTest);
            Unregister(hwnd, IdRecord);
        }
    }

    /// <summary>
    /// 录制器：低级鼠标/键盘钩子，只记录，不拦截任何输入
    /// （每次回调都 CallNextHookEx，绝不可能把鼠标卡住）。
    /// </summary>
    public class Recorder : IDisposable
    {
        private const int MaxActions = 2000;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const uint LLMHF_INJECTED = 0x00000001;
        private const uint LLKHF_INJECTED = 0x00000010;

        private sealed class HostForm : Form
        {
            public Recorder Rec;

            public HostForm()
            {
                ShowInTaskbar = false;
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                Location = new System.Drawing.Point(-32000, -32000);
                Size = new System.Drawing.Size(1, 1);
                Opacity = 0;
                Visible = false;
            }
        }

        private readonly object _sync = new object();
        private readonly List<MacroAction> _actions = new List<MacroAction>();

        private System.Threading.Thread _thread;
        private HostForm _host;
        private IntPtr _mouseHook = IntPtr.Zero;
        private IntPtr _keyHook = IntPtr.Zero;
        private Native.LowLevelProc _mouseProc;
        private Native.LowLevelProc _keyProc;

        private volatile bool _recording;
        private volatile bool _stopping;
        private volatile int _count;
        private uint _lastTick;
        private bool _hasLast;
        private bool _lastWasClick;
        private bool _lastWasWheel;
        private int _wheelAccum;
        private int _lastX;
        private int _lastY;
        private uint _lastDownButton;
        private readonly HashSet<int> _keysDown = new HashSet<int>();
        private string _lastError = string.Empty;

        public bool IsRecording { get { return _recording; } }
        public int Count { get { return _count; } }
        public string LastError { get { return _lastError; } }

        public int OwnPid { get; set; }

        public bool Start()
        {
            if (_recording)
            {
                return false;
            }
            lock (_sync)
            {
                _actions.Clear();
                _count = 0;
                _hasLast = false;
            _lastWasWheel = false;
            _wheelAccum = 0;
                _lastError = string.Empty;
                _keysDown.Clear();
            }
            _stopping = false;
            _recording = true;

            _thread = new System.Threading.Thread(ThreadMain);
            _thread.IsBackground = true;
            _thread.Name = "AutoClickerRecorder";
            _thread.SetApartmentState(System.Threading.ApartmentState.STA);
            _thread.Start();
            return true;
        }

        public void Stop()
        {
            _stopping = true;
            HostForm host = _host;
            if (host != null)
            {
                try
                {
                    host.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            host.Close();
                        }
                        catch
                        {
                        }
                    }));
                }
                catch
                {
                }
            }
        }

        private void ThreadMain()
        {
            try
            {
                _host = new HostForm();
                _host.Rec = this;
                IntPtr hInst = Native.GetModuleHandle(null);
                _mouseProc = MouseHookProc;
                _keyProc = KeyHookProc;
                _mouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _mouseProc, hInst, 0);
                _keyHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _keyProc, hInst, 0);

                if (_mouseHook == IntPtr.Zero || _keyHook == IntPtr.Zero)
                {
                    _lastError = "安装键盘/鼠标钩子失败，Win32Error=" + Marshal.GetLastWin32Error();
                    AppLog.WriteError("录制启动失败：" + _lastError);
                    CleanupHooks();
                    _recording = false;
                    return;
                }

                AppLog.Write("录制已开始（低级钩子，只记录不拦截）");
                Application.Run(_host);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                AppLog.WriteError("录制异常：" + ex.Message);
            }
            finally
            {
                CleanupHooks();
                _recording = false;
                _host = null;
                AppLog.Write("录制已结束，共 " + _count + " 个动作");
            }
        }

        private void CleanupHooks()
        {
            try
            {
                if (_mouseHook != IntPtr.Zero)
                {
                    Native.UnhookWindowsHookEx(_mouseHook);
                    _mouseHook = IntPtr.Zero;
                }
                if (_keyHook != IntPtr.Zero)
                {
                    Native.UnhookWindowsHookEx(_keyHook);
                    _keyHook = IntPtr.Zero;
                }
            }
            catch
            {
            }
        }

        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && _recording && !_stopping)
                {
                    int msg = wParam.ToInt32();
                    if (msg == Native.WM_LBUTTONDOWN || msg == Native.WM_RBUTTONDOWN || msg == Native.WM_MBUTTONDOWN)
                    {
                        Native.MSLLHOOKSTRUCT info = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                        if ((info.flags & LLMHF_INJECTED) == 0)
                        {
                            RecordClick(msg, info.pt.X, info.pt.Y, info.time);
                        }
                    }
                    else if (msg == WM_MOUSEWHEEL)
                    {
                        Native.MSLLHOOKSTRUCT info = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                        if ((info.flags & LLMHF_INJECTED) == 0)
                        {
                            int delta = (short)((info.mouseData >> 16) & 0xFFFF);
                            RecordWheel(info.pt.X, info.pt.Y, delta, info.time);
                        }
                    }
                }
            }
            catch
            {
            }
            // 永远放行，绝不吞输入
            return Native.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        private IntPtr KeyHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && _recording && !_stopping)
                {
                    int msg = wParam.ToInt32();
                    Native.KBDLLHOOKSTRUCT info = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                    if ((info.flags & LLKHF_INJECTED) == 0)
                    {
                        int vk = (int)info.vkCode;
                        if (msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN)
                        {
                            if (!_keysDown.Contains(vk))
                            {
                                _keysDown.Add(vk);
                                RecordKeyDown(vk, info.time);
                            }
                        }
                        else if (msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP)
                        {
                            _keysDown.Remove(vk);
                        }
                    }
                }
            }
            catch
            {
            }
            return Native.CallNextHookEx(_keyHook, nCode, wParam, lParam);
        }

        private bool IsOwnWindowAt(int x, int y)
        {
            IntPtr h = Native.RootWindowAt(x, y);
            if (h == IntPtr.Zero)
            {
                return false;
            }
            int pid = AppEnv.PidOfWindow(h);
            return pid != 0 && pid == OwnPid;
        }

        private bool IsOwnForeground()
        {
            IntPtr h = Native.GetForegroundWindow();
            if (h == IntPtr.Zero)
            {
                return false;
            }
            int pid = AppEnv.PidOfWindow(h);
            return pid != 0 && pid == OwnPid;
        }

        private void RecordClick(int msg, int x, int y, uint tick)
        {
            if (IsOwnWindowAt(x, y))
            {
                return;
            }

            MouseBtn btn;
            if (msg == Native.WM_RBUTTONDOWN)
            {
                btn = MouseBtn.Right;
            }
            else if (msg == Native.WM_MBUTTONDOWN)
            {
                btn = MouseBtn.Middle;
            }
            else
            {
                btn = MouseBtn.Left;
            }

            // 同一位置 350ms 内的第二次点击 -> 合并为双击
            if (_lastWasClick && _lastDownButton == (uint)msg)
            {
                uint dt = tick - _lastTick;
                if (dt <= 350 && Math.Abs(x - _lastX) <= 3 && Math.Abs(y - _lastY) <= 3)
                {
                    lock (_sync)
                    {
                        if (_actions.Count > 0)
                        {
                            MacroAction last = _actions[_actions.Count - 1];
                            if (last.Kind == ActionKind.Click && !last.DoubleClick)
                            {
                                last.DoubleClick = true;
                                // 把时间基准移到第二次按下，下一次动作的等待从双击结束算起
                                _lastTick = tick;
                                return;
                            }
                        }
                    }
                }
            }

            MacroAction a = new MacroAction();
            a.Kind = ActionKind.Click;
            a.Button = btn;
            a.X = x;
            a.Y = y;
            a.Space = CoordSpace.Screen;
            a.WaitMs = 100;

            lock (_sync)
            {
                if (_actions.Count >= MaxActions)
                {
                    return;
                }
                AppendWaitInternal((int)Elapsed(tick));
                _actions.Add(a);
                _count = _actions.Count;
            }

            _lastTick = tick;
            _hasLast = true;
            _lastWasClick = true;
            _lastWasWheel = false;
            _lastDownButton = (uint)msg;
            _lastX = x;
            _lastY = y;
        }

        /// <summary>
        /// 滚轮：一格 = 120。触摸板增量很小，攒够一格再记；
        /// 同一位置 600ms 内的同向滚动合并成一条，免得列表被刷爆。
        /// </summary>
        private void RecordWheel(int x, int y, int rawDelta, uint tick)
        {
            if (rawDelta == 0 || IsOwnWindowAt(x, y))
            {
                return;
            }

            _wheelAccum += rawDelta;
            int notches = _wheelAccum / 120;
            if (notches == 0)
            {
                return;
            }
            _wheelAccum -= notches * 120;

            lock (_sync)
            {
                if (_lastWasWheel && _actions.Count > 0)
                {
                    MacroAction last = _actions[_actions.Count - 1];
                    if (last.Kind == ActionKind.Wheel
                        && Math.Sign(last.WheelDelta) == Math.Sign(notches)
                        && Math.Abs(last.X - x) <= 6
                        && Math.Abs(last.Y - y) <= 6
                        && (tick - _lastTick) <= 600)
                    {
                        last.WheelDelta += notches;
                        _lastTick = tick;
                        _lastX = x;
                        _lastY = y;
                        return;
                    }
                }

                if (_actions.Count >= MaxActions)
                {
                    return;
                }

                MacroAction a = new MacroAction();
                a.Kind = ActionKind.Wheel;
                a.X = x;
                a.Y = y;
                a.Space = CoordSpace.Screen;
                a.WheelDelta = notches;
                a.WaitMs = 120;

                AppendWaitInternal((int)Elapsed(tick));
                _actions.Add(a);
                _count = _actions.Count;
            }

            _lastTick = tick;
            _hasLast = true;
            _lastWasClick = false;
            _lastWasWheel = true;
            _lastX = x;
            _lastY = y;
        }

        private void RecordKeyDown(int vk, uint tick)
        {
            // 不录我们自己的热键
            if (vk == (int)Keys.F10 || vk == (int)Keys.F8 || vk == (int)Keys.F6)
            {
                return;
            }
            // 修饰键本身不单独成动作
            if (vk == 0x10 || vk == 0x11 || vk == 0x12 || vk == 0x5B || vk == 0x5C ||
                vk == 0xA0 || vk == 0xA1 || vk == 0xA2 || vk == 0xA3 || vk == 0xA4 || vk == 0xA5)
            {
                return;
            }
            if (IsOwnForeground())
            {
                return;
            }

            Keys mods = Keys.None;
            if (IsDown(0x11) || IsDown(0xA2) || IsDown(0xA3)) mods |= Keys.Control;
            if (IsDown(0x12) || IsDown(0xA4) || IsDown(0xA5)) mods |= Keys.Alt;
            if (IsDown(0x10) || IsDown(0xA0) || IsDown(0xA1)) mods |= Keys.Shift;

            MacroAction a = new MacroAction();
            a.Kind = ActionKind.Key;
            a.KeyCode = (Keys)vk;
            a.Modifiers = mods;
            a.WaitMs = 100;

            lock (_sync)
            {
                if (_actions.Count >= MaxActions)
                {
                    return;
                }
                AppendWaitInternal((int)Elapsed(tick));
                _actions.Add(a);
                _count = _actions.Count;
            }

            _lastTick = tick;
            _hasLast = true;
            _lastWasClick = false;
        }

        private static bool IsDown(int vk)
        {
            return (Native.GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        /// <summary>把“距离上一动作的间隔”写进上一动作的等待时间。</summary>
        private int Elapsed(uint tick)
        {
            if (!_hasLast)
            {
                return 120;
            }
            uint dt = tick - _lastTick;
            return Fmt.Clamp((int)dt, 0, 60000);
        }

        private void AppendWait(int ms)
        {
            lock (_sync)
            {
                AppendWaitInternal(ms);
                _lastWasClick = false;
            }
        }

        private void AppendWaitInternal(int ms)
        {
            if (_actions.Count == 0)
            {
                return;
            }
            MacroAction prev = _actions[_actions.Count - 1];
            // 双击合并时保留原有等待的语义：把间隔压到很短
            prev.WaitMs = Fmt.Clamp(ms, 0, 60000);
        }

        public List<MacroAction> TakeResult()
        {
            lock (_sync)
            {
                List<MacroAction> copy = new List<MacroAction>();
                for (int i = 0; i < _actions.Count; i++)
                {
                    copy.Add(_actions[i].Clone());
                }
                _actions.Clear();
                _count = 0;
                return copy;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

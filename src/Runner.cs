using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

namespace AutoClicker
{
    /// <summary>
    /// 动作执行器。所有坐标在执行时才解析成屏幕坐标，执行中的参数是开始时的深拷贝。
    /// 任何失败都会明确写进 LastError，不会静默卡住。
    /// </summary>
    public class Runner
    {
        private Thread _thread;
        private volatile bool _stopRequested;

        private volatile bool _running;
        private volatile int _actionsDone;
        private volatile int _loopsDone;
        private volatile int _currentAction = -1;
        private volatile string _lastError = string.Empty;
        private volatile string _phase = "就绪";
        private volatile bool _isTest;

        private MacroSettings _settings;
        private IntPtr _window;
        private string _windowTitle = string.Empty;
        private readonly Random _random = new Random();

        public bool IsRunning { get { return _running; } }
        public bool IsTest { get { return _isTest; } }
        public int ActionsDone { get { return _actionsDone; } }
        public int LoopsDone { get { return _loopsDone; } }
        public int CurrentAction { get { return _currentAction; } }
        public string LastError { get { return _lastError; } }
        public string Phase { get { return _phase; } }

        /// <summary>运行中每一步都想通知界面刷新时用（可选）。</summary>
        public event EventHandler StateChanged;

        /// <summary>运行结束（正常或出错）后触发，参数为错误文本（正常为 null）。</summary>
        public event EventHandler<RunnerFinishedEventArgs> Finished;

        private void RaiseState()
        {
            EventHandler h = StateChanged;
            if (h != null)
            {
                try
                {
                    h(this, EventArgs.Empty);
                }
                catch
                {
                }
            }
        }

        public bool Start(MacroSettings settings, IntPtr window, string windowTitle, bool isTest)
        {
            if (_running)
            {
                return false;
            }
            if (settings == null)
            {
                return false;
            }

            _settings = settings.Clone();
            _window = window;
            _windowTitle = windowTitle ?? string.Empty;
            _isTest = isTest;
            _stopRequested = false;
            _actionsDone = 0;
            _loopsDone = 0;
            _currentAction = -1;
            _lastError = string.Empty;
            _phase = isTest ? "测试中" : "运行中";

            _thread = new Thread(ThreadMain);
            _thread.IsBackground = true;
            _thread.Priority = ThreadPriority.AboveNormal;
            _thread.Name = "AutoClickerRunner";
            _running = true;
            _thread.Start();
            RaiseState();
            return true;
        }

        public void Stop()
        {
            _stopRequested = true;
            _phase = "停止中";
            RaiseState();
        }

        private void ThreadMain()
        {
            string error = null;
            try
            {
                Native.timeBeginPeriod(1);
                RunCore();
            }
            catch (Exception ex)
            {
                error = ex.Message;
                _lastError = error;
                AppLog.WriteError("执行出错：" + ex.Message);
            }
            finally
            {
                try
                {
                    InputEngine.ReleaseAll();
                }
                catch
                {
                }
                Native.timeEndPeriod(1);
                _running = false;
                _currentAction = -1;
                _phase = error != null ? "出错" : (_stopRequested ? "已停止" : "完成");
                RaiseState();
                AppLog.Flush();

                EventHandler<RunnerFinishedEventArgs> h = Finished;
                if (h != null)
                {
                    try
                    {
                        h(this, new RunnerFinishedEventArgs(error));
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void RunCore()
        {
            MacroSettings s = _settings;

            AppLog.BeginRun();
            AppLog.Write(string.Format(
                CultureInfo.InvariantCulture,
                "===== 开始{0}：动作 {1} 个，循环 {2}，启动延迟 {3}ms，点击方式 {4}，绑定窗口 {5} =====",
                _isTest ? "测试" : "执行",
                s.Actions.Count,
                s.LoopCount <= 0 ? "无限" : s.LoopCount.ToString(CultureInfo.InvariantCulture),
                _isTest ? 0 : s.StartDelayMs,
                s.InputMethod == MouseInputMethod.SendInput ? "SendInput" : "mouse_event",
                _window == IntPtr.Zero ? "无" : ("0x" + _window.ToInt64().ToString("X", CultureInfo.InvariantCulture) + " \"" + _windowTitle + "\"")));

            bool needWindow = s.HasWindowRelativeAction();
            if (needWindow)
            {
                if (_window == IntPtr.Zero)
                {
                    throw new InvalidOperationException("有动作使用「窗口相对」坐标，但还没有绑定窗口。请先点「绑定窗口」。");
                }
                if (!Native.IsWindow(_window))
                {
                    throw new InvalidOperationException("绑定的窗口已经关闭，请重新绑定窗口。");
                }
                if (Native.IsIconic(_window))
                {
                    Native.ShowWindow(_window, Native.SW_RESTORE);
                    Thread.Sleep(150);
                }
                if (!Native.IsWindow(_window))
                {
                    throw new InvalidOperationException("绑定的窗口不可用，请重新绑定窗口。");
                }
                AppLog.Write("绑定窗口：" + AppEnv.DescribeTarget(0, _window));
            }

            // 窗口模式：先把窗口叫到前台；屏幕模式：第一次点击时再按点激活。
            if (_window != IntPtr.Zero)
            {
                if (Native.GetForegroundWindow() != _window)
                {
                    bool ok = Native.ForceForeground(_window);
                    AppLog.Write("激活绑定窗口：" + (ok ? "成功" : "失败（仍继续，点击时会再试一次）"));
                    if (ok)
                    {
                        Thread.Sleep(80);
                    }
                }
            }

            int delay = _isTest ? 0 : s.StartDelayMs;
            if (delay > 0)
            {
                _phase = "启动延迟 " + Fmt.Seconds(delay);
                SleepCancelable(delay);
                if (_stopRequested)
                {
                    return;
                }
            }

            _phase = "运行中";
            int loops = 0;
            int loopLimit = _isTest ? 1 : s.LoopCount;

            while (!_stopRequested)
            {
                if (loopLimit > 0 && loops >= loopLimit)
                {
                    break;
                }

                for (int i = 0; i < s.Actions.Count && !_stopRequested; i++)
                {
                    _currentAction = i;
                    MacroAction action = s.Actions[i];
                    if (!action.Enabled)
                    {
                        continue;
                    }

                    if (action.Kind == ActionKind.Click)
                    {
                        DoClick(s, action);
                    }
                    else if (action.Kind == ActionKind.Wheel)
                    {
                        DoWheel(s, action);
                    }
                    else if (action.Kind == ActionKind.Key)
                    {
                        if (_window != IntPtr.Zero && Native.GetForegroundWindow() != _window)
                        {
                            Native.ForceForeground(_window);
                            Thread.Sleep(40);
                        }
                        string detail;
                        if (!InputEngine.SendKey(action.KeyCode, action.Modifiers, 25, out detail))
                        {
                            throw new InvalidOperationException("按键发送失败：" + detail);
                        }
                        AppLog.Write("KEY " + detail);
                    }

                    _actionsDone++;
                    RaiseState();

                    int wait = action.WaitMs;
                    if (!_isTest && s.TimeJitter > 0 && wait > 0)
                    {
                        wait += _random.Next(-s.TimeJitter, s.TimeJitter + 1);
                    }
                    if (wait > 0)
                    {
                        SleepCancelable(wait);
                    }
                }

                loops++;
                _loopsDone = loops;

                if (_stopRequested)
                {
                    break;
                }
                if (loopLimit > 0 && loops >= loopLimit)
                {
                    break;
                }
            }

            _phase = _stopRequested ? "已停止" : "完成";
        }

        /// <summary>
        /// 点击和滚轮共用的前半段：解析坐标 → 抖动 → 边界检查 → 激活窗口 → 移动 → 校验。
        /// 返回 false 表示执行中途被停了。
        /// </summary>
        private bool PrepareTarget(MacroSettings s, MacroAction action, string label, out int x, out int y)
        {
            ResolvePoint(s, action, out x, out y);

            if (!_isTest && s.PositionJitter > 0)
            {
                x += _random.Next(-s.PositionJitter, s.PositionJitter + 1);
                y += _random.Next(-s.PositionJitter, s.PositionJitter + 1);
            }

            if (!Native.IsPointOnScreen(x, y))
            {
                Native.RECT vs = Native.GetVirtualScreen();
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    "动作 #{0} 解析出的坐标 ({1},{2}) 在屏幕范围之外（虚拟屏幕 [{3},{4}]-[{5},{6}]）。{7}",
                    _currentAction + 1,
                    x,
                    y,
                    vs.Left,
                    vs.Top,
                    vs.Right,
                    vs.Bottom,
                    action.Space == CoordSpace.WindowClient ? "请检查绑定窗口的位置/DPI，或重新绑定。" : "请重新捕捉坐标。"));
            }

            // 让事件落到正确的窗口上：如果目标窗口不是前台，先激活它。
            if (s.ActivateBeforeClick)
            {
                IntPtr target = _window != IntPtr.Zero ? _window : Native.RootWindowAt(x, y);
                if (target != IntPtr.Zero && Native.GetForegroundWindow() != target && !IsDesktopWindow(target))
                {
                    bool ok = Native.ForceForeground(target);
                    AppLog.Write(string.Format(
                        CultureInfo.InvariantCulture,
                        "激活 {0}：{1}",
                        Native.GetWindowTextSafe(target),
                        ok ? "成功" : "失败"));
                    if (ok)
                    {
                        Thread.Sleep(60);
                    }
                }
            }

            string detail;
            bool moved = InputEngine.MoveTo(x, y, out detail);
            AppLog.Write("MOVE " + detail);
            if (!moved)
            {
                throw new InvalidOperationException(
                    "鼠标移动到 (" + x + "," + y + ") 失败：" + detail);
            }

            Thread.Sleep(18);
            if (_stopRequested)
            {
                return false;
            }

            IntPtr under = Native.RootWindowAt(x, y);
            AppLog.Write(string.Format(
                CultureInfo.InvariantCulture,
                "{0} 目标=({1},{2}) 该点窗口=0x{3:X} \"{4}\" 前台=0x{5:X}",
                label,
                x,
                y,
                under.ToInt64(),
                Native.GetWindowTextSafe(under),
                Native.GetForegroundWindow().ToInt64()));
            return true;
        }

        private void DoClick(MacroSettings s, MacroAction action)
        {
            int x;
            int y;
            if (!PrepareTarget(s, action, "CLICK", out x, out y))
            {
                return;
            }

            string detail;
            if (!InputEngine.Click(action.Button, s.ClickHoldMs, s.InputMethod, out detail))
            {
                throw new InvalidOperationException("点击发送失败：" + detail);
            }
            AppLog.Write("CLICK " + detail);

            if (action.DoubleClick)
            {
                Thread.Sleep(Math.Max(30, s.DoubleClickGapMs));
                if (_stopRequested)
                {
                    return;
                }
                if (!InputEngine.Click(action.Button, s.ClickHoldMs, s.InputMethod, out detail))
                {
                    throw new InvalidOperationException("第二次点击发送失败：" + detail);
                }
                AppLog.Write("CLICK(2) " + detail);
            }

            Thread.Sleep(12);
        }

        private void DoWheel(MacroSettings s, MacroAction action)
        {
            int x;
            int y;
            if (!PrepareTarget(s, action, "WHEEL", out x, out y))
            {
                return;
            }

            string detail;
            if (!InputEngine.SendWheel(action.WheelDelta, out detail))
            {
                throw new InvalidOperationException("滚轮发送失败：" + detail);
            }
            AppLog.Write("WHEEL " + detail);

            // 有些程序滚完还要一点时间处理，给一帧缓冲
            Thread.Sleep(25);
        }

        private static bool IsDesktopWindow(IntPtr hwnd)
        {
            string cls = Native.GetClassNameSafe(hwnd);
            return string.Equals(cls, "Progman", StringComparison.OrdinalIgnoreCase)
                || string.Equals(cls, "WorkerW", StringComparison.OrdinalIgnoreCase)
                || string.Equals(cls, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>把动作坐标解析成屏幕坐标。窗口相对只在这里做 ClientToScreen。</summary>
        private void ResolvePoint(MacroSettings s, MacroAction action, out int x, out int y)
        {
            if (action.Space == CoordSpace.Screen)
            {
                x = action.X;
                y = action.Y;
                return;
            }

            IntPtr hwnd = _window;
            if (hwnd == IntPtr.Zero)
            {
                throw new InvalidOperationException("动作使用「窗口相对」坐标，但没有绑定窗口。请先点「绑定窗口」。");
            }
            if (!Native.IsWindow(hwnd))
            {
                throw new InvalidOperationException("绑定的窗口已关闭，请重新绑定窗口。");
            }
            if (Native.IsIconic(hwnd))
            {
                throw new InvalidOperationException("绑定的窗口已最小化，无法计算坐标。请把它还原。");
            }

            Native.POINT p = new Native.POINT(action.X, action.Y);
            if (!Native.ClientToScreen(hwnd, ref p))
            {
                int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                throw new InvalidOperationException("ClientToScreen 失败，Win32Error=" + err + "。请重新绑定窗口。");
            }

            x = p.X;
            y = p.Y;
            AppLog.Write(string.Format(
                CultureInfo.InvariantCulture,
                "RESOLVE 窗口相对 ({0},{1}) -> 屏幕 ({2},{3}) hwnd=0x{4:X}",
                action.X,
                action.Y,
                x,
                y,
                hwnd.ToInt64()));
        }

        private void SleepCancelable(int ms)
        {
            int remain = ms;
            while (remain > 0 && !_stopRequested)
            {
                int chunk = remain > 10 ? 10 : remain;
                Thread.Sleep(chunk);
                remain -= chunk;
            }
        }
    }

    public class RunnerFinishedEventArgs : EventArgs
    {
        public readonly string Error;

        public RunnerFinishedEventArgs(string error)
        {
            Error = error;
        }
    }
}

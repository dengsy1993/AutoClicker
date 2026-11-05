using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace AutoClicker
{
    /// <summary>坐标所属的坐标空间。每个点击动作自己记住，绝不靠全局模式推断。</summary>
    public enum CoordSpace
    {
        Screen = 0,
        WindowClient = 1
    }

    public enum ActionKind
    {
        Click = 0,
        Key = 1,
        Wait = 2,
        Wheel = 3
    }

    public enum MouseBtn
    {
        Left = 0,
        Right = 1,
        Middle = 2
    }

    /// <summary>鼠标输入方式。</summary>
    public enum MouseInputMethod
    {
        SendInput = 0,
        MouseEvent = 1
    }

    public class MacroAction
    {
        public ActionKind Kind = ActionKind.Click;
        public bool Enabled = true;
        public int WaitMs = 100;

        // 点击
        public int X;
        public int Y;
        public CoordSpace Space = CoordSpace.Screen;
        public MouseBtn Button = MouseBtn.Left;
        public bool DoubleClick;

        // 按键
        public Keys KeyCode = Keys.Space;
        public Keys Modifiers = Keys.None;

        // 滚轮：正数向上、负数向下，单位是"格"（一格 = 系统里的 120）
        public int WheelDelta = 3;

        public MacroAction Clone()
        {
            MacroAction c = new MacroAction();
            c.Kind = Kind;
            c.Enabled = Enabled;
            c.WaitMs = WaitMs;
            c.X = X;
            c.Y = Y;
            c.Space = Space;
            c.Button = Button;
            c.DoubleClick = DoubleClick;
            c.KeyCode = KeyCode;
            c.Modifiers = Modifiers;
            c.WheelDelta = WheelDelta;
            return c;
        }

        public static string ButtonName(MouseBtn b)
        {
            switch (b)
            {
                case MouseBtn.Right: return "右键";
                case MouseBtn.Middle: return "中键";
                default: return "左键";
            }
        }

        public static string SpaceName(CoordSpace s)
        {
            return s == CoordSpace.WindowClient ? "窗口" : "屏幕";
        }

        public string Describe()
        {
            switch (Kind)
            {
                case ActionKind.Click:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}{1} ({2}, {3})",
                        ButtonName(Button),
                        DoubleClick ? "双击" : "单击",
                        X,
                        Y);
                case ActionKind.Key:
                    return "按键 " + HotkeyText.Describe(KeyCode, Modifiers);
                case ActionKind.Wheel:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "滚轮 向{0} {1} 格 ({2}, {3})",
                        WheelDelta >= 0 ? "上" : "下",
                        Math.Abs(WheelDelta),
                        X,
                        Y);
                default:
                    return "等待 " + WaitMs + " ms";
            }
        }

        /// <summary>列表里显示的文本（带坐标空间提示）。</summary>
        public string DescribeForList()
        {
            string s = Describe();
            if ((Kind == ActionKind.Click || Kind == ActionKind.Wheel) && Space == CoordSpace.WindowClient)
            {
                s += " · 窗口相对";
            }
            return s;
        }

        public string ToLine()
        {
            switch (Kind)
            {
                case ActionKind.Click:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "C|{0}|{1}|{2}|{3}|{4}|{5}|{6}",
                        (int)Button,
                        DoubleClick ? 1 : 0,
                        X,
                        Y,
                        (int)Space,
                        WaitMs,
                        Enabled ? 1 : 0);
                case ActionKind.Key:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "K|{0}|{1}|{2}|{3}",
                        (int)KeyCode,
                        (int)Modifiers,
                        WaitMs,
                        Enabled ? 1 : 0);
                case ActionKind.Wheel:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "R|{0}|{1}|{2}|{3}|{4}|{5}",
                        WheelDelta,
                        X,
                        Y,
                        (int)Space,
                        WaitMs,
                        Enabled ? 1 : 0);
                default:
                    return string.Format(CultureInfo.InvariantCulture, "W|{0}|{1}", WaitMs, Enabled ? 1 : 0);
            }
        }

        public static MacroAction ParseLine(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return null;
            }
            string[] p = line.Split('|');
            try
            {
                if (p.Length >= 3 && p[0] == "W")
                {
                    MacroAction a = new MacroAction();
                    a.Kind = ActionKind.Wait;
                    a.WaitMs = ParseInt(p[1], 0);
                    a.Enabled = p.Length > 2 ? ParseInt(p[2], 1) != 0 : true;
                    return a;
                }
                if (p.Length >= 5 && p[0] == "K")
                {
                    MacroAction a = new MacroAction();
                    a.Kind = ActionKind.Key;
                    a.KeyCode = (Keys)ParseInt(p[1], (int)Keys.Space);
                    a.Modifiers = (Keys)ParseInt(p[2], 0);
                    a.WaitMs = ParseInt(p[3], 100);
                    a.Enabled = p.Length > 4 ? ParseInt(p[4], 1) != 0 : true;
                    if (a.KeyCode == Keys.None)
                    {
                        a.KeyCode = Keys.Space;
                    }
                    return a;
                }
                if (p.Length >= 7 && p[0] == "R")
                {
                    MacroAction a = new MacroAction();
                    a.Kind = ActionKind.Wheel;
                    a.WheelDelta = ParseInt(p[1], 3);
                    a.X = ParseInt(p[2], 0);
                    a.Y = ParseInt(p[3], 0);
                    a.Space = (CoordSpace)ParseInt(p[4], 0);
                    a.WaitMs = ParseInt(p[5], 100);
                    a.Enabled = ParseInt(p[6], 1) != 0;
                    return a;
                }
                if (p.Length >= 6 && p[0] == "C")
                {
                    MacroAction a = new MacroAction();
                    a.Kind = ActionKind.Click;
                    a.Button = (MouseBtn)ParseInt(p[1], 0);
                    a.DoubleClick = ParseInt(p[2], 0) != 0;
                    a.X = ParseInt(p[3], 0);
                    a.Y = ParseInt(p[4], 0);

                    if (p.Length >= 8)
                    {
                        // v3: C|button|double|x|y|space|wait|enabled
                        a.Space = (CoordSpace)ParseInt(p[5], 0);
                        a.WaitMs = ParseInt(p[6], 100);
                        a.Enabled = ParseInt(p[7], 1) != 0;
                    }
                    else
                    {
                        // v2 兼容: C|button|double|x|y|wait|enabled
                        a.Space = CoordSpace.Screen;
                        a.WaitMs = ParseInt(p[5], 100);
                        a.Enabled = p.Length > 6 ? ParseInt(p[6], 1) != 0 : true;
                    }
                    return a;
                }
            }
            catch
            {
                return null;
            }
            return null;
        }

        private static int ParseInt(string s, int fallback)
        {
            int v;
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
            {
                return v;
            }
            return fallback;
        }
    }

    /// <summary>一次运行用到的全部参数（运行时使用深拷贝，避免执行中被 UI 改坏）。</summary>
    public class MacroSettings
    {
        public List<MacroAction> Actions = new List<MacroAction>();
        public int LoopCount = 0;          // 0 = 无限
        public int StartDelayMs = 800;
        public int PositionJitter;
        public int TimeJitter;
        public int ClickHoldMs = 40;
        public int DoubleClickGapMs = 90;
        public bool ActivateBeforeClick = true;
        public MouseInputMethod InputMethod = MouseInputMethod.SendInput;

        public MacroSettings Clone()
        {
            MacroSettings c = new MacroSettings();
            c.LoopCount = LoopCount;
            c.StartDelayMs = StartDelayMs;
            c.PositionJitter = PositionJitter;
            c.TimeJitter = TimeJitter;
            c.ClickHoldMs = ClickHoldMs;
            c.DoubleClickGapMs = DoubleClickGapMs;
            c.ActivateBeforeClick = ActivateBeforeClick;
            c.InputMethod = InputMethod;
            for (int i = 0; i < Actions.Count; i++)
            {
                c.Actions.Add(Actions[i].Clone());
            }
            return c;
        }

        public int EnabledInputCount()
        {
            int n = 0;
            for (int i = 0; i < Actions.Count; i++)
            {
                if (Actions[i].Enabled && Actions[i].Kind != ActionKind.Wait)
                {
                    n++;
                }
            }
            return n;
        }

        public bool HasWindowRelativeAction()
        {
            for (int i = 0; i < Actions.Count; i++)
            {
                MacroAction a = Actions[i];
                if (a.Enabled && (a.Kind == ActionKind.Click || a.Kind == ActionKind.Wheel) && a.Space == CoordSpace.WindowClient)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 所有本地文件位置。
    /// 默认放在 exe 旁边的「数据」文件夹里（便携：整个文件夹拷走就能用）；
    /// 如果 exe 所在目录不可写（比如放在 Program Files），才退回 %APPDATA%。
    /// </summary>
    public static class AppPaths
    {
        /// <summary>exe 所在目录。</summary>
        public static readonly string ExeDir;
        /// <summary>实际使用的数据目录。</summary>
        public static readonly string DataDir;
        /// <summary>数据目录是否在 exe 旁边。</summary>
        public static readonly bool IsPortable;

        public static readonly string SettingsFile;
        public static readonly string SequencesFile;
        public static readonly string RunLogFile;
        public static readonly string CrashLogFile;

        private const string PortableFolderName = "数据";
        private const string LegacyFolderName = "AutoClicker3";

        static AppPaths()
        {
            try
            {
                ExeDir = Path.GetDirectoryName(Application.ExecutablePath);
            }
            catch
            {
                ExeDir = null;
            }
            if (string.IsNullOrEmpty(ExeDir))
            {
                ExeDir = AppDomain.CurrentDomain.BaseDirectory;
            }

            string portable = Path.Combine(ExeDir, PortableFolderName);
            IsPortable = TryPrepare(portable);
            DataDir = IsPortable
                ? portable
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), LegacyFolderName);

            SettingsFile = Path.Combine(DataDir, "settings.ini");
            SequencesFile = Path.Combine(DataDir, "sequences.dat");
            RunLogFile = Path.Combine(DataDir, "run.log");
            CrashLogFile = Path.Combine(DataDir, "crash.log");

            try
            {
                Directory.CreateDirectory(DataDir);
            }
            catch
            {
            }

            MigrateLegacyData();
        }

        private static bool TryPrepare(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".write-test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>老版本把配置存在 %APPDATA%\AutoClicker3，第一次改用便携目录时搬过来。</summary>
        private static void MigrateLegacyData()
        {
            if (!IsPortable)
            {
                return;
            }
            try
            {
                string legacy = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    LegacyFolderName);
                if (!Directory.Exists(legacy))
                {
                    return;
                }
                string[] names = new string[] { "settings.ini", "sequences.dat" };
                for (int i = 0; i < names.Length; i++)
                {
                    string from = Path.Combine(legacy, names[i]);
                    string to = Path.Combine(DataDir, names[i]);
                    if (File.Exists(from) && !File.Exists(to))
                    {
                        File.Copy(from, to);
                    }
                }
            }
            catch
            {
            }
        }

        public static void EnsureDir()
        {
            try
            {
                if (!Directory.Exists(DataDir))
                {
                    Directory.CreateDirectory(DataDir);
                }
            }
            catch
            {
            }
        }

        public static string Combine(string name)
        {
            EnsureDir();
            return Path.Combine(DataDir, name);
        }
    }

    /// <summary>运行日志：内存里保留一份给界面显示，同时落盘一个 run.log 方便排查。</summary>
    public static class AppLog
    {
        private static readonly object Sync = new object();
        private static readonly List<string> Lines = new List<string>();
        private static StreamWriter _writer;
        private const int MaxMemoryLines = 500;

        public static event EventHandler Changed;

        public static void StartSession()
        {
            lock (Sync)
            {
                try
                {
                    if (_writer != null)
                    {
                        _writer.Dispose();
                        _writer = null;
                    }
                    AppPaths.EnsureDir();
                    _writer = new StreamWriter(AppPaths.RunLogFile, false, new UTF8Encoding(false));
                    _writer.WriteLine("# AutoClicker v3 运行日志  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                    _writer.WriteLine("# OS=" + Environment.OSVersion.VersionString + " x64=" + Environment.Is64BitProcess + " admin=" + AppEnv.IsSelfElevated);
                    _writer.WriteLine("# exe=" + Application.ExecutablePath);
                    _writer.Flush();
                }
                catch
                {
                    _writer = null;
                }
            }
        }

        public static void BeginRun()
        {
            Write("----------------------------------------");
        }

        public static void Write(string text)
        {
            Write(text, false);
        }

        public static void WriteError(string text)
        {
            Write(text, true);
        }

        private static void Write(string text, bool isError)
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + text;
            lock (Sync)
            {
                Lines.Add(line);
                if (Lines.Count > MaxMemoryLines)
                {
                    Lines.RemoveRange(0, Lines.Count - MaxMemoryLines);
                }
                try
                {
                    if (_writer != null)
                    {
                        _writer.WriteLine(line);
                        _writer.Flush();
                    }
                }
                catch
                {
                }
            }
            EventHandler h = Changed;
            if (h != null)
            {
                try
                {
                    h(null, EventArgs.Empty);
                }
                catch
                {
                }
            }
        }

        public static string[] Snapshot()
        {
            lock (Sync)
            {
                return Lines.ToArray();
            }
        }

        public static string Tail(int count)
        {
            lock (Sync)
            {
                int start = Math.Max(0, Lines.Count - count);
                StringBuilder sb = new StringBuilder();
                for (int i = start; i < Lines.Count; i++)
                {
                    sb.AppendLine(Lines[i]);
                }
                return sb.ToString();
            }
        }

        public static void Flush()
        {
            lock (Sync)
            {
                try
                {
                    if (_writer != null)
                    {
                        _writer.Flush();
                    }
                }
                catch
                {
                }
            }
        }

        public static void Shutdown()
        {
            lock (Sync)
            {
                try
                {
                    if (_writer != null)
                    {
                        _writer.Flush();
                        _writer.Dispose();
                        _writer = null;
                    }
                }
                catch
                {
                }
            }
        }
    }

    /// <summary>进程/系统环境探测。</summary>
    public static class AppEnv
    {
        public static bool IsSelfElevated
        {
            get
            {
                try
                {
                    System.Security.Principal.WindowsIdentity id = System.Security.Principal.WindowsIdentity.GetCurrent();
                    System.Security.Principal.WindowsPrincipal p = new System.Security.Principal.WindowsPrincipal(id);
                    return p.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>目标进程是否更高权限（提权）。null = 无法判断。</summary>
        public static bool? IsProcessElevated(int pid)
        {
            if (pid <= 0)
            {
                return null;
            }
            IntPtr h = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero)
            {
                return null;
            }
            try
            {
                IntPtr token;
                if (!Native.OpenProcessToken(h, Native.TOKEN_QUERY, out token))
                {
                    // 打不开令牌，基本可以确定对方完整性级别更高
                    int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    return err == 5 ? (bool?)true : null;
                }
                try
                {
                    Native.TOKEN_ELEVATION elev;
                    int ret;
                    if (!Native.GetTokenInformation(token, Native.TokenElevation, out elev, 4, out ret))
                    {
                        return null;
                    }
                    return elev.TokenIsElevated != 0;
                }
                finally
                {
                    Native.CloseHandle(token);
                }
            }
            finally
            {
                Native.CloseHandle(h);
            }
        }

        public static string ProcessNameOf(int pid)
        {
            IntPtr h = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero)
            {
                return string.Empty;
            }
            try
            {
                StringBuilder sb = new StringBuilder(600);
                uint size = (uint)sb.Capacity;
                if (Native.QueryFullProcessImageName(h, 0, sb, ref size))
                {
                    return Path.GetFileName(sb.ToString());
                }
            }
            catch
            {
            }
            finally
            {
                Native.CloseHandle(h);
            }
            return string.Empty;
        }

        public static int PidOfWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return 0;
            }
            uint pid;
            Native.GetWindowThreadProcessId(hwnd, out pid);
            return (int)pid;
        }

        /// <summary>把窗口置前的能力检查 + 说明。</summary>
        public static string DescribeTarget(int hwndPid, IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return "无";
            }
            string title = Native.GetWindowTextSafe(hwnd);
            string cls = Native.GetClassNameSafe(hwnd);
            int pid = PidOfWindow(hwnd);
            string exe = ProcessNameOf(pid);
            return string.Format(
                CultureInfo.InvariantCulture,
                "hwnd=0x{0:X} pid={1} exe={2} class={3} title=\"{4}\"",
                hwnd.ToInt64(),
                pid,
                exe,
                cls,
                title);
        }
    }

    /// <summary>settings.ini 读写（简单的 key=value，容忍手工编辑）。</summary>
    public static class SettingsStore
    {
        private const int CurrentVersion = 3;

        public static void Save(string path, MacroSettings s, bool topMost, bool darkMode, CoordSpace uiSpace, int windowX, int windowY)
        {
            AppPaths.EnsureDir();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("; 鼠标连点器 v3 设置，可手工编辑");
            sb.AppendLine("[General]");
            sb.AppendLine("Version=" + CurrentVersion);
            sb.AppendLine("LoopCount=" + s.LoopCount);
            sb.AppendLine("StartDelayMs=" + s.StartDelayMs);
            sb.AppendLine("PositionJitter=" + s.PositionJitter);
            sb.AppendLine("TimeJitter=" + s.TimeJitter);
            sb.AppendLine("ClickHoldMs=" + s.ClickHoldMs);
            sb.AppendLine("DoubleClickGapMs=" + s.DoubleClickGapMs);
            sb.AppendLine("ActivateBeforeClick=" + (s.ActivateBeforeClick ? 1 : 0));
            sb.AppendLine("InputMethod=" + (int)s.InputMethod);
            sb.AppendLine("UiSpace=" + (int)uiSpace);
            sb.AppendLine("TopMost=" + (topMost ? 1 : 0));
            sb.AppendLine("WindowX=" + windowX);
            sb.AppendLine("WindowY=" + windowY);
            sb.AppendLine("DarkMode=" + (darkMode ? 1 : 0));
            sb.AppendLine("ActionCount=" + s.Actions.Count);
            for (int i = 0; i < s.Actions.Count; i++)
            {
                sb.AppendLine("Action" + i + "=" + s.Actions[i].ToLine());
            }
            WriteAtomic(path, sb.ToString());
        }

        public static bool Load(string path, out MacroSettings settings, out bool topMost, out bool darkMode, out CoordSpace uiSpace, out int windowX, out int windowY)
        {
            settings = new MacroSettings();
            topMost = false;
            darkMode = true;
            uiSpace = CoordSpace.Screen;
            windowX = -1;
            windowY = -1;

            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                List<MacroAction> parsed = new List<MacroAction>();
                int actionCount = -1;

                for (int i = 0; i < lines.Length; i++)
                {
                    string raw = lines[i];
                    if (raw == null)
                    {
                        continue;
                    }
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(";", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("["))
                    {
                        continue;
                    }
                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();

                    if (key.Equals("LoopCount", StringComparison.OrdinalIgnoreCase)) settings.LoopCount = ParseInt(val, settings.LoopCount);
                    else if (key.Equals("StartDelayMs", StringComparison.OrdinalIgnoreCase)) settings.StartDelayMs = ParseInt(val, settings.StartDelayMs);
                    else if (key.Equals("DelaySeconds", StringComparison.OrdinalIgnoreCase)) settings.StartDelayMs = (int)Math.Round(ParseDouble(val, 1.0) * 1000.0);
                    else if (key.Equals("PositionJitter", StringComparison.OrdinalIgnoreCase)) settings.PositionJitter = ParseInt(val, 0);
                    else if (key.Equals("TimeJitter", StringComparison.OrdinalIgnoreCase)) settings.TimeJitter = ParseInt(val, 0);
                    else if (key.Equals("ClickHoldMs", StringComparison.OrdinalIgnoreCase)) settings.ClickHoldMs = ParseInt(val, settings.ClickHoldMs);
                    else if (key.Equals("DoubleClickGapMs", StringComparison.OrdinalIgnoreCase)) settings.DoubleClickGapMs = ParseInt(val, settings.DoubleClickGapMs);
                    else if (key.Equals("ActivateBeforeClick", StringComparison.OrdinalIgnoreCase)) settings.ActivateBeforeClick = ParseInt(val, 1) != 0;
                    else if (key.Equals("InputMethod", StringComparison.OrdinalIgnoreCase)) settings.InputMethod = (MouseInputMethod)Math.Max(0, Math.Min(1, ParseInt(val, 0)));
                    else if (key.Equals("UiSpace", StringComparison.OrdinalIgnoreCase)) uiSpace = ParseInt(val, 0) == 1 ? CoordSpace.WindowClient : CoordSpace.Screen;
                    else if (key.Equals("Mode", StringComparison.OrdinalIgnoreCase)) uiSpace = ParseInt(val, 0) == 1 ? CoordSpace.WindowClient : CoordSpace.Screen;
                    else if (key.Equals("TopMost", StringComparison.OrdinalIgnoreCase)) topMost = ParseInt(val, 0) != 0;
                    else if (key.Equals("WindowX", StringComparison.OrdinalIgnoreCase)) windowX = ParseInt(val, -1);
                    else if (key.Equals("WindowY", StringComparison.OrdinalIgnoreCase)) windowY = ParseInt(val, -1);
                    else if (key.Equals("DarkMode", StringComparison.OrdinalIgnoreCase)) darkMode = ParseInt(val, 1) != 0;
                    else if (key.Equals("ActionCount", StringComparison.OrdinalIgnoreCase)) actionCount = ParseInt(val, -1);
                    else if (key.StartsWith("Action", StringComparison.OrdinalIgnoreCase))
                    {
                        MacroAction a = MacroAction.ParseLine(val);
                        if (a != null)
                        {
                            parsed.Add(a);
                        }
                    }
                }

                if (actionCount >= 0 && parsed.Count > actionCount)
                {
                    parsed.RemoveRange(actionCount, parsed.Count - actionCount);
                }
                settings.Actions = parsed;
                return true;
            }
            catch (Exception ex)
            {
                AppLog.WriteError("读取设置失败：" + ex.Message);
                return false;
            }
        }

        public static void WriteAtomic(string path, string content)
        {
            try
            {
                AppPaths.EnsureDir();
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, content, new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                AppLog.WriteError("写入失败 " + path + " : " + ex.Message);
            }
        }

        private static int ParseInt(string s, int fallback)
        {
            int v;
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
            {
                return v;
            }
            return fallback;
        }

        private static double ParseDouble(string s, double fallback)
        {
            double v;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
            {
                return v;
            }
            return fallback;
        }
    }

    // ------------------------------------------------------------------
    // 历史序列
    // ------------------------------------------------------------------

    public class SavedSequence
    {
        public string Name = string.Empty;
        public DateTime SavedAt = DateTime.Now;
        public List<MacroAction> Actions = new List<MacroAction>();

        public SavedSequence Clone()
        {
            SavedSequence c = new SavedSequence();
            c.Name = Name;
            c.SavedAt = SavedAt;
            for (int i = 0; i < Actions.Count; i++)
            {
                c.Actions.Add(Actions[i].Clone());
            }
            return c;
        }
    }

    public static class SequenceStore
    {
        private const string Header = "# AutoClicker sequence library v3";

        public static List<SavedSequence> Load(string path)
        {
            List<SavedSequence> result = new List<SavedSequence>();
            if (!File.Exists(path))
            {
                return result;
            }
            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                SavedSequence current = null;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line == null)
                    {
                        continue;
                    }
                    line = line.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (line.StartsWith("SEQ|", StringComparison.Ordinal))
                    {
                        string[] p = line.Split('|');
                        if (p.Length >= 4)
                        {
                            current = new SavedSequence();
                            current.Name = DecodeName(p[1]);
                            long ticks;
                            if (long.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks) && ticks > 0)
                            {
                                try
                                {
                                    current.SavedAt = new DateTime(ticks);
                                }
                                catch
                                {
                                    current.SavedAt = DateTime.Now;
                                }
                            }
                            result.Add(current);
                        }
                        continue;
                    }
                    if (current != null)
                    {
                        MacroAction a = MacroAction.ParseLine(line);
                        if (a != null)
                        {
                            current.Actions.Add(a);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.WriteError("读取历史序列失败：" + ex.Message);
            }
            for (int i = result.Count - 1; i >= 0; i--)
            {
                if (result[i].Actions.Count == 0)
                {
                    result.RemoveAt(i);
                }
            }
            return result;
        }

        public static void Save(string path, List<SavedSequence> list)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Header);
            for (int i = 0; i < list.Count; i++)
            {
                SavedSequence s = list[i];
                sb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "SEQ|{0}|{1}|{2}",
                    EncodeName(s.Name),
                    s.SavedAt.Ticks,
                    s.Actions.Count));
                for (int j = 0; j < s.Actions.Count; j++)
                {
                    sb.AppendLine(s.Actions[j].ToLine());
                }
            }
            SettingsStore.WriteAtomic(path, sb.ToString());
        }

        private static string EncodeName(string name)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(name ?? string.Empty));
        }

        private static string DecodeName(string b64)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(b64));
            }
            catch
            {
                return "未命名";
            }
        }
    }
}

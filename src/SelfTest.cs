using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace AutoClicker
{
    /// <summary>
    /// 自测：用两个真实窗口验证「移动 + 点击 + 按键 + 窗口相对坐标 + 激活」整条链路。
    /// 用 AutoClicker.exe --selftest 触发，结果写 selftest.log，退出码 0 = 通过。
    /// </summary>
    public static class SelfTest
    {
        private static readonly StringBuilder Report = new StringBuilder();
        private static bool _pass = true;

        public static int Run()
        {
            ProbeForm probe = null;
            Form other = null;
            try
            {
                Line("AutoClicker 自测 v" + Program.VersionText + "  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                Line("系统：" + Environment.OSVersion.VersionString + "；64 位进程：" + (Environment.Is64BitProcess ? "是" : "否"));
                Line("管理员权限：" + (AppEnv.IsSelfElevated ? "是" : "否"));

                Native.RECT vs = Native.GetVirtualScreen();
                Line(string.Format(CultureInfo.InvariantCulture, "虚拟屏幕：{0}×{1}，原点 ({2},{3})", vs.Width, vs.Height, vs.Left, vs.Top));

                Native.POINT original;
                Native.GetCursorPos(out original);

                probe = new ProbeForm();
                probe.StartPosition = FormStartPosition.Manual;
                probe.Location = new System.Drawing.Point(140, 140);
                probe.Show();
                Pump(350);

                other = new Form();
                other.Text = "selftest-focus";
                other.StartPosition = FormStartPosition.Manual;
                other.Location = new System.Drawing.Point(760, 140);
                other.ClientSize = new System.Drawing.Size(240, 200);
                other.Show();
                Pump(200);

                probe.ResetCounters();
                Line("");
                Line("窗口：探针 hwnd=0x" + probe.Handle.ToInt64().ToString("X", CultureInfo.InvariantCulture)
                    + "，客户区原点 " + probe.PointToScreen(System.Drawing.Point.Empty).ToString()
                    + "，尺寸 " + probe.ClientSize.Width + "×" + probe.ClientSize.Height);

                // --- 1. 单纯移动 ---
                System.Drawing.Point target = probe.PointToScreen(new System.Drawing.Point(70, 95));
                Line("");
                Line("--- 1) 移动测试 ---");
                Line("目标屏幕坐标 (" + target.X + ", " + target.Y + ")（探针客户区 70,95）");
                string detail;
                bool moved = InputEngine.MoveTo(target.X, target.Y, out detail);
                Line("MoveTo 返回：" + moved + "；" + detail);
                Native.POINT now;
                Native.GetCursorPos(out now);
                Line("GetCursorPos：" + now.X + ", " + now.Y);
                if (!moved)
                {
                    Line("!! 移动失败");
                    _pass = false;
                }

                // --- 2. 点击 / 按键 / 窗口坐标（探针不在前台）---
                Line("");
                Line("--- 2) 点击 + 按键 + 窗口相对坐标（目标窗口故意不在前台）---");
                Native.ForceForeground(other.Handle);
                Pump(200);
                IntPtr fg = Native.GetForegroundWindow();
                Line("前台窗口：0x" + fg.ToInt64().ToString("X", CultureInfo.InvariantCulture) + " \"" + Native.GetWindowTextSafe(fg) + "\"");

                MacroSettings s = new MacroSettings();
                s.LoopCount = 1;
                s.StartDelayMs = 0;
                s.ClickHoldMs = 40;
                s.ActivateBeforeClick = true;

                MacroAction c1 = new MacroAction();
                c1.Kind = ActionKind.Click;
                c1.Space = CoordSpace.Screen;
                c1.X = target.X;
                c1.Y = target.Y;
                c1.WaitMs = 200;
                s.Actions.Add(c1);

                MacroAction k1 = new MacroAction();
                k1.Kind = ActionKind.Key;
                k1.KeyCode = Keys.Space;
                k1.WaitMs = 200;
                s.Actions.Add(k1);

                MacroAction c2 = new MacroAction();
                c2.Kind = ActionKind.Click;
                c2.Space = CoordSpace.WindowClient;
                c2.X = 180;
                c2.Y = 130;
                c2.WaitMs = 200;
                s.Actions.Add(c2);

                MacroAction w1 = new MacroAction();
                w1.Kind = ActionKind.Wheel;
                w1.Space = CoordSpace.Screen;
                w1.X = target.X;
                w1.Y = target.Y;
                w1.WheelDelta = 3;
                w1.WaitMs = 200;
                s.Actions.Add(w1);

                Runner runner = new Runner();
                Stopwatch sw = Stopwatch.StartNew();
                runner.Start(s, probe.Handle, probe.Text, false);
                while (runner.IsRunning && sw.ElapsedMilliseconds < 15000)
                {
                    Pump(40);
                }
                Pump(300);

                Line("运行耗时：" + sw.ElapsedMilliseconds + "ms；执行动作数：" + runner.ActionsDone + "；轮次：" + runner.LoopsDone);
                if (!string.IsNullOrEmpty(runner.LastError))
                {
                    Line("运行错误：" + runner.LastError);
                    _pass = false;
                }

                Line("探针统计：左键点击 " + probe.LeftClicks + "，右键 " + probe.RightClicks + "，按键 " + probe.KeyPresses
                    + "，最后点击 (" + probe.LastX + "," + probe.LastY + ")，被激活 " + probe.ActivateCount + " 次");
                Line("右键事件：按下 " + probe.RightDownCount + " / 抬起 " + probe.RightUpCount + "（正常应都是 0）");
                Line("滚轮：收到 " + probe.WheelEvents + " 次事件，累计 " + probe.WheelNotches + " 格");

                // --- 3. 判定 ---
                Line("");
                Line("--- 3) 判定 ---");
                Expect(probe.LeftClicks == 2, "探针应收到 2 次左键点击，实际 " + probe.LeftClicks);
                Expect(probe.KeyPresses >= 1, "探针应收到至少 1 次按键，实际 " + probe.KeyPresses);
                Expect(runner.ActionsDone == 4, "应执行 4 个动作，实际 " + runner.ActionsDone);
                Expect(probe.WheelNotches >= 1, "探针应收到滚轮，实际 " + probe.WheelNotches + " 格");
                Expect(probe.ActivateCount >= 1, "点击前应把探针激活，实际激活 " + probe.ActivateCount + " 次");
                // 回归：只在动作里写左键，结束后绝不能出现任何右键事件
                Expect(probe.RightUpCount == 0 && probe.RightDownCount == 0,
                    "不应产生右键事件（按下 " + probe.RightDownCount + " / 抬起 " + probe.RightUpCount + "）");

                // 恢复鼠标位置
                InputEngine.MoveTo(original.X, original.Y, out detail);

                Line("");
                Line(_pass ? "结果：PASS" : "结果：FAIL");
            }
            catch (Exception ex)
            {
                _pass = false;
                Line("异常：" + ex);
            }
            finally
            {
                try
                {
                    if (other != null)
                    {
                        other.Close();
                    }
                }
                catch
                {
                }
                try
                {
                    if (probe != null)
                    {
                        probe.Close();
                    }
                }
                catch
                {
                }
                WriteReport();
            }
            return _pass ? 0 : 1;
        }

        private static void Expect(bool condition, string message)
        {
            Line((condition ? "[OK]   " : "[FAIL] ") + message);
            if (!condition)
            {
                _pass = false;
            }
        }

        private static void Line(string text)
        {
            Report.AppendLine(text);
            Console.WriteLine(text);
        }

        private static void Pump(int ms)
        {
            int spent = 0;
            while (spent < ms)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(10);
                spent += 10;
            }
        }

        private static void WriteReport()
        {
            string text = Report.ToString();
            string[] targets = new string[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.log"),
                AppPaths.Combine("selftest.log")
            };
            for (int i = 0; i < targets.Length; i++)
            {
                try
                {
                    File.WriteAllText(targets[i], text, new UTF8Encoding(false));
                }
                catch
                {
                }
            }
        }
    }
}

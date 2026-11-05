using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace AutoClicker
{
    internal static class Program
    {
        public const string VersionText = "1.00.0";
        public const string ShortVersion = "1.00.0";

        private static Mutex _mutex;

        [STAThread]
        private static int Main(string[] args)
        {
            AppPaths.EnsureDir();
            AppLog.StartSession();
            InstallCrashHandlers();

            bool selfTest = false;
            foreach (string a in args)
            {
                if (string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase))
                {
                    selfTest = true;
                }
            }

            if (selfTest)
            {
                return SelfTest.Run();
            }

            bool createdNew = true;
            _mutex = new Mutex(true, "AutoClicker3_SingleInstance", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show(
                    "鼠标连点器已经在运行了。\n请使用已经打开的窗口（按 F6 开始/停止）。",
                    "已经在运行",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return 0;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

                // 尽量保证坐标与物理像素一致（manifest 里也声明了 PerMonitorV2）
                try
                {
                    Native.SetThreadDpiAwarenessContext(Native.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
                }
                catch
                {
                }

                Application.Run(new MainForm());
                return 0;
            }
            catch (Exception ex)
            {
                WriteCrash(ex);
                MessageBox.Show(
                    "程序启动失败：" + ex.Message + "\n\n详细信息已写入：" + AppPaths.CrashLogFile,
                    "启动失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }
            finally
            {
                try
                {
                    if (_mutex != null)
                    {
                        _mutex.ReleaseMutex();
                    }
                }
                catch
                {
                }
            }
        }

        private static void InstallCrashHandlers()
        {
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
            {
                WriteCrash(e.Exception);
                try
                {
                    AppLog.WriteError("界面异常：" + e.Exception.Message);
                }
                catch
                {
                }
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                Exception ex = e.ExceptionObject as Exception;
                if (ex != null)
                {
                    WriteCrash(ex);
                }
            };
        }

        private static void WriteCrash(Exception ex)
        {
            try
            {
                AppPaths.EnsureDir();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("版本：" + VersionText);
                sb.AppendLine(ex.ToString());
                sb.AppendLine();
                File.AppendAllText(AppPaths.CrashLogFile, sb.ToString(), new UTF8Encoding(false));
            }
            catch
            {
            }
        }
    }
}

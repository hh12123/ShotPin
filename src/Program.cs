using System;
using System.Threading;
using System.Windows.Forms;
using ShotPin.App;

namespace ShotPin
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (HasFlag(args, "--diag"))
            {
                Diagnostics.WriteReport();
                return;
            }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Local\\ShotPin.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "ShotPin 已经在运行，请查看系统托盘。",
                        "ShotPin",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

                Log.Init();
                Application.ThreadException += OnThreadException;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

                Log.Info("starting UI");
                Application.Run(new TrayAppContext());
                Log.Info("UI exited");
            }
        }

        private static bool HasFlag(string[] args, string flag)
        {
            if (args == null)
            {
                return false;
            }
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Log.Error("UI thread exception", e.Exception);
            try
            {
                if (TrayAppContext.Current != null)
                {
                    TrayAppContext.Current.Notify("发生错误，详情见日志", ToolTipIcon.Warning);
                }
            }
            catch
            {
            }
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Error("unhandled exception", e.ExceptionObject as Exception);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShotPin.App
{
    /// <summary>
    /// 极简文件日志。所有异常都在内部吞掉，避免日志本身影响主流程。
    /// </summary>
    internal static class Log
    {
        private static readonly object Sync = new object();
        private static bool _initialized;
        private static string _dataDir;
        private static string _logDir;

        public static string DataDir
        {
            get
            {
                if (_dataDir == null)
                {
                    _dataDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "ShotPin");
                }
                return _dataDir;
            }
        }

        public static string LogDir
        {
            get
            {
                if (_logDir == null)
                {
                    _logDir = Path.Combine(DataDir, "logs");
                }
                return _logDir;
            }
        }

        public static void Init()
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                Directory.CreateDirectory(LogDir);
                Prune();
                _initialized = true;
                Info("==== ShotPin started ====");
            }
            catch
            {
                _initialized = true;
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Warn(string message)
        {
            Write("WARN", message);
        }

        public static void Error(string message, Exception exception)
        {
            if (exception == null)
            {
                Write("ERROR", message);
            }
            else
            {
                Write("ERROR", message + " | " + exception.GetType().Name + ": " + exception.Message +
                               Environment.NewLine + exception.StackTrace);
            }
        }

        private static void Write(string level, string message)
        {
            if (!_initialized)
            {
                Init();
            }
            try
            {
                Directory.CreateDirectory(LogDir);
                string path = Path.Combine(LogDir, "app-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] " + message +
                              Environment.NewLine;
                lock (Sync)
                {
                    File.AppendAllText(path, line, Encoding.UTF8);
                }
            }
            catch
            {
                // 日志失败不影响功能。
            }
        }

        private static void Prune()
        {
            try
            {
                if (!Directory.Exists(LogDir))
                {
                    return;
                }
                DateTime limit = DateTime.Now.AddDays(-7);
                string[] files = Directory.GetFiles(LogDir, "app-*.log");
                for (int i = 0; i < files.Length; i++)
                {
                    if (File.GetLastWriteTime(files[i]) < limit)
                    {
                        try
                        {
                            File.Delete(files[i]);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
        }
    }
}

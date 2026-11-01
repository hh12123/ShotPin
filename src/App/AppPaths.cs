using System;
using System.IO;
using System.Windows.Forms;

namespace ShotPin.App
{
    /// <summary>
    /// 便携路径：配置、日志优先放在 exe 同目录；目录不可写时回退到 AppData。
    /// </summary>
    internal static class AppPaths
    {
        private static string _dataDir;

        public static string DataDir
        {
            get
            {
                if (_dataDir == null)
                {
                    _dataDir = Resolve();
                }
                return _dataDir;
            }
        }

        public static string SettingsPath
        {
            get { return Path.Combine(DataDir, "settings.ini"); }
        }

        public static string LegacyDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ShotPin");
            }
        }

        private static string Resolve()
        {
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
            if (!string.IsNullOrEmpty(exeDir))
            {
                string probe = Path.Combine(exeDir, ".shotpin_write_test");
                try
                {
                    File.WriteAllText(probe, "probe");
                    File.Delete(probe);
                    return exeDir;
                }
                catch
                {
                    try
                    {
                        if (File.Exists(probe))
                        {
                            File.Delete(probe);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            return LegacyDir;
        }
    }
}

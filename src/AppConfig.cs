using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace NoSleep
{
    public enum CloseAction
    {
        AskEveryTime = 0,
        MinimizeToTaskbar = 1,
        ExitProgram = 2
    }

    public class AppConfig
    {
        public double NetworkThresholdMBps { get; set; }
        public double DiskThresholdMBps { get; set; }
        public int GracePeriodSeconds { get; set; }
        public int ActivationDelaySeconds { get; set; }
        public bool MonitorNetwork { get; set; }
        public bool MonitorDisk { get; set; }
        public bool KeepDisplayOn { get; set; }
        public bool StartWithWindows { get; set; }
        public bool StartMinimized { get; set; }
        public bool ForceAwake { get; set; }
        public CloseAction ActionOnClose { get; set; }

        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "NoSleep";

        private static string _resolvedConfigPath = null;

        public AppConfig()
        {
            NetworkThresholdMBps = 1.0;
            DiskThresholdMBps = 5.0;
            GracePeriodSeconds = 60;
            ActivationDelaySeconds = 5;
            MonitorNetwork = true;
            MonitorDisk = true;
            KeepDisplayOn = false;
            StartWithWindows = false;
            StartMinimized = false;
            ForceAwake = false;
            ActionOnClose = CloseAction.AskEveryTime;
        }

        public static string GetConfigFilePath()
        {
            if (_resolvedConfigPath != null) return _resolvedConfigPath;

            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string portablePath = Path.Combine(appDir, "config.json");
                if (File.Exists(portablePath))
                {
                    _resolvedConfigPath = portablePath;
                    return _resolvedConfigPath;
                }
            }
            catch { }

            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (!string.IsNullOrEmpty(appData))
                {
                    string dir = Path.Combine(appData, "NoSleep");
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    string path = Path.Combine(dir, "config.json");
                    _resolvedConfigPath = path;
                    return _resolvedConfigPath;
                }
            }
            catch { }

            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(localAppData))
                {
                    string dir = Path.Combine(localAppData, "NoSleep");
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    string path = Path.Combine(dir, "config.json");
                    _resolvedConfigPath = path;
                    return _resolvedConfigPath;
                }
            }
            catch { }

            try
            {
                string fallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                _resolvedConfigPath = fallback;
                return _resolvedConfigPath;
            }
            catch
            {
                _resolvedConfigPath = "config.json";
                return _resolvedConfigPath;
            }
        }

        public static AppConfig Load()
        {
            AppConfig config = new AppConfig();
            try
            {
                string path = GetConfigFilePath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    config.ParseJson(json);
                }
                else
                {
                    string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                    if (File.Exists(localPath))
                    {
                        string json = File.ReadAllText(localPath, Encoding.UTF8);
                        config.ParseJson(json);
                        _resolvedConfigPath = localPath;
                    }
                }
            }
            catch { }

            try
            {
                config.StartWithWindows = CheckAutostartEnabled();
            }
            catch { }

            return config;
        }

        public void Save()
        {
            try
            {
                string path = GetConfigFilePath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = ToJson();
                File.WriteAllText(path, json, Encoding.UTF8);
            }
            catch
            {
                try
                {
                    string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                    string json = ToJson();
                    File.WriteAllText(localPath, json, Encoding.UTF8);
                    _resolvedConfigPath = localPath;
                }
                catch { }
            }

            try
            {
                SetAutostart(StartWithWindows);
            }
            catch { }
        }

        public static bool CheckAutostartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue(AppName);
                        return val != null;
                    }
                }
            }
            catch { }
            return false;
        }

        public static void SetAutostart(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                            if (string.IsNullOrEmpty(exePath))
                            {
                                exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NoSleep.exe");
                            }
                            key.SetValue(AppName, string.Format("\"{0}\" --minimized", exePath));
                        }
                        else
                        {
                            key.DeleteValue(AppName, false);
                        }
                    }
                }
            }
            catch { }
        }

        public string ToJson()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine(string.Format("  \"NetworkThresholdMBps\": {0},", NetworkThresholdMBps.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)));
            sb.AppendLine(string.Format("  \"DiskThresholdMBps\": {0},", DiskThresholdMBps.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)));
            sb.AppendLine(string.Format("  \"GracePeriodSeconds\": {0},", GracePeriodSeconds));
            sb.AppendLine(string.Format("  \"ActivationDelaySeconds\": {0},", ActivationDelaySeconds));
            sb.AppendLine(string.Format("  \"MonitorNetwork\": {0},", MonitorNetwork ? "true" : "false"));
            sb.AppendLine(string.Format("  \"MonitorDisk\": {0},", MonitorDisk ? "true" : "false"));
            sb.AppendLine(string.Format("  \"KeepDisplayOn\": {0},", KeepDisplayOn ? "true" : "false"));
            sb.AppendLine(string.Format("  \"StartWithWindows\": {0},", StartWithWindows ? "true" : "false"));
            sb.AppendLine(string.Format("  \"StartMinimized\": {0},", StartMinimized ? "true" : "false"));
            sb.AppendLine(string.Format("  \"ForceAwake\": {0},", ForceAwake ? "true" : "false"));
            sb.AppendLine(string.Format("  \"ActionOnClose\": {0}", (int)ActionOnClose));
            sb.AppendLine("}");
            return sb.ToString();
        }

        private void ParseJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            NetworkThresholdMBps = ExtractDouble(json, "NetworkThresholdMBps", NetworkThresholdMBps);
            DiskThresholdMBps = ExtractDouble(json, "DiskThresholdMBps", DiskThresholdMBps);
            GracePeriodSeconds = ExtractInt(json, "GracePeriodSeconds", GracePeriodSeconds);
            ActivationDelaySeconds = ExtractInt(json, "ActivationDelaySeconds", ActivationDelaySeconds);
            MonitorNetwork = ExtractBool(json, "MonitorNetwork", MonitorNetwork);
            MonitorDisk = ExtractBool(json, "MonitorDisk", MonitorDisk);
            KeepDisplayOn = ExtractBool(json, "KeepDisplayOn", KeepDisplayOn);
            StartWithWindows = ExtractBool(json, "StartWithWindows", StartWithWindows);
            StartMinimized = ExtractBool(json, "StartMinimized", StartMinimized);
            ForceAwake = ExtractBool(json, "ForceAwake", ForceAwake);
            
            int actionVal = ExtractInt(json, "ActionOnClose", (int)ActionOnClose);
            if (actionVal >= 0 && actionVal <= 2)
            {
                ActionOnClose = (CloseAction)actionVal;
            }
            else if (actionVal == 3) // Legacy mapping for ExitProgram
            {
                ActionOnClose = CloseAction.ExitProgram;
            }
        }

        private static double ExtractDouble(string json, string key, double defaultValue)
        {
            Match m = Regex.Match(json, string.Format("\"{0}\"\\s*:\\s*([0-9\\.]+)", key));
            double val;
            if (m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val))
            {
                return val;
            }
            return defaultValue;
        }

        private static int ExtractInt(string json, string key, int defaultValue)
        {
            Match m = Regex.Match(json, string.Format("\"{0}\"\\s*:\\s*([0-9]+)", key));
            int val;
            if (m.Success && int.TryParse(m.Groups[1].Value, out val))
            {
                return val;
            }
            return defaultValue;
        }

        private static bool ExtractBool(string json, string key, bool defaultValue)
        {
            Match m = Regex.Match(json, string.Format("\"{0}\"\\s*:\\s*(true|false)", key), RegexOptions.IgnoreCase);
            if (m.Success)
            {
                return string.Equals(m.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
            }
            return defaultValue;
        }
    }
}

using System;
using System.Threading;
using System.Windows.Forms;

namespace NoSleep
{
    internal static class Program
    {
        private const string AppGuid = "NoSleep-Steam-AntiStandby-App-8F9E2A10";

        [STAThread]
        static void Main(string[] args)
        {
            bool isNewInstance;
            using (Mutex mutex = new Mutex(true, AppGuid, out isNewInstance))
            {
                if (!isNewInstance)
                {
                    MessageBox.Show("NoSleep is already running!", "NoSleep", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                AppConfig config = AppConfig.Load();
                PowerManager powerManager = new PowerManager();
                ActivityMonitor monitor = new ActivityMonitor(config, powerManager);

                bool startMinimized = config.StartMinimized;
                foreach (string arg in args)
                {
                    if (arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase) || arg.Equals("-m", StringComparison.OrdinalIgnoreCase))
                    {
                        startMinimized = true;
                    }
                }

                Application.Run(new MainForm(config, powerManager, monitor, startMinimized));
            }
        }
    }
}

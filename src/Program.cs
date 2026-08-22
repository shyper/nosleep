using System;
using System.Threading;
using System.Windows.Forms;

namespace NoSleep
{
    internal static class Program
    {
        private const string AppGuid = "NoSleep-Steam-AntiStandby-App-8F9E2A10";
        private const string ActivateEventName = AppGuid + "-Activate";

        [STAThread]
        static void Main(string[] args)
        {
            bool isNewInstance;
            using (Mutex mutex = new Mutex(true, AppGuid, out isNewInstance))
            {
                if (!isNewInstance)
                {
                    SignalExistingInstance();
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

                MainForm mainForm = new MainForm(config, powerManager, monitor, startMinimized);

                using (EventWaitHandle activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName))
                {
                    RegisteredWaitHandle registration = ThreadPool.RegisterWaitForSingleObject(
                        activateEvent,
                        delegate(object state, bool timedOut)
                        {
                            try
                            {
                                mainForm.BeginInvoke((Action)(delegate { mainForm.RestoreFromTray(); }));
                            }
                            catch
                            {
                                // Form handle gone or app shutting down.
                            }
                        },
                        null,
                        -1,
                        false);

                    Application.Run(mainForm);

                    registration.Unregister(null);
                }
            }
        }

        private static void SignalExistingInstance()
        {
            try
            {
                using (EventWaitHandle evt = EventWaitHandle.OpenExisting(ActivateEventName))
                {
                    evt.Set();
                    return;
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // Existing instance is shutting down and no longer listens.
            }
            catch
            {
                // Unexpected IPC failure - fall back to the classic notice.
            }

            MessageBox.Show("NoSleep is already running!", "NoSleep", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

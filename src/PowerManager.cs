using System;
using System.Runtime.InteropServices;

namespace NoSleep
{
    [Flags]
    public enum ExecutionState : uint
    {
        ES_SYSTEM_REQUIRED   = 0x00000001,
        ES_DISPLAY_REQUIRED  = 0x00000002,
        ES_USER_PRESENT      = 0x00000004,
        ES_AWAYMODE_REQUIRED = 0x00000040,
        ES_CONTINUOUS        = 0x80000000
    }

    public class PowerManager
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

        public bool IsSleepBlocked { get; private set; }
        public bool IsDisplayBlocked { get; private set; }
        public DateTime? LastStateChange { get; private set; }

        public event Action<bool, bool> StateChanged;

        public PowerManager()
        {
            IsSleepBlocked = false;
            IsDisplayBlocked = false;
            LastStateChange = DateTime.Now;
        }

        /// <summary>
        /// Enables or disables Windows standby/sleep prevention.
        /// </summary>
        /// <param name="blockSleep">True to prevent sleep, False to allow standard Windows power management</param>
        /// <param name="keepDisplayOn">True to also prevent the display/screen from turning off</param>
        public bool SetSleepState(bool blockSleep, bool keepDisplayOn)
        {
            if (blockSleep == IsSleepBlocked && keepDisplayOn == IsDisplayBlocked)
            {
                return true; // No state change needed
            }

            ExecutionState flags = ExecutionState.ES_CONTINUOUS;

            if (blockSleep)
            {
                flags |= ExecutionState.ES_SYSTEM_REQUIRED | ExecutionState.ES_AWAYMODE_REQUIRED;
                if (keepDisplayOn)
                {
                    flags |= ExecutionState.ES_DISPLAY_REQUIRED;
                }
            }

            ExecutionState result = SetThreadExecutionState(flags);

            if (result == 0)
            {
                // Fallback without AwayMode if unsupported by system
                if (blockSleep)
                {
                    flags = ExecutionState.ES_CONTINUOUS | ExecutionState.ES_SYSTEM_REQUIRED;
                    if (keepDisplayOn)
                    {
                        flags |= ExecutionState.ES_DISPLAY_REQUIRED;
                    }
                    result = SetThreadExecutionState(flags);
                }
            }

            bool success = (result != 0);
            if (success)
            {
                IsSleepBlocked = blockSleep;
                IsDisplayBlocked = (blockSleep && keepDisplayOn);
                LastStateChange = DateTime.Now;
                
                Action<bool, bool> handler = StateChanged;
                if (handler != null)
                {
                    handler(IsSleepBlocked, IsDisplayBlocked);
                }
            }

            return success;
        }

        /// <summary>
        /// Resets all execution states back to default (e.g. on application exit).
        /// </summary>
        public void Reset()
        {
            SetThreadExecutionState(ExecutionState.ES_CONTINUOUS);
            IsSleepBlocked = false;
            IsDisplayBlocked = false;
            LastStateChange = DateTime.Now;
            
            Action<bool, bool> handler = StateChanged;
            if (handler != null)
            {
                handler(false, false);
            }
        }
    }
}

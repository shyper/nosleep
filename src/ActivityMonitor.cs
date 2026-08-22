using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Timers;

namespace NoSleep
{
    public class ActivityData
    {
        public double NetworkDownMBps { get; set; }
        public double NetworkUpMBps { get; set; }
        public double NetworkTotalMBps
        {
            get { return NetworkDownMBps + NetworkUpMBps; }
        }

        public double DiskReadMBps { get; set; }
        public double DiskWriteMBps { get; set; }
        public double DiskTotalMBps
        {
            get { return DiskReadMBps + DiskWriteMBps; }
        }

        public bool IsNetworkActive { get; set; }
        public bool IsDiskActive { get; set; }
        public bool IsProcessActive { get; set; }
        public System.Collections.Generic.List<string> ActiveMonitoredProcesses { get; set; }
        public bool IsActiveRaw { get; set; }

        public bool IsPendingActivation { get; set; }
        public int PendingActivationSeconds { get; set; }
        public int ActivationDelayRequiredSeconds { get; set; }

        public bool IsConfirmedActive { get; set; }
        public bool InGracePeriod { get; set; }
        public int GracePeriodRemainingSeconds { get; set; }
        public bool IsSleepBlocked { get; set; }
        public bool IsForceAwake { get; set; }
    }

    public class ActivityMonitor : IDisposable
    {
        private readonly AppConfig _config;
        private readonly PowerManager _powerManager;
        private readonly Timer _timer;

        private long _lastBytesReceived;
        private long _lastBytesSent;
        private DateTime _lastNetworkSampleTime;

        private PerformanceCounter _diskReadCounter;
        private PerformanceCounter _diskWriteCounter;
        private bool _diskCounterAvailable;

        private DateTime? _lastActiveTime;
        private int _consecutiveActiveSeconds;
        private bool _isCurrentlyEngaged;
        private bool _isMonitoring;

        public event Action<ActivityData> ActivityUpdated;

        public ActivityMonitor(AppConfig config, PowerManager powerManager)
        {
            _config = config;
            _powerManager = powerManager;
            _lastBytesReceived = -1;
            _lastBytesSent = -1;
            _lastNetworkSampleTime = DateTime.MinValue;
            _diskCounterAvailable = true;
            _lastActiveTime = null;
            _consecutiveActiveSeconds = 0;
            _isCurrentlyEngaged = false;
            _isMonitoring = false;

            InitDiskCounters();

            _timer = new Timer(1000); // 1-second interval
            _timer.AutoReset = true;
            _timer.Elapsed += OnTimerElapsed;
        }

        private void InitDiskCounters()
        {
            try
            {
                _diskReadCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total", true);
                _diskWriteCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total", true);
                
                // Initial sample to prime counter values
                _diskReadCounter.NextValue();
                _diskWriteCounter.NextValue();
                _diskCounterAvailable = true;
            }
            catch
            {
                try
                {
                    // Fallback to LogicalDisk
                    _diskReadCounter = new PerformanceCounter("LogicalDisk", "Disk Read Bytes/sec", "_Total", true);
                    _diskWriteCounter = new PerformanceCounter("LogicalDisk", "Disk Write Bytes/sec", "_Total", true);
                    _diskReadCounter.NextValue();
                    _diskWriteCounter.NextValue();
                    _diskCounterAvailable = true;
                }
                catch
                {
                    _diskCounterAvailable = false;
                }
            }
        }

        public void Start()
        {
            if (_isMonitoring) return;
            _isMonitoring = true;
            _lastNetworkSampleTime = DateTime.UtcNow;
            _consecutiveActiveSeconds = 0;
            _isCurrentlyEngaged = false;
            SampleNetworkBytes(out _lastBytesReceived, out _lastBytesSent);
            _timer.Start();
        }

        public void Stop()
        {
            if (!_isMonitoring) return;
            _isMonitoring = false;
            _timer.Stop();
            _consecutiveActiveSeconds = 0;
            _isCurrentlyEngaged = false;
            _powerManager.Reset();
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (!_isMonitoring) return;

            try
            {
                DateTime now = DateTime.UtcNow;
                
                // 1. Measure network throughput
                double netDownMBps = 0;
                double netUpMBps = 0;

                long curReceived, curSent;
                if (SampleNetworkBytes(out curReceived, out curSent))
                {
                    if (_lastBytesReceived >= 0 && _lastBytesSent >= 0)
                    {
                        double seconds = (now - _lastNetworkSampleTime).TotalSeconds;
                        if (seconds > 0.1)
                        {
                            long diffReceived = Math.Max(0, curReceived - _lastBytesReceived);
                            long diffSent = Math.Max(0, curSent - _lastBytesSent);

                            netDownMBps = (diffReceived / (1024.0 * 1024.0)) / seconds;
                            netUpMBps = (diffSent / (1024.0 * 1024.0)) / seconds;
                        }
                    }

                    _lastBytesReceived = curReceived;
                    _lastBytesSent = curSent;
                    _lastNetworkSampleTime = now;
                }

                // 2. Measure disk throughput
                double diskReadMBps = 0;
                double diskWriteMBps = 0;

                if (_diskCounterAvailable && _diskReadCounter != null && _diskWriteCounter != null)
                {
                    try
                    {
                        float readBytes = _diskReadCounter.NextValue();
                        float writeBytes = _diskWriteCounter.NextValue();

                        diskReadMBps = Math.Max(0, readBytes / (1024.0 * 1024.0));
                        diskWriteMBps = Math.Max(0, writeBytes / (1024.0 * 1024.0));
                    }
                    catch
                    {
                        // Keep as 0 if counter read fails
                    }
                }

                // 3. Evaluate activity thresholds & Peak filtering
                bool netActive = _config.MonitorNetwork && (netDownMBps >= _config.NetworkThresholdMBps);
                bool diskActive = _config.MonitorDisk && ((diskReadMBps + diskWriteMBps) >= _config.DiskThresholdMBps);
                
                System.Collections.Generic.List<string> activeProcesses;
                bool procActive = CheckMonitoredProcesses(out activeProcesses);

                bool isRawActive = netActive || diskActive || procActive;
                bool forceAwake = _config.ForceAwake;

                bool isPendingActivation = false;
                bool isConfirmedActive = false;
                bool inGracePeriod = false;
                int graceRemaining = 0;
                bool shouldBlock = false;

                if (forceAwake)
                {
                    shouldBlock = true;
                    _isCurrentlyEngaged = true;
                    _lastActiveTime = now;
                    _consecutiveActiveSeconds = 0;
                }
                else if (procActive)
                {
                    // Monitored application running: immediately engage standby block
                    shouldBlock = true;
                    isConfirmedActive = true;
                    _isCurrentlyEngaged = true;
                    _lastActiveTime = now;
                    _consecutiveActiveSeconds = _config.ActivationDelaySeconds;
                }
                else if (netActive || diskActive)
                {
                    if (_isCurrentlyEngaged)
                    {
                        // Already in confirmed active or cooldown state: maintain active
                        shouldBlock = true;
                        isConfirmedActive = true;
                        _lastActiveTime = now;
                        _consecutiveActiveSeconds = _config.ActivationDelaySeconds;
                    }
                    else
                    {
                        // From Idle: require consecutive seconds before locking standby
                        _consecutiveActiveSeconds++;
                        if (_consecutiveActiveSeconds >= _config.ActivationDelaySeconds)
                        {
                            _isCurrentlyEngaged = true;
                            shouldBlock = true;
                            isConfirmedActive = true;
                            _lastActiveTime = now;
                        }
                        else
                        {
                            // In peak verification phase (not blocking sleep yet)
                            isPendingActivation = true;
                            shouldBlock = false;
                        }
                    }
                }
                else
                {
                    // No raw activity in this tick
                    _consecutiveActiveSeconds = 0;

                    if (_isCurrentlyEngaged && _lastActiveTime.HasValue)
                    {
                        double elapsed = (now - _lastActiveTime.Value).TotalSeconds;
                        if (elapsed < _config.GracePeriodSeconds)
                        {
                            inGracePeriod = true;
                            graceRemaining = (int)Math.Ceiling(_config.GracePeriodSeconds - elapsed);
                            shouldBlock = true;
                        }
                        else
                        {
                            // Cooldown expired
                            _isCurrentlyEngaged = false;
                            _lastActiveTime = null;
                            shouldBlock = false;
                        }
                    }
                    else
                    {
                        _isCurrentlyEngaged = false;
                        shouldBlock = false;
                    }
                }

                // 4. Update Windows execution state
                _powerManager.SetSleepState(shouldBlock, _config.KeepDisplayOn);

                // 5. Build data object and fire event
                ActivityData data = new ActivityData
                {
                    NetworkDownMBps = netDownMBps,
                    NetworkUpMBps = netUpMBps,
                    DiskReadMBps = diskReadMBps,
                    DiskWriteMBps = diskWriteMBps,
                    IsNetworkActive = netActive,
                    IsDiskActive = diskActive,
                    IsProcessActive = procActive,
                    ActiveMonitoredProcesses = activeProcesses,
                    IsActiveRaw = isRawActive,
                    IsPendingActivation = isPendingActivation,
                    PendingActivationSeconds = _consecutiveActiveSeconds,
                    ActivationDelayRequiredSeconds = _config.ActivationDelaySeconds,
                    IsConfirmedActive = isConfirmedActive,
                    InGracePeriod = inGracePeriod,
                    GracePeriodRemainingSeconds = graceRemaining,
                    IsSleepBlocked = shouldBlock,
                    IsForceAwake = forceAwake
                };

                Action<ActivityData> handler = ActivityUpdated;
                if (handler != null)
                {
                    handler(data);
                }
            }
            catch
            {
                // Prevent unhandled timer exceptions
            }
        }

        private bool CheckMonitoredProcesses(out System.Collections.Generic.List<string> activeProcesses)
        {
            activeProcesses = new System.Collections.Generic.List<string>();
            if (!_config.MonitorProcesses || _config.MonitoredProcesses == null || _config.MonitoredProcesses.Count == 0)
            {
                return false;
            }

            try
            {
                Process[] runningProcesses = Process.GetProcesses();
                try
                {
                    System.Collections.Generic.HashSet<string> runningNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < runningProcesses.Length; i++)
                    {
                        try
                        {
                            runningNames.Add(runningProcesses[i].ProcessName);
                        }
                        catch { }
                    }

                    for (int j = 0; j < _config.MonitoredProcesses.Count; j++)
                    {
                        string target = _config.MonitoredProcesses[j];
                        if (string.IsNullOrEmpty(target)) continue;

                        string cleanName = target.Trim();
                        if (cleanName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            cleanName = cleanName.Substring(0, cleanName.Length - 4);
                        }

                        if (runningNames.Contains(cleanName))
                        {
                            activeProcesses.Add(target.Trim());
                        }
                    }
                }
                finally
                {
                    for (int i = 0; i < runningProcesses.Length; i++)
                    {
                        try
                        {
                            runningProcesses[i].Dispose();
                        }
                        catch { }
                    }
                }

                return activeProcesses.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private bool SampleNetworkBytes(out long totalReceived, out long totalSent)
        {
            totalReceived = 0;
            totalSent = 0;
            try
            {
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface ni in interfaces)
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    {
                        try
                        {
                            IPInterfaceStatistics stats = ni.GetIPStatistics();
                            totalReceived += stats.BytesReceived;
                            totalSent += stats.BytesSent;
                        }
                        catch { }
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            Stop();
            if (_timer != null) _timer.Dispose();
            if (_diskReadCounter != null) _diskReadCounter.Dispose();
            if (_diskWriteCounter != null) _diskWriteCounter.Dispose();
        }
    }
}

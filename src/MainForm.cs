using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace NoSleep
{
    public enum SystemActivityState
    {
        Initial,
        Idle,
        PendingVerification,
        Active,
        Cooldown,
        Forced
    }

    public class MainForm : Form
    {
        private IContainer components;

        private readonly AppConfig _config;
        private readonly PowerManager _powerManager;
        private readonly ActivityMonitor _monitor;
        private readonly bool _startMinimized;

        // UI Controls
        private Panel _headerPanel;
        private Label _titleLabel;
        private Label _subtitleLabel;
        private CheckBox _forceAwakeCheck;

        private Panel _statusCard;
        private Label _statusTitleLabel;
        private Label _statusDetailLabel;

        private Panel _netCard;
        private Label _netTitleLabel;
        private Label _netValueLabel;
        private Label _netSubValueLabel;
        private ProgressBar _netProgressBar;
        private Label _netStatusLabel;

        private Panel _diskCard;
        private Label _diskTitleLabel;
        private Label _diskValueLabel;
        private Label _diskSubValueLabel;
        private ProgressBar _diskProgressBar;
        private Label _diskStatusLabel;

        // Settings Controls
        private GroupBox _settingsGroup;
        private NumericUpDown _numNetThreshold;
        private NumericUpDown _numDiskThreshold;
        private NumericUpDown _numGracePeriod;
        private NumericUpDown _numActivationDelay;
        private CheckBox _chkMonitorNet;
        private CheckBox _chkMonitorDisk;
        private CheckBox _chkKeepDisplay;
        private CheckBox _chkAutostart;
        private CheckBox _chkStartMinimized;
        private ComboBox _cmbCloseAction;

        // Log / Event list
        private ListBox _logList;
        private ContextMenuStrip _logContextMenu;
        private readonly List<string> _pendingLogs = new List<string>();
        private readonly object _logLock = new object();

        private Button _btnMinimize;
        private Button _btnSaveSettings;
        private Button _btnClearLog;

        // System Tray
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private ToolStripMenuItem _trayStatusItem;
        private ToolStripMenuItem _trayShowItem;
        private ToolStripMenuItem _trayAwakeItem;
        private ToolStripMenuItem _trayExitItem;

        private bool _isExplicitExit = false;
        private ActivityData _lastData = null;
        private SystemActivityState _lastState = SystemActivityState.Initial;

        public MainForm(AppConfig config, PowerManager powerManager, ActivityMonitor monitor, bool startMinimized)
        {
            _config = config;
            _powerManager = powerManager;
            _monitor = monitor;
            _startMinimized = startMinimized;

            InitializeFormProperties();
            InitializeComponents();

            _monitor.ActivityUpdated += OnActivityUpdated;
            _monitor.Start();

            AddLogEntry("NoSleep started. Real-time background monitoring active.");
            AddLogEntry(string.Format("Config: Net ≥ {0:0.0} MB/s, Disk ≥ {1:0.0} MB/s, Trigger Delay = {2}s, Cooldown = {3}s.", 
                _config.NetworkThresholdMBps, _config.DiskThresholdMBps, _config.ActivationDelaySeconds, _config.GracePeriodSeconds));
        }

        private void InitializeFormProperties()
        {
            this.Text = "NoSleep - Standby Prevention Utility";
            this.Size = new Size(740, 800);
            this.MinimumSize = new Size(700, 760);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(24, 24, 37);
            this.ForeColor = Color.FromArgb(205, 214, 244);
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.ShowInTaskbar = true;
            this.MinimizeBox = true;
            this.MaximizeBox = false;

            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(icoPath))
                {
                    this.Icon = new Icon(icoPath);
                }
                else
                {
                    this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
            }
            catch
            {
                this.Icon = SystemIcons.Application;
            }

            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }

        private void InitializeComponents()
        {
            this.components = new Container();

            // 1. Header Panel
            _headerPanel = new Panel();
            _headerPanel.Location = new Point(20, 15);
            _headerPanel.Size = new Size(685, 60);
            _headerPanel.BackColor = Color.FromArgb(30, 30, 46);
            _headerPanel.Paint += delegate(object s, PaintEventArgs e)
            {
                DrawRoundedCard(e.Graphics, _headerPanel.ClientRectangle, Color.FromArgb(30, 30, 46), Color.FromArgb(49, 50, 68), 1);
            };

            _titleLabel = new Label();
            _titleLabel.Text = "⚡ NoSleep";
            _titleLabel.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
            _titleLabel.ForeColor = Color.FromArgb(245, 194, 231);
            _titleLabel.Location = new Point(15, 8);
            _titleLabel.AutoSize = true;
            _titleLabel.BackColor = Color.Transparent;
            _headerPanel.Controls.Add(_titleLabel);

            _subtitleLabel = new Label();
            _subtitleLabel.Text = "Automatic standby prevention for Steam downloads, updates & disk activity";
            _subtitleLabel.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            _subtitleLabel.ForeColor = Color.FromArgb(166, 173, 200);
            _subtitleLabel.Location = new Point(16, 34);
            _subtitleLabel.AutoSize = true;
            _subtitleLabel.BackColor = Color.Transparent;
            _headerPanel.Controls.Add(_subtitleLabel);

            _forceAwakeCheck = new CheckBox();
            _forceAwakeCheck.Text = "Keep PC Awake";
            _forceAwakeCheck.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _forceAwakeCheck.ForeColor = Color.FromArgb(137, 180, 250);
            _forceAwakeCheck.Location = new Point(515, 18);
            _forceAwakeCheck.Size = new Size(155, 26);
            _forceAwakeCheck.Checked = _config.ForceAwake;
            _forceAwakeCheck.BackColor = Color.Transparent;
            _forceAwakeCheck.CheckedChanged += delegate
            {
                _config.ForceAwake = _forceAwakeCheck.Checked;
                _config.Save();
                if (_trayAwakeItem != null && _trayAwakeItem.Checked != _forceAwakeCheck.Checked)
                {
                    _trayAwakeItem.Checked = _forceAwakeCheck.Checked;
                }
            };
            _headerPanel.Controls.Add(_forceAwakeCheck);

            this.Controls.Add(_headerPanel);

            // 2. Dynamic Status Card
            _statusCard = new Panel();
            _statusCard.Location = new Point(20, 85);
            _statusCard.Size = new Size(685, 75);
            _statusCard.BackColor = Color.FromArgb(30, 30, 46);
            _statusCard.Paint += delegate(object s, PaintEventArgs e)
            {
                Color border = Color.FromArgb(148, 163, 184);
                Color bg = Color.FromArgb(30, 30, 46);

                if (_lastData != null)
                {
                    if (_lastData.IsForceAwake)
                    {
                        border = Color.FromArgb(59, 130, 246);
                        bg = Color.FromArgb(23, 37, 84);
                    }
                    else if (_lastData.IsConfirmedActive)
                    {
                        border = Color.FromArgb(34, 197, 94);
                        bg = Color.FromArgb(20, 83, 45);
                    }
                    else if (_lastData.IsPendingActivation)
                    {
                        border = Color.FromArgb(56, 189, 248);
                        bg = Color.FromArgb(12, 74, 110);
                    }
                    else if (_lastData.InGracePeriod)
                    {
                        border = Color.FromArgb(234, 179, 8);
                        bg = Color.FromArgb(66, 32, 6);
                    }
                }
                DrawRoundedCard(e.Graphics, _statusCard.ClientRectangle, bg, border, 2);
            };

            _statusTitleLabel = new Label();
            _statusTitleLabel.Text = "⚪ NORMAL SLEEP ALLOWED (IDLE)";
            _statusTitleLabel.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            _statusTitleLabel.ForeColor = Color.FromArgb(166, 173, 200);
            _statusTitleLabel.Location = new Point(20, 14);
            _statusTitleLabel.AutoSize = true;
            _statusTitleLabel.BackColor = Color.Transparent;
            _statusCard.Controls.Add(_statusTitleLabel);

            _statusDetailLabel = new Label();
            _statusDetailLabel.Text = "Network and disk throughput are below threshold levels.";
            _statusDetailLabel.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            _statusDetailLabel.ForeColor = Color.FromArgb(205, 214, 244);
            _statusDetailLabel.Location = new Point(21, 42);
            _statusDetailLabel.AutoSize = true;
            _statusDetailLabel.BackColor = Color.Transparent;
            _statusCard.Controls.Add(_statusDetailLabel);

            this.Controls.Add(_statusCard);

            // 3. Network Metric Card
            _netCard = new Panel();
            _netCard.Location = new Point(20, 170);
            _netCard.Size = new Size(335, 120);
            _netCard.BackColor = Color.FromArgb(30, 30, 46);
            _netCard.Paint += delegate(object s, PaintEventArgs e)
            {
                DrawRoundedCard(e.Graphics, _netCard.ClientRectangle, Color.FromArgb(30, 30, 46), Color.FromArgb(69, 71, 90), 1);
            };

            _netTitleLabel = new Label();
            _netTitleLabel.Text = "🌐 NETWORK THROUGHPUT";
            _netTitleLabel.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _netTitleLabel.ForeColor = Color.FromArgb(137, 180, 250);
            _netTitleLabel.Location = new Point(15, 12);
            _netTitleLabel.AutoSize = true;
            _netTitleLabel.BackColor = Color.Transparent;
            _netCard.Controls.Add(_netTitleLabel);

            _netValueLabel = new Label();
            _netValueLabel.Text = "0.0 MB/s";
            _netValueLabel.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            _netValueLabel.ForeColor = Color.White;
            _netValueLabel.Location = new Point(14, 32);
            _netValueLabel.AutoSize = true;
            _netValueLabel.BackColor = Color.Transparent;
            _netCard.Controls.Add(_netValueLabel);

            _netSubValueLabel = new Label();
            _netSubValueLabel.Text = "↓ 0.0 MB/s   ↑ 0.0 MB/s";
            _netSubValueLabel.Font = new Font("Segoe UI", 8f);
            _netSubValueLabel.ForeColor = Color.FromArgb(166, 173, 200);
            _netSubValueLabel.Location = new Point(17, 63);
            _netSubValueLabel.AutoSize = true;
            _netSubValueLabel.BackColor = Color.Transparent;
            _netCard.Controls.Add(_netSubValueLabel);

            _netProgressBar = new ProgressBar();
            _netProgressBar.Location = new Point(18, 85);
            _netProgressBar.Size = new Size(295, 8);
            _netProgressBar.Maximum = 100;
            _netProgressBar.Value = 0;
            _netCard.Controls.Add(_netProgressBar);

            _netStatusLabel = new Label();
            _netStatusLabel.Text = "Idle (< 1.0 MB/s)";
            _netStatusLabel.Font = new Font("Segoe UI", 7.5f);
            _netStatusLabel.ForeColor = Color.FromArgb(148, 163, 184);
            _netStatusLabel.Location = new Point(17, 98);
            _netStatusLabel.AutoSize = true;
            _netStatusLabel.BackColor = Color.Transparent;
            _netCard.Controls.Add(_netStatusLabel);

            this.Controls.Add(_netCard);

            // 4. Disk Metric Card
            _diskCard = new Panel();
            _diskCard.Location = new Point(370, 170);
            _diskCard.Size = new Size(335, 120);
            _diskCard.BackColor = Color.FromArgb(30, 30, 46);
            _diskCard.Paint += delegate(object s, PaintEventArgs e)
            {
                DrawRoundedCard(e.Graphics, _diskCard.ClientRectangle, Color.FromArgb(30, 30, 46), Color.FromArgb(69, 71, 90), 1);
            };

            _diskTitleLabel = new Label();
            _diskTitleLabel.Text = "💾 DISK ACTIVITY";
            _diskTitleLabel.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _diskTitleLabel.ForeColor = Color.FromArgb(166, 227, 161);
            _diskTitleLabel.Location = new Point(15, 12);
            _diskTitleLabel.AutoSize = true;
            _diskTitleLabel.BackColor = Color.Transparent;
            _diskCard.Controls.Add(_diskTitleLabel);

            _diskValueLabel = new Label();
            _diskValueLabel.Text = "0.0 MB/s";
            _diskValueLabel.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            _diskValueLabel.ForeColor = Color.White;
            _diskValueLabel.Location = new Point(14, 32);
            _diskValueLabel.AutoSize = true;
            _diskValueLabel.BackColor = Color.Transparent;
            _diskCard.Controls.Add(_diskValueLabel);

            _diskSubValueLabel = new Label();
            _diskSubValueLabel.Text = "Write: 0.0 MB/s   Read: 0.0 MB/s";
            _diskSubValueLabel.Font = new Font("Segoe UI", 8f);
            _diskSubValueLabel.ForeColor = Color.FromArgb(166, 173, 200);
            _diskSubValueLabel.Location = new Point(17, 63);
            _diskSubValueLabel.AutoSize = true;
            _diskSubValueLabel.BackColor = Color.Transparent;
            _diskCard.Controls.Add(_diskSubValueLabel);

            _diskProgressBar = new ProgressBar();
            _diskProgressBar.Location = new Point(18, 85);
            _diskProgressBar.Size = new Size(295, 8);
            _diskProgressBar.Maximum = 100;
            _diskProgressBar.Value = 0;
            _diskCard.Controls.Add(_diskProgressBar);

            _diskStatusLabel = new Label();
            _diskStatusLabel.Text = "Idle (< 5.0 MB/s)";
            _diskStatusLabel.Font = new Font("Segoe UI", 7.5f);
            _diskStatusLabel.ForeColor = Color.FromArgb(148, 163, 184);
            _diskStatusLabel.Location = new Point(17, 98);
            _diskStatusLabel.AutoSize = true;
            _diskStatusLabel.BackColor = Color.Transparent;
            _diskCard.Controls.Add(_diskStatusLabel);

            this.Controls.Add(_diskCard);

            // 5. Settings Panel
            _settingsGroup = new GroupBox();
            _settingsGroup.Text = " Settings & Thresholds ";
            _settingsGroup.Location = new Point(20, 300);
            _settingsGroup.Size = new Size(685, 255);
            _settingsGroup.ForeColor = Color.FromArgb(245, 194, 231);
            _settingsGroup.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _settingsGroup.BackColor = Color.Transparent;

            // Row 1: Net Threshold & Disk Threshold
            Label lblNetThresh = new Label();
            lblNetThresh.Text = "Network Threshold (MB/s):";
            lblNetThresh.Location = new Point(20, 24);
            lblNetThresh.Size = new Size(200, 20);
            lblNetThresh.ForeColor = Color.FromArgb(205, 214, 244);
            lblNetThresh.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            _settingsGroup.Controls.Add(lblNetThresh);

            _numNetThreshold = new NumericUpDown();
            _numNetThreshold.Location = new Point(230, 22);
            _numNetThreshold.Size = new Size(80, 24);
            _numNetThreshold.DecimalPlaces = 1;
            _numNetThreshold.Increment = 0.5M;
            _numNetThreshold.Minimum = 0.1M;
            _numNetThreshold.Maximum = 500.0M;
            _numNetThreshold.Value = (decimal)_config.NetworkThresholdMBps;
            _numNetThreshold.BackColor = Color.FromArgb(49, 50, 68);
            _numNetThreshold.ForeColor = Color.White;
            _numNetThreshold.ValueChanged += delegate { AutoSaveSettings(); };
            _settingsGroup.Controls.Add(_numNetThreshold);

            Label lblDiskThresh = new Label();
            lblDiskThresh.Text = "Disk Threshold (MB/s):";
            lblDiskThresh.Location = new Point(350, 24);
            lblDiskThresh.Size = new Size(210, 20);
            lblDiskThresh.ForeColor = Color.FromArgb(205, 214, 244);
            lblDiskThresh.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            _settingsGroup.Controls.Add(lblDiskThresh);

            _numDiskThreshold = new NumericUpDown();
            _numDiskThreshold.Location = new Point(570, 22);
            _numDiskThreshold.Size = new Size(80, 24);
            _numDiskThreshold.DecimalPlaces = 1;
            _numDiskThreshold.Increment = 1.0M;
            _numDiskThreshold.Minimum = 0.5M;
            _numDiskThreshold.Maximum = 1000.0M;
            _numDiskThreshold.Value = (decimal)_config.DiskThresholdMBps;
            _numDiskThreshold.BackColor = Color.FromArgb(49, 50, 68);
            _numDiskThreshold.ForeColor = Color.White;
            _numDiskThreshold.ValueChanged += delegate { AutoSaveSettings(); };
            _settingsGroup.Controls.Add(_numDiskThreshold);

            // Row 2: Trigger Delay & Cooldown
            Label lblActivation = new Label();
            lblActivation.Text = "Trigger Delay / Peak Filter (sec):";
            lblActivation.Location = new Point(20, 54);
            lblActivation.Size = new Size(205, 20);
            lblActivation.ForeColor = Color.FromArgb(205, 214, 244);
            lblActivation.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            _settingsGroup.Controls.Add(lblActivation);

            _numActivationDelay = new NumericUpDown();
            _numActivationDelay.Location = new Point(230, 52);
            _numActivationDelay.Size = new Size(80, 24);
            _numActivationDelay.Minimum = 1;
            _numActivationDelay.Maximum = 60;
            _numActivationDelay.Value = _config.ActivationDelaySeconds;
            _numActivationDelay.BackColor = Color.FromArgb(49, 50, 68);
            _numActivationDelay.ForeColor = Color.White;
            _numActivationDelay.ValueChanged += delegate { AutoSaveSettings(); };
            _settingsGroup.Controls.Add(_numActivationDelay);

            Label lblGrace = new Label();
            lblGrace.Text = "Cooldown / Grace Period (sec):";
            lblGrace.Location = new Point(350, 54);
            lblGrace.Size = new Size(210, 20);
            lblGrace.ForeColor = Color.FromArgb(205, 214, 244);
            lblGrace.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            _settingsGroup.Controls.Add(lblGrace);

            _numGracePeriod = new NumericUpDown();
            _numGracePeriod.Location = new Point(570, 52);
            _numGracePeriod.Size = new Size(80, 24);
            _numGracePeriod.Minimum = 5;
            _numGracePeriod.Maximum = 600;
            _numGracePeriod.Value = _config.GracePeriodSeconds;
            _numGracePeriod.BackColor = Color.FromArgb(49, 50, 68);
            _numGracePeriod.ForeColor = Color.White;
            _numGracePeriod.ValueChanged += delegate { AutoSaveSettings(); };
            _settingsGroup.Controls.Add(_numGracePeriod);

            // Row 3: Checkboxes Monitor Net & Disk
            _chkMonitorNet = new CheckBox();
            _chkMonitorNet.Text = "Monitor Network (Downloads)";
            _chkMonitorNet.Location = new Point(20, 84);
            _chkMonitorNet.Size = new Size(280, 24);
            _chkMonitorNet.Checked = _config.MonitorNetwork;
            _chkMonitorNet.ForeColor = Color.FromArgb(205, 214, 244);
            _chkMonitorNet.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            _chkMonitorNet.CheckedChanged += delegate { AutoSaveSettings(); };
            _settingsGroup.Controls.Add(_chkMonitorNet);

            _chkMonitorDisk = new CheckBox();
            _chkMonitorDisk.Text = "Monitor Disk (Installations/Patching)";
            _chkMonitorDisk.Location = new Point(350, 84);
            _chkMonitorDisk.Size = new Size(320, 24);
            _chkMonitorDisk.Checked = _config.MonitorDisk;
            _chkMonitorDisk.ForeColor = Color.FromArgb(205, 214, 244);
            _chkMonitorDisk.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            _chkMonitorDisk.CheckedChanged += delegate { AutoSaveSettings(); };
            _settingsGroup.Controls.Add(_chkMonitorDisk);

            // Row 4: Keep Display On
            _chkKeepDisplay = new CheckBox();
            _chkKeepDisplay.Text = "Keep Display On (Default: Off = Energy Saving)";
            _chkKeepDisplay.Location = new Point(20, 112);
            _chkKeepDisplay.Size = new Size(480, 24);
            _chkKeepDisplay.Checked = _config.KeepDisplayOn;
            _chkKeepDisplay.ForeColor = Color.FromArgb(205, 214, 244);
            _chkKeepDisplay.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            _chkKeepDisplay.CheckedChanged += delegate { AutoSaveSettings(); };
            _settingsGroup.Controls.Add(_chkKeepDisplay);

            // Row 5: Start with Windows & Start Minimized
            _chkAutostart = new CheckBox();
            _chkAutostart.Text = "Start automatically with Windows";
            _chkAutostart.Location = new Point(20, 140);
            _chkAutostart.Size = new Size(260, 24);
            _chkAutostart.Checked = _config.StartWithWindows;
            _chkAutostart.ForeColor = Color.FromArgb(205, 214, 244);
            _chkAutostart.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            _chkAutostart.CheckedChanged += delegate { AutoSaveSettings(); };
            _settingsGroup.Controls.Add(_chkAutostart);

            _chkStartMinimized = new CheckBox();
            _chkStartMinimized.Text = "Start minimized in System Tray";
            _chkStartMinimized.Location = new Point(350, 140);
            _chkStartMinimized.Size = new Size(260, 24);
            _chkStartMinimized.Checked = _config.StartMinimized;
            _chkStartMinimized.ForeColor = Color.FromArgb(205, 214, 244);
            _chkStartMinimized.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            _chkStartMinimized.CheckedChanged += delegate { AutoSaveSettings(); };
            _settingsGroup.Controls.Add(_chkStartMinimized);

            // Row 6: Close Action Dropdown
            Label lblCloseAction = new Label();
            lblCloseAction.Text = "Action on Close (X):";
            lblCloseAction.Location = new Point(20, 185);
            lblCloseAction.Size = new Size(180, 20);
            lblCloseAction.ForeColor = Color.FromArgb(205, 214, 244);
            lblCloseAction.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            _settingsGroup.Controls.Add(lblCloseAction);

            _cmbCloseAction = new ComboBox();
            _cmbCloseAction.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbCloseAction.Location = new Point(200, 181);
            _cmbCloseAction.Size = new Size(280, 26);
            _cmbCloseAction.BackColor = Color.FromArgb(49, 50, 68);
            _cmbCloseAction.ForeColor = Color.White;
            _cmbCloseAction.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            _cmbCloseAction.Items.Add("Always prompt (Dialog)");
            _cmbCloseAction.Items.Add("Minimize to System Tray (Keep running in background)");
            _cmbCloseAction.Items.Add("Exit program completely");
            _cmbCloseAction.SelectedIndex = (int)_config.ActionOnClose;
            _cmbCloseAction.SelectedIndexChanged += delegate
            {
                if (_cmbCloseAction.SelectedIndex >= 0)
                {
                    _config.ActionOnClose = (CloseAction)_cmbCloseAction.SelectedIndex;
                    AutoSaveSettings();
                }
            };
            _settingsGroup.Controls.Add(_cmbCloseAction);

            this.Controls.Add(_settingsGroup);

            // 6. Log List Box Header
            Label lblLog = new Label();
            lblLog.Text = "Activity Log (Double-click or Ctrl+C to copy entry):";
            lblLog.Location = new Point(20, 565);
            lblLog.AutoSize = true;
            lblLog.ForeColor = Color.FromArgb(166, 173, 200);
            lblLog.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            this.Controls.Add(lblLog);

            _btnClearLog = new Button();
            _btnClearLog.Text = "Clear Log";
            _btnClearLog.Location = new Point(625, 560);
            _btnClearLog.Size = new Size(80, 22);
            _btnClearLog.BackColor = Color.FromArgb(49, 50, 68);
            _btnClearLog.ForeColor = Color.FromArgb(186, 194, 222);
            _btnClearLog.FlatStyle = FlatStyle.Flat;
            _btnClearLog.Font = new Font("Segoe UI", 7.5f);
            _btnClearLog.FlatAppearance.BorderSize = 0;
            _btnClearLog.Cursor = Cursors.Hand;
            _btnClearLog.Click += delegate
            {
                _logList.Items.Clear();
                AddLogEntry("Log cleared.");
            };
            this.Controls.Add(_btnClearLog);

            // Activity Log ListBox with Context Menu and Copy Shortcuts
            _logContextMenu = new ContextMenuStrip(this.components);
            ToolStripMenuItem copyItem = new ToolStripMenuItem("📋 Copy Selected (Ctrl+C)");
            copyItem.Click += delegate { CopySelectedLogsToClipboard(); };
            _logContextMenu.Items.Add(copyItem);

            ToolStripMenuItem copyAllItem = new ToolStripMenuItem("📋 Copy All Logs");
            copyAllItem.Click += delegate { CopyAllLogsToClipboard(); };
            _logContextMenu.Items.Add(copyAllItem);

            _logContextMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem clearItem = new ToolStripMenuItem("🗑️ Clear Log");
            clearItem.Click += delegate { _logList.Items.Clear(); AddLogEntry("Log cleared."); };
            _logContextMenu.Items.Add(clearItem);

            _logList = new ListBox();
            _logList.Location = new Point(20, 585);
            _logList.Size = new Size(685, 95);
            _logList.BackColor = Color.FromArgb(17, 17, 27);
            _logList.ForeColor = Color.FromArgb(186, 194, 222);
            _logList.Font = new Font("Consolas", 8.5f);
            _logList.BorderStyle = BorderStyle.FixedSingle;
            _logList.ContextMenuStrip = _logContextMenu;

            _logList.DoubleClick += delegate
            {
                CopySelectedLogsToClipboard();
            };

            _logList.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.C)
                {
                    CopySelectedLogsToClipboard();
                    e.Handled = true;
                }
                else if (e.Control && e.KeyCode == Keys.A)
                {
                    CopyAllLogsToClipboard();
                    e.Handled = true;
                }
            };

            this.Controls.Add(_logList);

            // 7. Action Buttons
            _btnSaveSettings = new Button();
            _btnSaveSettings.Text = "💾 Save Settings";
            _btnSaveSettings.Location = new Point(300, 695);
            _btnSaveSettings.Size = new Size(195, 36);
            _btnSaveSettings.BackColor = Color.FromArgb(137, 180, 250);
            _btnSaveSettings.ForeColor = Color.FromArgb(17, 17, 27);
            _btnSaveSettings.FlatStyle = FlatStyle.Flat;
            _btnSaveSettings.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _btnSaveSettings.Cursor = Cursors.Hand;
            _btnSaveSettings.FlatAppearance.BorderSize = 0;
            _btnSaveSettings.Click += delegate { SaveSettingsExplicitly(); };
            this.Controls.Add(_btnSaveSettings);

            _btnMinimize = new Button();
            _btnMinimize.Text = "🗕 Minimize to System Tray";
            _btnMinimize.Location = new Point(510, 695);
            _btnMinimize.Size = new Size(195, 36);
            _btnMinimize.BackColor = Color.FromArgb(49, 50, 68);
            _btnMinimize.ForeColor = Color.White;
            _btnMinimize.FlatStyle = FlatStyle.Flat;
            _btnMinimize.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _btnMinimize.Cursor = Cursors.Hand;
            _btnMinimize.FlatAppearance.BorderSize = 0;
            _btnMinimize.Click += delegate
            {
                SyncConfigFromUI();
                _config.Save();
                this.WindowState = FormWindowState.Minimized;
            };
            this.Controls.Add(_btnMinimize);

            // 8. System Tray Icon
            _trayMenu = new ContextMenuStrip(this.components);

            _trayStatusItem = new ToolStripMenuItem("Status: Monitoring...");
            _trayStatusItem.Enabled = false;
            _trayMenu.Items.Add(_trayStatusItem);

            _trayMenu.Items.Add(new ToolStripSeparator());

            _trayShowItem = new ToolStripMenuItem("Open NoSleep");
            _trayShowItem.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _trayShowItem.Click += delegate { RestoreFromTray(); };
            _trayMenu.Items.Add(_trayShowItem);

            _trayAwakeItem = new ToolStripMenuItem("Keep PC Awake");
            _trayAwakeItem.CheckOnClick = true;
            _trayAwakeItem.Checked = _config.ForceAwake;
            _trayAwakeItem.Click += delegate
            {
                if (_forceAwakeCheck.Checked != _trayAwakeItem.Checked)
                {
                    _forceAwakeCheck.Checked = _trayAwakeItem.Checked;
                }
            };
            _trayMenu.Items.Add(_trayAwakeItem);

            _trayMenu.Items.Add(new ToolStripSeparator());

            _trayExitItem = new ToolStripMenuItem("Exit");
            _trayExitItem.Click += delegate { ExitApplication(); };
            _trayMenu.Items.Add(_trayExitItem);

            _trayIcon = new NotifyIcon(this.components);
            _trayIcon.Icon = this.Icon;
            _trayIcon.Text = "NoSleep";
            _trayIcon.ContextMenuStrip = _trayMenu;
            _trayIcon.Visible = false;
            _trayIcon.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    RestoreFromTray();
                }
            };
        }

        private void CopySelectedLogsToClipboard()
        {
            if (_logList.SelectedItem != null)
            {
                string text = _logList.SelectedItem.ToString();
                if (!string.IsNullOrEmpty(text))
                {
                    try
                    {
                        Clipboard.SetText(text);
                    }
                    catch { }
                }
            }
        }

        private void CopyAllLogsToClipboard()
        {
            if (_logList.Items.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (object item in _logList.Items)
                {
                    sb.AppendLine(item != null ? item.ToString() : string.Empty);
                }
                try
                {
                    Clipboard.SetText(sb.ToString());
                }
                catch { }
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            FlushPendingLogs();
        }

        private bool _startupVisibilityHandled = false;
        private bool _restoringFromTray = false;

        protected override void SetVisibleCore(bool value)
        {
            if (!_startupVisibilityHandled)
            {
                _startupVisibilityHandled = true;
                if (value && _startMinimized && !_isExplicitExit)
                {
                    base.SetVisibleCore(false);
                    HideToTray();
                    AddLogEntry("NoSleep started minimized in system tray. Background monitoring active.");
                    return;
                }
            }
            base.SetVisibleCore(value);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (!_isExplicitExit && !_restoringFromTray && this.Visible && this.WindowState == FormWindowState.Minimized && _trayIcon != null)
            {
                HideToTray();
            }
        }

        public void HideToTray()
        {
            if (_isExplicitExit || this.IsDisposed || _trayIcon == null) return;
            this.Hide();
            _trayIcon.Visible = true;
        }

        public void RestoreFromTray()
        {
            _restoringFromTray = true;
            try
            {
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                }

                // Show first: WindowState changes only apply to the native
                // window while the form is visible. Restoring the state of a
                // hidden form leaves it natively minimized after Show().
                this.Show();

                if (this.WindowState != FormWindowState.Normal)
                {
                    this.WindowState = FormWindowState.Normal;
                }

                this.Activate();
            }
            finally
            {
                _restoringFromTray = false;
            }
        }

        private void FlushPendingLogs()
        {
            lock (_logLock)
            {
                if (_pendingLogs.Count > 0)
                {
                    foreach (string pending in _pendingLogs)
                    {
                        _logList.Items.Add(pending);
                    }
                    _pendingLogs.Clear();
                    if (_logList.Items.Count > 0)
                    {
                        _logList.SelectedIndex = _logList.Items.Count - 1;
                    }
                }
            }
        }

        private void SyncConfigFromUI()
        {
            _config.NetworkThresholdMBps = (double)_numNetThreshold.Value;
            _config.DiskThresholdMBps = (double)_numDiskThreshold.Value;
            _config.GracePeriodSeconds = (int)_numGracePeriod.Value;
            _config.ActivationDelaySeconds = (int)_numActivationDelay.Value;
            _config.MonitorNetwork = _chkMonitorNet.Checked;
            _config.MonitorDisk = _chkMonitorDisk.Checked;
            _config.KeepDisplayOn = _chkKeepDisplay.Checked;
            _config.StartWithWindows = _chkAutostart.Checked;
            _config.StartMinimized = _chkStartMinimized.Checked;
            if (_cmbCloseAction != null && _cmbCloseAction.SelectedIndex >= 0)
            {
                _config.ActionOnClose = (CloseAction)_cmbCloseAction.SelectedIndex;
            }
        }

        private void AutoSaveSettings()
        {
            SyncConfigFromUI();
            _config.Save();
        }

        private void SaveSettingsExplicitly()
        {
            SyncConfigFromUI();
            _config.Save();
            AddLogEntry(string.Format("Settings updated: Net ≥ {0:0.0} MB/s, Disk ≥ {1:0.0} MB/s, Trigger = {2}s, Cooldown = {3}s.", 
                _config.NetworkThresholdMBps, _config.DiskThresholdMBps, _config.ActivationDelaySeconds, _config.GracePeriodSeconds));
            MessageBox.Show("Settings were successfully saved and applied!", "NoSleep", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnActivityUpdated(ActivityData data)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;

            this.BeginInvoke((Action)(delegate()
            {
                _lastData = data;

                // Update Network Card
                _netValueLabel.Text = string.Format("{0:0.0} MB/s", data.NetworkDownMBps);
                _netSubValueLabel.Text = string.Format("↓ {0:0.0} MB/s   ↑ {1:0.0} MB/s", data.NetworkDownMBps, data.NetworkUpMBps);
                
                int netPercent = (int)Math.Min(100, (data.NetworkDownMBps / Math.Max(0.1, _config.NetworkThresholdMBps * 2.0)) * 100);
                _netProgressBar.Value = Math.Max(0, Math.Min(100, netPercent));

                if (data.IsNetworkActive)
                {
                    _netStatusLabel.Text = string.Format("🟢 Active (≥ {0:0.0} MB/s)", _config.NetworkThresholdMBps);
                    _netStatusLabel.ForeColor = Color.FromArgb(34, 197, 94);
                }
                else
                {
                    _netStatusLabel.Text = string.Format("⚪ Idle (< {0:0.0} MB/s)", _config.NetworkThresholdMBps);
                    _netStatusLabel.ForeColor = Color.FromArgb(148, 163, 184);
                }

                // Update Disk Card
                _diskValueLabel.Text = string.Format("{0:0.0} MB/s", data.DiskTotalMBps);
                _diskSubValueLabel.Text = string.Format("Write: {0:0.0} MB/s   Read: {0:0.0} MB/s", data.DiskWriteMBps, data.DiskReadMBps);

                int diskPercent = (int)Math.Min(100, (data.DiskTotalMBps / Math.Max(0.1, _config.DiskThresholdMBps * 2.0)) * 100);
                _diskProgressBar.Value = Math.Max(0, Math.Min(100, diskPercent));

                if (data.IsDiskActive)
                {
                    _diskStatusLabel.Text = string.Format("🟢 Active (≥ {0:0.0} MB/s)", _config.DiskThresholdMBps);
                    _diskStatusLabel.ForeColor = Color.FromArgb(34, 197, 94);
                }
                else
                {
                    _diskStatusLabel.Text = string.Format("⚪ Idle (< {0:0.0} MB/s)", _config.DiskThresholdMBps);
                    _diskStatusLabel.ForeColor = Color.FromArgb(148, 163, 184);
                }

                // Update Status Hero Banner
                if (data.IsForceAwake)
                {
                    _statusTitleLabel.Text = "🔒 SLEEP FORCIBLY BLOCKED";
                    _statusTitleLabel.ForeColor = Color.FromArgb(147, 197, 253);
                    _statusDetailLabel.Text = "'Keep PC Awake' mode is active. Standby is blocked indefinitely.";
                }
                else if (data.IsConfirmedActive)
                {
                    _statusTitleLabel.Text = "⚡ STANDBY BLOCKED (ACTIVITY CONFIRMED)";
                    _statusTitleLabel.ForeColor = Color.FromArgb(74, 222, 128);
                    _statusDetailLabel.Text = string.Format("High throughput sustained (Network: {0:0.0} MB/s, Disk: {1:0.0} MB/s).", data.NetworkDownMBps, data.DiskTotalMBps);
                }
                else if (data.IsPendingActivation)
                {
                    _statusTitleLabel.Text = string.Format("⏳ VERIFYING ACTIVITY ({0}/{1}s)", data.PendingActivationSeconds, data.ActivationDelayRequiredSeconds);
                    _statusTitleLabel.ForeColor = Color.FromArgb(56, 189, 248);
                    _statusDetailLabel.Text = string.Format("Throughput above threshold. Verifying continuous activity for {0}s...", data.ActivationDelayRequiredSeconds);
                }
                else if (data.InGracePeriod)
                {
                    _statusTitleLabel.Text = string.Format("⏳ COOLDOWN ACTIVE ({0}s)", data.GracePeriodRemainingSeconds);
                    _statusTitleLabel.ForeColor = Color.FromArgb(250, 204, 21);
                    _statusDetailLabel.Text = string.Format("Throughput dropped below threshold. Normal sleep will be allowed in {0} seconds.", data.GracePeriodRemainingSeconds);
                }
                else
                {
                    _statusTitleLabel.Text = "⚪ NORMAL SLEEP ALLOWED (IDLE)";
                    _statusTitleLabel.ForeColor = Color.FromArgb(166, 173, 200);
                    _statusDetailLabel.Text = "No elevated activity. Standard Windows power management is active.";
                }

                // Track and log state transitions
                SystemActivityState currentState;
                if (data.IsForceAwake)
                {
                    currentState = SystemActivityState.Forced;
                }
                else if (data.IsConfirmedActive)
                {
                    currentState = SystemActivityState.Active;
                }
                else if (data.IsPendingActivation)
                {
                    currentState = SystemActivityState.PendingVerification;
                }
                else if (data.InGracePeriod)
                {
                    currentState = SystemActivityState.Cooldown;
                }
                else
                {
                    currentState = SystemActivityState.Idle;
                }

                if (currentState != _lastState)
                {
                    switch (currentState)
                    {
                        case SystemActivityState.PendingVerification:
                            AddLogEntry(string.Format("Activity peak detected (Net: {0:0.0} MB/s, Disk: {1:0.0} MB/s) - Verifying for {2}s before blocking standby...", 
                                data.NetworkDownMBps, data.DiskTotalMBps, data.ActivationDelayRequiredSeconds));
                            break;

                        case SystemActivityState.Active:
                            AddLogEntry(string.Format("High activity confirmed (sustained ≥ {0}s) - Standby blocked (Net: {1:0.0} MB/s, Disk: {2:0.0} MB/s).", 
                                _config.ActivationDelaySeconds, data.NetworkDownMBps, data.DiskTotalMBps));
                            break;

                        case SystemActivityState.Cooldown:
                            AddLogEntry(string.Format("Activity dropped below threshold - Cooldown started ({0}s grace period).", _config.GracePeriodSeconds));
                            break;

                        case SystemActivityState.Idle:
                            if (_lastState == SystemActivityState.PendingVerification)
                            {
                                AddLogEntry("Activity peak ended before trigger delay - Standby was not blocked.");
                            }
                            else if (_lastState == SystemActivityState.Cooldown)
                            {
                                AddLogEntry("Cooldown expired - Normal Windows standby allowed.");
                            }
                            else if (_lastState != SystemActivityState.Initial)
                            {
                                AddLogEntry("System is idle - Normal Windows standby allowed.");
                            }
                            break;

                        case SystemActivityState.Forced:
                            AddLogEntry("'Keep PC Awake' enabled - Standby blocked indefinitely.");
                            break;
                    }

                    _lastState = currentState;
                }

                UpdateTrayStatus(data, currentState);

                _statusCard.Invalidate();
            }));
        }

        private void UpdateTrayStatus(ActivityData data, SystemActivityState state)
        {
            if (_trayIcon == null || _trayStatusItem == null) return;

            string trayStatus;
            switch (state)
            {
                case SystemActivityState.Forced:
                    trayStatus = "Sleep forcibly blocked";
                    break;
                case SystemActivityState.Active:
                    trayStatus = "Standby blocked (activity confirmed)";
                    break;
                case SystemActivityState.PendingVerification:
                    trayStatus = string.Format("Verifying activity ({0}/{1}s)", data.PendingActivationSeconds, data.ActivationDelayRequiredSeconds);
                    break;
                case SystemActivityState.Cooldown:
                    trayStatus = string.Format("Cooldown active ({0}s)", data.GracePeriodRemainingSeconds);
                    break;
                default:
                    trayStatus = "Idle - sleep allowed";
                    break;
            }

            _trayStatusItem.Text = "Status: " + trayStatus;

            string tooltip = "NoSleep - " + trayStatus + " (Net: " + data.NetworkDownMBps.ToString("0.0") + ", Disk: " + data.DiskTotalMBps.ToString("0.0") + " MB/s)";
            if (tooltip.Length > 63) tooltip = tooltip.Substring(0, 63);
            _trayIcon.Text = tooltip;
        }

        public void AddLogEntry(string message)
        {
            string entry = string.Format("[{0:HH:mm:ss}] {1}", DateTime.Now, message);

            if (this.IsDisposed) return;

            if (!this.IsHandleCreated)
            {
                lock (_logLock)
                {
                    _pendingLogs.Add(entry);
                }
                return;
            }

            this.BeginInvoke((Action)(delegate()
            {
                FlushPendingLogs();
                _logList.Items.Add(entry);
                if (_logList.Items.Count > 100)
                {
                    _logList.Items.RemoveAt(0);
                }
                _logList.SelectedIndex = _logList.Items.Count - 1;
            }));
        }

        private void DrawRoundedCard(Graphics g, Rectangle rect, Color bg, Color border, int borderWidth)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = GetRoundedRectanglePath(rect, 8))
            using (SolidBrush brush = new SolidBrush(bg))
            using (Pen pen = new Pen(border, borderWidth))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }
        }

        private GraphicsPath GetRoundedRectanglePath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            Rectangle arcRect = new Rectangle(rect.X, rect.Y, d, d);

            path.AddArc(arcRect, 180, 90);
            arcRect.X = rect.Right - d - 1;
            path.AddArc(arcRect, 270, 90);
            arcRect.Y = rect.Bottom - d - 1;
            path.AddArc(arcRect, 0, 90);
            arcRect.X = rect.Left;
            path.AddArc(arcRect, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_isExplicitExit && e.CloseReason == CloseReason.UserClosing)
            {
                SyncConfigFromUI();
                _config.Save();

                if (_config.ActionOnClose == CloseAction.MinimizeToTaskbar)
                {
                    e.Cancel = true;
                    this.WindowState = FormWindowState.Minimized;
                    return;
                }
                
                if (_config.ActionOnClose == CloseAction.ExitProgram)
                {
                    ExitApplication();
                    return;
                }

                // AskEveryTime: Show CloseConfirmDialog
                e.Cancel = true;
                using (CloseConfirmDialog dlg = new CloseConfirmDialog())
                {
                    DialogResult res = dlg.ShowDialog(this);
                    if (dlg.RememberChoice)
                    {
                        _config.ActionOnClose = dlg.SelectedAction;
                        _config.Save();
                        if (_cmbCloseAction != null)
                        {
                            _cmbCloseAction.SelectedIndex = (int)_config.ActionOnClose;
                        }
                    }

                    if (dlg.SelectedAction == CloseAction.MinimizeToTaskbar)
                    {
                        this.WindowState = FormWindowState.Minimized;
                    }
                    else if (dlg.SelectedAction == CloseAction.ExitProgram)
                    {
                        ExitApplication();
                    }
                }
            }
            else
            {
                base.OnFormClosing(e);
            }
        }

        public void ExitApplication()
        {
            _isExplicitExit = true;
            SyncConfigFromUI();
            _config.Save();
            _monitor.Stop();
            _powerManager.Reset();
            Application.Exit();
        }
    }
}

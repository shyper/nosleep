using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace NoSleep
{
    public class ProcessManagerDialog : Form
    {
        private readonly AppConfig _config;
        private readonly List<string> _workingList;

        private Panel _headerPanel;
        private Label _titleLabel;
        private Label _subtitleLabel;
        private CheckBox _chkEnable;

        private GroupBox _addCard;
        private TextBox _txtProcessName;
        private Button _btnBrowse;
        private Button _btnAddCustom;
        private ComboBox _cmbRunning;
        private Button _btnRefreshRunning;
        private Button _btnAddRunning;

        private GroupBox _listCard;
        private ListView _lvProcesses;
        private Button _btnRemove;
        private Button _btnClearAll;
        private Button _btnRefreshStatus;

        private Button _btnSave;
        private Button _btnCancel;

        public ProcessManagerDialog(AppConfig config)
        {
            _config = config;
            _workingList = new List<string>();
            if (_config.MonitoredProcesses != null)
            {
                foreach (string p in _config.MonitoredProcesses)
                {
                    if (!string.IsNullOrEmpty(p))
                    {
                        _workingList.Add(p.Trim());
                    }
                }
            }

            InitializeComponent();
            PopulateRunningProcesses();
            RefreshProcessListView();
        }

        private void InitializeComponent()
        {
            this.Text = "NoSleep - Manage Monitored Applications";
            this.Size = new Size(650, 640);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(24, 24, 37);
            this.ForeColor = Color.FromArgb(205, 214, 244);
            this.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            try
            {
                if (File.Exists("app.ico"))
                {
                    this.Icon = new Icon("app.ico");
                }
            }
            catch { }

            // 1. Header Panel
            _headerPanel = new Panel();
            _headerPanel.Location = new Point(16, 14);
            _headerPanel.Size = new Size(602, 60);
            _headerPanel.BackColor = Color.FromArgb(30, 30, 46);
            _headerPanel.Paint += delegate(object s, PaintEventArgs e)
            {
                DrawRoundedCard(e.Graphics, _headerPanel.ClientRectangle, Color.FromArgb(30, 30, 46), Color.FromArgb(69, 71, 90), 1);
            };

            _titleLabel = new Label();
            _titleLabel.Text = "⚙️ Monitored Applications & Processes";
            _titleLabel.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            _titleLabel.ForeColor = Color.FromArgb(245, 194, 231);
            _titleLabel.Location = new Point(12, 10);
            _titleLabel.AutoSize = true;
            _titleLabel.BackColor = Color.Transparent;
            _headerPanel.Controls.Add(_titleLabel);

            _subtitleLabel = new Label();
            _subtitleLabel.Text = "Standby is automatically blocked whenever any listed application is running.";
            _subtitleLabel.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            _subtitleLabel.ForeColor = Color.FromArgb(166, 173, 200);
            _subtitleLabel.Location = new Point(13, 33);
            _subtitleLabel.AutoSize = true;
            _subtitleLabel.BackColor = Color.Transparent;
            _headerPanel.Controls.Add(_subtitleLabel);

            _chkEnable = new CheckBox();
            _chkEnable.Text = "Enable Monitoring";
            _chkEnable.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _chkEnable.ForeColor = Color.FromArgb(137, 180, 250);
            _chkEnable.Location = new Point(445, 18);
            _chkEnable.Size = new Size(145, 24);
            _chkEnable.Checked = _config.MonitorProcesses;
            _chkEnable.BackColor = Color.Transparent;
            _headerPanel.Controls.Add(_chkEnable);

            this.Controls.Add(_headerPanel);

            // 2. Add Application Group
            _addCard = new GroupBox();
            _addCard.Text = " Add Application to Watchlist ";
            _addCard.Location = new Point(16, 82);
            _addCard.Size = new Size(602, 140);
            _addCard.ForeColor = Color.FromArgb(245, 194, 231);
            _addCard.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _addCard.BackColor = Color.Transparent;

            // Row 1: Manual name / Browse
            Label lblCustom = new Label();
            lblCustom.Text = "Name or executable (e.g. putty.exe, obs64.exe):";
            lblCustom.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblCustom.ForeColor = Color.FromArgb(205, 214, 244);
            lblCustom.Location = new Point(15, 22);
            lblCustom.AutoSize = true;
            _addCard.Controls.Add(lblCustom);

            _txtProcessName = new TextBox();
            _txtProcessName.Location = new Point(16, 44);
            _txtProcessName.Size = new Size(235, 24);
            _txtProcessName.BackColor = Color.FromArgb(49, 50, 68);
            _txtProcessName.ForeColor = Color.White;
            _txtProcessName.Font = new Font("Segoe UI", 9f);
            _txtProcessName.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    AddCustomProcess();
                    e.SuppressKeyPress = true;
                }
            };
            _addCard.Controls.Add(_txtProcessName);

            _btnBrowse = new Button();
            _btnBrowse.Text = "📂 Browse...";
            _btnBrowse.Location = new Point(258, 43);
            _btnBrowse.Size = new Size(95, 26);
            _btnBrowse.BackColor = Color.FromArgb(49, 50, 68);
            _btnBrowse.ForeColor = Color.FromArgb(205, 214, 244);
            _btnBrowse.FlatStyle = FlatStyle.Flat;
            _btnBrowse.Font = new Font("Segoe UI", 8.5f);
            _btnBrowse.FlatAppearance.BorderSize = 0;
            _btnBrowse.Cursor = Cursors.Hand;
            _btnBrowse.Click += delegate { BrowseForExecutable(); };
            _addCard.Controls.Add(_btnBrowse);

            _btnAddCustom = new Button();
            _btnAddCustom.Text = "➕ Add";
            _btnAddCustom.Location = new Point(358, 43);
            _btnAddCustom.Size = new Size(75, 26);
            _btnAddCustom.BackColor = Color.FromArgb(137, 180, 250);
            _btnAddCustom.ForeColor = Color.FromArgb(17, 17, 27);
            _btnAddCustom.FlatStyle = FlatStyle.Flat;
            _btnAddCustom.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _btnAddCustom.FlatAppearance.BorderSize = 0;
            _btnAddCustom.Cursor = Cursors.Hand;
            _btnAddCustom.Click += delegate { AddCustomProcess(); };
            _addCard.Controls.Add(_btnAddCustom);

            // Row 2: Select Running Process
            Label lblRunning = new Label();
            lblRunning.Text = "Select from currently running processes:";
            lblRunning.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblRunning.ForeColor = Color.FromArgb(205, 214, 244);
            lblRunning.Location = new Point(15, 80);
            lblRunning.AutoSize = true;
            _addCard.Controls.Add(lblRunning);

            _cmbRunning = new ComboBox();
            _cmbRunning.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbRunning.Location = new Point(16, 102);
            _cmbRunning.Size = new Size(337, 24);
            _cmbRunning.BackColor = Color.FromArgb(49, 50, 68);
            _cmbRunning.ForeColor = Color.White;
            _cmbRunning.Font = new Font("Segoe UI", 9f);
            _addCard.Controls.Add(_cmbRunning);

            _btnRefreshRunning = new Button();
            _btnRefreshRunning.Text = "🔄";
            _btnRefreshRunning.Location = new Point(358, 101);
            _btnRefreshRunning.Size = new Size(32, 26);
            _btnRefreshRunning.BackColor = Color.FromArgb(49, 50, 68);
            _btnRefreshRunning.ForeColor = Color.White;
            _btnRefreshRunning.FlatStyle = FlatStyle.Flat;
            _btnRefreshRunning.Font = new Font("Segoe UI", 9f);
            _btnRefreshRunning.FlatAppearance.BorderSize = 0;
            _btnRefreshRunning.Cursor = Cursors.Hand;
            _btnRefreshRunning.Click += delegate { PopulateRunningProcesses(); };
            _addCard.Controls.Add(_btnRefreshRunning);

            _btnAddRunning = new Button();
            _btnAddRunning.Text = "➕ Add Process";
            _btnAddRunning.Location = new Point(395, 101);
            _btnAddRunning.Size = new Size(115, 26);
            _btnAddRunning.BackColor = Color.FromArgb(166, 227, 161);
            _btnAddRunning.ForeColor = Color.FromArgb(17, 17, 27);
            _btnAddRunning.FlatStyle = FlatStyle.Flat;
            _btnAddRunning.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _btnAddRunning.FlatAppearance.BorderSize = 0;
            _btnAddRunning.Cursor = Cursors.Hand;
            _btnAddRunning.Click += delegate { AddRunningProcess(); };
            _addCard.Controls.Add(_btnAddRunning);

            this.Controls.Add(_addCard);

            // 3. Monitored List Group
            _listCard = new GroupBox();
            _listCard.Text = " Configured Watchlist ";
            _listCard.Location = new Point(16, 230);
            _listCard.Size = new Size(602, 300);
            _listCard.ForeColor = Color.FromArgb(245, 194, 231);
            _listCard.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _listCard.BackColor = Color.Transparent;

            _lvProcesses = new ListView();
            _lvProcesses.Location = new Point(16, 25);
            _lvProcesses.Size = new Size(570, 220);
            _lvProcesses.View = View.Details;
            _lvProcesses.FullRowSelect = true;
            _lvProcesses.MultiSelect = true;
            _lvProcesses.HideSelection = false;
            _lvProcesses.BackColor = Color.FromArgb(17, 17, 27);
            _lvProcesses.ForeColor = Color.FromArgb(205, 214, 244);
            _lvProcesses.Font = new Font("Segoe UI", 9.5f);
            _lvProcesses.BorderStyle = BorderStyle.FixedSingle;
            _lvProcesses.HeaderStyle = ColumnHeaderStyle.Nonclickable;

            _lvProcesses.Columns.Add("Application / Process", 340);
            _lvProcesses.Columns.Add("Live Status", 200);

            _lvProcesses.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Delete)
                {
                    RemoveSelected();
                }
            };

            _listCard.Controls.Add(_lvProcesses);

            _btnRemove = new Button();
            _btnRemove.Text = "🗑️ Remove Selected";
            _btnRemove.Location = new Point(16, 255);
            _btnRemove.Size = new Size(150, 32);
            _btnRemove.BackColor = Color.FromArgb(49, 50, 68);
            _btnRemove.ForeColor = Color.FromArgb(243, 139, 168);
            _btnRemove.FlatStyle = FlatStyle.Flat;
            _btnRemove.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _btnRemove.FlatAppearance.BorderSize = 0;
            _btnRemove.Cursor = Cursors.Hand;
            _btnRemove.Click += delegate { RemoveSelected(); };
            _listCard.Controls.Add(_btnRemove);

            _btnRefreshStatus = new Button();
            _btnRefreshStatus.Text = "🔄 Refresh Status";
            _btnRefreshStatus.Location = new Point(175, 255);
            _btnRefreshStatus.Size = new Size(130, 32);
            _btnRefreshStatus.BackColor = Color.FromArgb(49, 50, 68);
            _btnRefreshStatus.ForeColor = Color.FromArgb(186, 194, 222);
            _btnRefreshStatus.FlatStyle = FlatStyle.Flat;
            _btnRefreshStatus.Font = new Font("Segoe UI", 8.5f);
            _btnRefreshStatus.FlatAppearance.BorderSize = 0;
            _btnRefreshStatus.Cursor = Cursors.Hand;
            _btnRefreshStatus.Click += delegate { RefreshProcessListView(); };
            _listCard.Controls.Add(_btnRefreshStatus);

            _btnClearAll = new Button();
            _btnClearAll.Text = "Clear All";
            _btnClearAll.Location = new Point(486, 255);
            _btnClearAll.Size = new Size(100, 32);
            _btnClearAll.BackColor = Color.FromArgb(49, 50, 68);
            _btnClearAll.ForeColor = Color.FromArgb(166, 173, 200);
            _btnClearAll.FlatStyle = FlatStyle.Flat;
            _btnClearAll.Font = new Font("Segoe UI", 8.5f);
            _btnClearAll.FlatAppearance.BorderSize = 0;
            _btnClearAll.Cursor = Cursors.Hand;
            _btnClearAll.Click += delegate
            {
                if (_workingList.Count > 0 && MessageBox.Show("Are you sure you want to remove all monitored applications?", "Confirm Clear", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _workingList.Clear();
                    RefreshProcessListView();
                }
            };
            _listCard.Controls.Add(_btnClearAll);

            this.Controls.Add(_listCard);

            // 4. Footer Buttons
            _btnSave = new Button();
            _btnSave.Text = "💾 Save";
            _btnSave.Location = new Point(360, 545);
            _btnSave.Size = new Size(140, 38);
            _btnSave.BackColor = Color.FromArgb(137, 180, 250);
            _btnSave.ForeColor = Color.FromArgb(17, 17, 27);
            _btnSave.FlatStyle = FlatStyle.Flat;
            _btnSave.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Cursor = Cursors.Hand;
            _btnSave.Click += delegate
            {
                SaveAndClose();
            };
            this.Controls.Add(_btnSave);

            _btnCancel = new Button();
            _btnCancel.Text = "Cancel";
            _btnCancel.Location = new Point(510, 545);
            _btnCancel.Size = new Size(108, 38);
            _btnCancel.BackColor = Color.FromArgb(49, 50, 68);
            _btnCancel.ForeColor = Color.FromArgb(166, 173, 200);
            _btnCancel.FlatStyle = FlatStyle.Flat;
            _btnCancel.Font = new Font("Segoe UI", 9.5f);
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Cursor = Cursors.Hand;
            _btnCancel.Click += delegate
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            this.Controls.Add(_btnCancel);

            this.AcceptButton = _btnSave;
            this.CancelButton = _btnCancel;
        }

        private void PopulateRunningProcesses()
        {
            try
            {
                _cmbRunning.Items.Clear();
                Process[] processes = Process.GetProcesses();
                List<string> names = new List<string>();

                try
                {
                    for (int i = 0; i < processes.Length; i++)
                    {
                        try
                        {
                            string pName = processes[i].ProcessName;
                            if (!string.IsNullOrEmpty(pName) && !ContainsIgnoreCase(names, pName))
                            {
                                names.Add(pName);
                            }
                        }
                        catch { }
                    }
                }
                finally
                {
                    for (int i = 0; i < processes.Length; i++)
                    {
                        try { processes[i].Dispose(); } catch { }
                    }
                }

                names.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (string n in names)
                {
                    _cmbRunning.Items.Add(n + ".exe");
                }

                if (_cmbRunning.Items.Count > 0)
                {
                    _cmbRunning.SelectedIndex = 0;
                }
            }
            catch { }
        }

        private void RefreshProcessListView()
        {
            _lvProcesses.Items.Clear();

            HashSet<string> runningSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                Process[] processes = Process.GetProcesses();
                try
                {
                    for (int i = 0; i < processes.Length; i++)
                    {
                        try
                        {
                            runningSet.Add(processes[i].ProcessName);
                        }
                        catch { }
                    }
                }
                finally
                {
                    for (int i = 0; i < processes.Length; i++)
                    {
                        try { processes[i].Dispose(); } catch { }
                    }
                }
            }
            catch { }

            foreach (string p in _workingList)
            {
                if (string.IsNullOrEmpty(p)) continue;
                string clean = p.Trim();
                string lookupName = clean;
                if (lookupName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    lookupName = lookupName.Substring(0, lookupName.Length - 4);
                }

                bool isRunning = runningSet.Contains(lookupName);

                ListViewItem item = new ListViewItem(clean);
                item.UseItemStyleForSubItems = false;

                ListViewItem.ListViewSubItem statusSub = new ListViewItem.ListViewSubItem();
                if (isRunning)
                {
                    statusSub.Text = "🟢 Running (Active)";
                    statusSub.ForeColor = Color.FromArgb(74, 222, 128);
                    item.ForeColor = Color.FromArgb(245, 194, 231);
                }
                else
                {
                    statusSub.Text = "⚪ Not Running";
                    statusSub.ForeColor = Color.FromArgb(148, 163, 184);
                    item.ForeColor = Color.FromArgb(205, 214, 244);
                }

                item.SubItems.Add(statusSub);
                _lvProcesses.Items.Add(item);
            }
        }

        private void AddCustomProcess()
        {
            string text = _txtProcessName.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            string exeName = Path.GetFileName(text);
            if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                exeName += ".exe";
            }

            if (!ContainsIgnoreCase(_workingList, exeName))
            {
                _workingList.Add(exeName);
                _txtProcessName.Clear();
                RefreshProcessListView();
            }
            else
            {
                MessageBox.Show(string.Format("'{0}' is already in your monitored list.", exeName), "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void AddRunningProcess()
        {
            if (_cmbRunning.SelectedItem == null) return;
            string selected = _cmbRunning.SelectedItem.ToString().Trim();
            if (string.IsNullOrEmpty(selected)) return;

            if (!ContainsIgnoreCase(_workingList, selected))
            {
                _workingList.Add(selected);
                RefreshProcessListView();
            }
            else
            {
                MessageBox.Show(string.Format("'{0}' is already in your monitored list.", selected), "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static bool ContainsIgnoreCase(List<string> list, string value)
        {
            if (list == null || string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private void BrowseForExecutable()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select Executable to Monitor";
                ofd.Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*";
                ofd.CheckFileExists = true;
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    string fileName = Path.GetFileName(ofd.FileName);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        _txtProcessName.Text = fileName;
                        AddCustomProcess();
                    }
                }
            }
        }

        private void RemoveSelected()
        {
            if (_lvProcesses.SelectedItems.Count == 0) return;

            List<string> toRemove = new List<string>();
            foreach (ListViewItem item in _lvProcesses.SelectedItems)
            {
                toRemove.Add(item.Text);
            }

            foreach (string r in toRemove)
            {
                _workingList.RemoveAll(delegate(string x) { return string.Equals(x, r, StringComparison.OrdinalIgnoreCase); });
            }

            RefreshProcessListView();
        }

        private void SaveAndClose()
        {
            _config.MonitorProcesses = _chkEnable.Checked;
            _config.MonitoredProcesses = new List<string>(_workingList);
            _config.Save();

            this.DialogResult = DialogResult.OK;
            this.Close();
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

        private static GraphicsPath GetRoundedRectanglePath(Rectangle rect, int radius)
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
    }
}

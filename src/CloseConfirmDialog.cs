using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace NoSleep
{
    public class CloseConfirmDialog : Form
    {
        public CloseAction SelectedAction { get; private set; }
        public bool RememberChoice { get; private set; }

        private Panel _contentCard;
        private Label _titleLabel;
        private Label _descLabel;
        private Button _btnTaskbar;
        private Button _btnExit;
        private Button _btnCancel;
        private CheckBox _chkRemember;

        public CloseConfirmDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "NoSleep - Close Action";
            this.Size = new Size(500, 260);
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

            _contentCard = new Panel();
            _contentCard.Location = new Point(16, 16);
            _contentCard.Size = new Size(452, 105);
            _contentCard.BackColor = Color.FromArgb(30, 30, 46);
            _contentCard.Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = GetRoundedRectanglePath(_contentCard.ClientRectangle, 6))
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(30, 30, 46)))
                using (Pen borderPen = new Pen(Color.FromArgb(69, 71, 90), 1))
                {
                    e.Graphics.FillPath(bgBrush, path);
                    e.Graphics.DrawPath(borderPen, path);
                }
            };

            _titleLabel = new Label();
            _titleLabel.Text = "⚡ How would you like to close NoSleep?";
            _titleLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _titleLabel.ForeColor = Color.FromArgb(245, 194, 231);
            _titleLabel.Location = new Point(14, 12);
            _titleLabel.AutoSize = true;
            _titleLabel.BackColor = Color.Transparent;
            _contentCard.Controls.Add(_titleLabel);

            _descLabel = new Label();
            _descLabel.Text = "You can minimize NoSleep to your System Tray to keep monitoring downloads in the background, or exit the program completely.";
            _descLabel.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            _descLabel.ForeColor = Color.FromArgb(186, 194, 222);
            _descLabel.Location = new Point(15, 38);
            _descLabel.Size = new Size(420, 55);
            _descLabel.BackColor = Color.Transparent;
            _contentCard.Controls.Add(_descLabel);

            this.Controls.Add(_contentCard);

            // Remember choice checkbox
            _chkRemember = new CheckBox();
            _chkRemember.Text = "Remember this choice (can be changed anytime in Settings)";
            _chkRemember.Location = new Point(20, 130);
            _chkRemember.Size = new Size(440, 22);
            _chkRemember.ForeColor = Color.FromArgb(166, 173, 200);
            _chkRemember.Font = new Font("Segoe UI", 8.5f);
            this.Controls.Add(_chkRemember);

            // Button 1: Minimize to System Tray
            _btnTaskbar = new Button();
            _btnTaskbar.Text = "🗕 Minimize to Tray";
            _btnTaskbar.Location = new Point(16, 165);
            _btnTaskbar.Size = new Size(200, 36);
            _btnTaskbar.BackColor = Color.FromArgb(137, 180, 250);
            _btnTaskbar.ForeColor = Color.FromArgb(17, 17, 27);
            _btnTaskbar.FlatStyle = FlatStyle.Flat;
            _btnTaskbar.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _btnTaskbar.Cursor = Cursors.Hand;
            _btnTaskbar.FlatAppearance.BorderSize = 0;
            _btnTaskbar.Click += delegate
            {
                SelectedAction = CloseAction.MinimizeToTaskbar;
                RememberChoice = _chkRemember.Checked;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            this.Controls.Add(_btnTaskbar);

            // Button 2: Exit Program
            _btnExit = new Button();
            _btnExit.Text = "❌ Exit Program";
            _btnExit.Location = new Point(226, 165);
            _btnExit.Size = new Size(150, 36);
            _btnExit.BackColor = Color.FromArgb(235, 111, 146);
            _btnExit.ForeColor = Color.FromArgb(17, 17, 27);
            _btnExit.FlatStyle = FlatStyle.Flat;
            _btnExit.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _btnExit.Cursor = Cursors.Hand;
            _btnExit.FlatAppearance.BorderSize = 0;
            _btnExit.Click += delegate
            {
                SelectedAction = CloseAction.ExitProgram;
                RememberChoice = _chkRemember.Checked;
                this.DialogResult = DialogResult.Yes;
                this.Close();
            };
            this.Controls.Add(_btnExit);

            // Button 3: Cancel
            _btnCancel = new Button();
            _btnCancel.Text = "Cancel";
            _btnCancel.Location = new Point(386, 165);
            _btnCancel.Size = new Size(82, 36);
            _btnCancel.BackColor = Color.FromArgb(49, 50, 68);
            _btnCancel.ForeColor = Color.FromArgb(166, 173, 200);
            _btnCancel.FlatStyle = FlatStyle.Flat;
            _btnCancel.Font = new Font("Segoe UI", 9f);
            _btnCancel.Cursor = Cursors.Hand;
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += delegate
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            this.Controls.Add(_btnCancel);

            this.AcceptButton = _btnTaskbar;
            this.CancelButton = _btnCancel;
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

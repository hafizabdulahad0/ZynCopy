using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ZynCopy
{
    public class MainForm : Form
    {
        public const string WindowTitle = "ZynCopy - Ultra-Fast Cloud & Local File Transfer";

        private PictureBox _picLogo;
        private TextBox _txtSource;
        private Button _btnClearQueue;
        private ComboBox _cmbDest;
        private ComboBox _cmbSpeed;
        private CheckBox _chkBundleArchive;
        private Button _btnCopy;
        private Button _btnMove;
        private Button _btnPurge;
        private Button _btnCancel;

        // Metric Dashboard Cards
        private Label _lblSpeedValue;
        private Label _lblEtaValue;
        private Label _lblProgressValue;
        private Label _lblActiveFile;
        private ProgressBar _progressBar;
        private Label _lblPercent;

        // Collapsible Log Box
        private Button _btnToggleLog;
        private TextBox _txtLog;
        private bool _isLogExpanded = false;

        private CheckBox _chkContextMenu;
        private CopyEngine _engine;
        private readonly List<string> _queuedPaths = new List<string>();

        public MainForm(string[] initialArgs = null)
        {
            _engine = new CopyEngine();
            _engine.ProgressReceived += OnEngineProgressReceived;

            InitializeComponent();

            // Set window icon
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(iconPath))
            {
                try { this.Icon = new Icon(iconPath); } catch { }
            }

            if (initialArgs != null && initialArgs.Length > 0)
            {
                AddFilesToQueue(initialArgs);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == (int)Program.WM_COPYDATA)
            {
                try
                {
                    Program.COPYDATASTRUCT cds = (Program.COPYDATASTRUCT)Marshal.PtrToStructure(m.LParam, typeof(Program.COPYDATASTRUCT));
                    string payload = Marshal.PtrToStringUni(cds.lpData);
                    if (!string.IsNullOrEmpty(payload))
                    {
                        string[] files = payload.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                        AddFilesToQueue(files);
                    }
                    m.Result = (IntPtr)1;
                    return;
                }
                catch { }
            }
            base.WndProc(ref m);
        }

        private void InitializeComponent()
        {
            this.Text = WindowTitle;
            this.Size = new Size(680, 545);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(15, 23, 42); // Slate 900
            this.ForeColor = Color.White;
            this.AllowDrop = true;

            Font fontBrand = new Font("Segoe UI", 14, FontStyle.Bold);
            Font fontSub = new Font("Segoe UI", 8.5f);
            Font fontBold = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            Font fontRegular = new Font("Segoe UI", 9.5f);
            Font fontCardValue = new Font("Segoe UI", 15, FontStyle.Bold);
            Font fontCardLabel = new Font("Segoe UI", 8, FontStyle.Regular);
            Font fontConsole = new Font("Consolas", 8.5f);

            // ================= HEADER =================
            _picLogo = new PictureBox();
            _picLogo.Location = new Point(20, 15);
            _picLogo.Size = new Size(38, 38);
            _picLogo.SizeMode = PictureBoxSizeMode.Zoom;
            string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png");
            if (File.Exists(logoPath))
            {
                try { _picLogo.Image = Image.FromFile(logoPath); } catch { }
            }
            this.Controls.Add(_picLogo);

            Label lblTitle = new Label();
            lblTitle.Text = "ZynCopy";
            lblTitle.Font = fontBrand;
            lblTitle.ForeColor = Color.FromArgb(56, 189, 248); // Electric Sky
            lblTitle.Location = new Point(65, 12);
            lblTitle.Size = new Size(130, 26);
            this.Controls.Add(lblTitle);

            Label lblSub = new Label();
            lblSub.Text = "Single-Window Multi-Stream Engine | By ZynOps Solutions";
            lblSub.Font = fontSub;
            lblSub.ForeColor = Color.FromArgb(148, 163, 184);
            lblSub.Location = new Point(67, 36);
            lblSub.Size = new Size(360, 18);
            this.Controls.Add(lblSub);

            _chkContextMenu = new CheckBox();
            _chkContextMenu.Text = "Explorer Right-Click Menu";
            _chkContextMenu.Location = new Point(480, 18);
            _chkContextMenu.Size = new Size(180, 22);
            _chkContextMenu.Font = fontSub;
            _chkContextMenu.ForeColor = Color.FromArgb(203, 213, 225);
            _chkContextMenu.Checked = ShellIntegrator.IsRegistered();
            _chkContextMenu.CheckedChanged += OnContextMenuCheckedChanged;
            this.Controls.Add(_chkContextMenu);

            // ================= INPUT SECTION =================
            Label lblSrc = new Label();
            lblSrc.Text = "Source Files or Folders (Multi-File Drag & Drop Enabled):";
            lblSrc.Font = fontBold;
            lblSrc.Location = new Point(20, 68);
            lblSrc.Size = new Size(420, 18);
            this.Controls.Add(lblSrc);

            _txtSource = new TextBox();
            _txtSource.Location = new Point(20, 88);
            _txtSource.Size = new Size(435, 26);
            _txtSource.BackColor = Color.FromArgb(30, 41, 59);
            _txtSource.ForeColor = Color.White;
            _txtSource.BorderStyle = BorderStyle.FixedSingle;
            _txtSource.Font = fontRegular;
            _txtSource.AllowDrop = true;
            this.Controls.Add(_txtSource);

            Button btnBrowseSrc = new Button();
            btnBrowseSrc.Text = "Add...";
            btnBrowseSrc.Location = new Point(462, 87);
            btnBrowseSrc.Size = new Size(88, 28);
            btnBrowseSrc.BackColor = Color.FromArgb(51, 65, 85);
            btnBrowseSrc.FlatStyle = FlatStyle.Flat;
            btnBrowseSrc.FlatAppearance.BorderSize = 0;
            btnBrowseSrc.Font = fontRegular;
            btnBrowseSrc.Click += OnBrowseSourceClick;
            this.Controls.Add(btnBrowseSrc);

            _btnClearQueue = new Button();
            _btnClearQueue.Text = "Clear";
            _btnClearQueue.Location = new Point(557, 87);
            _btnClearQueue.Size = new Size(88, 28);
            _btnClearQueue.BackColor = Color.FromArgb(71, 85, 105);
            _btnClearQueue.FlatStyle = FlatStyle.Flat;
            _btnClearQueue.FlatAppearance.BorderSize = 0;
            _btnClearQueue.Font = fontRegular;
            _btnClearQueue.Click += OnClearQueueClick;
            this.Controls.Add(_btnClearQueue);

            Label lblDst = new Label();
            lblDst.Text = "Target Destination Drive or Folder:";
            lblDst.Font = fontBold;
            lblDst.Location = new Point(20, 122);
            lblDst.Size = new Size(300, 18);
            this.Controls.Add(lblDst);

            _cmbDest = new ComboBox();
            _cmbDest.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbDest.Location = new Point(20, 142);
            _cmbDest.Size = new Size(360, 26);
            _cmbDest.BackColor = Color.FromArgb(30, 41, 59);
            _cmbDest.ForeColor = Color.White;
            _cmbDest.Font = fontRegular;
            PopulateDrives();
            this.Controls.Add(_cmbDest);

            Button btnBrowseDst = new Button();
            btnBrowseDst.Text = "Browse...";
            btnBrowseDst.Location = new Point(388, 141);
            btnBrowseDst.Size = new Size(80, 28);
            btnBrowseDst.BackColor = Color.FromArgb(51, 65, 85);
            btnBrowseDst.FlatStyle = FlatStyle.Flat;
            btnBrowseDst.FlatAppearance.BorderSize = 0;
            btnBrowseDst.Font = fontRegular;
            btnBrowseDst.Click += OnBrowseDestClick;
            this.Controls.Add(btnBrowseDst);

            Label lblSpeed = new Label();
            lblSpeed.Text = "Stream Mode:";
            lblSpeed.Font = fontBold;
            lblSpeed.Location = new Point(480, 122);
            lblSpeed.Size = new Size(160, 18);
            this.Controls.Add(lblSpeed);

            _cmbSpeed = new ComboBox();
            _cmbSpeed.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbSpeed.Location = new Point(480, 142);
            _cmbSpeed.Size = new Size(165, 26);
            _cmbSpeed.BackColor = Color.FromArgb(30, 41, 59);
            _cmbSpeed.ForeColor = Color.White;
            _cmbSpeed.Font = fontRegular;
            _cmbSpeed.Items.Add("🌪️ Hyper (32x Streams)");
            _cmbSpeed.Items.Add("🚀 Turbo (16x Streams)");
            _cmbSpeed.Items.Add("⚡ Eco (8x Streams)");
            _cmbSpeed.SelectedIndex = 0;
            this.Controls.Add(_cmbSpeed);

            // ================= ARCHIVE BUNDLER TOGGLE =================
            _chkBundleArchive = new CheckBox();
            _chkBundleArchive.Text = "📦 Bundle Folder as Single Cloud Archive (.zip) - Maximum Speed & Zero API Lag";
            _chkBundleArchive.Location = new Point(22, 175);
            _chkBundleArchive.Size = new Size(620, 22);
            _chkBundleArchive.Font = fontSub;
            _chkBundleArchive.ForeColor = Color.FromArgb(56, 189, 248);
            _chkBundleArchive.Checked = false;
            this.Controls.Add(_chkBundleArchive);

            // ================= ACTION BUTTONS =================
            _btnCopy = new Button();
            _btnCopy.Text = "🚀 Turbo Copy";
            _btnCopy.Location = new Point(20, 202);
            _btnCopy.Size = new Size(150, 36);
            _btnCopy.BackColor = Color.FromArgb(14, 165, 233); // Sky Blue
            _btnCopy.ForeColor = Color.White;
            _btnCopy.FlatStyle = FlatStyle.Flat;
            _btnCopy.FlatAppearance.BorderSize = 0;
            _btnCopy.Font = fontBold;
            _btnCopy.Click += OnCopyClick;
            this.Controls.Add(_btnCopy);

            _btnMove = new Button();
            _btnMove.Text = "📦 Turbo Move";
            _btnMove.Location = new Point(180, 202);
            _btnMove.Size = new Size(150, 36);
            _btnMove.BackColor = Color.FromArgb(16, 185, 129); // Emerald
            _btnMove.ForeColor = Color.White;
            _btnMove.FlatStyle = FlatStyle.Flat;
            _btnMove.FlatAppearance.BorderSize = 0;
            _btnMove.Font = fontBold;
            _btnMove.Click += OnMoveClick;
            this.Controls.Add(_btnMove);

            _btnPurge = new Button();
            _btnPurge.Text = "🗑️ Instant Purge";
            _btnPurge.Location = new Point(340, 202);
            _btnPurge.Size = new Size(140, 36);
            _btnPurge.BackColor = Color.FromArgb(239, 68, 68); // Red
            _btnPurge.ForeColor = Color.White;
            _btnPurge.FlatStyle = FlatStyle.Flat;
            _btnPurge.FlatAppearance.BorderSize = 0;
            _btnPurge.Font = fontBold;
            _btnPurge.Click += OnPurgeClick;
            this.Controls.Add(_btnPurge);

            _btnCancel = new Button();
            _btnCancel.Text = "⏹ Cancel";
            _btnCancel.Location = new Point(490, 202);
            _btnCancel.Size = new Size(155, 36);
            _btnCancel.BackColor = Color.FromArgb(71, 85, 105);
            _btnCancel.ForeColor = Color.White;
            _btnCancel.FlatStyle = FlatStyle.Flat;
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Font = fontBold;
            _btnCancel.Enabled = false;
            _btnCancel.Click += OnCancelClick;
            this.Controls.Add(_btnCancel);

            // ================= DASHBOARD METRICS HUD =================
            Panel pnlSpeed = CreateCard(20, 248, 200, 70);
            Label lblSpeedTitle = new Label { Text = "⚡ TRANSFER SPEED", Location = new Point(12, 10), Size = new Size(170, 15), Font = fontCardLabel, ForeColor = Color.FromArgb(148, 163, 184) };
            _lblSpeedValue = new Label { Text = "0.0 MB/s", Location = new Point(10, 26), Size = new Size(180, 32), Font = fontCardValue, ForeColor = Color.FromArgb(56, 189, 248) };
            pnlSpeed.Controls.Add(lblSpeedTitle);
            pnlSpeed.Controls.Add(_lblSpeedValue);
            this.Controls.Add(pnlSpeed);

            Panel pnlEta = CreateCard(230, 248, 200, 70);
            Label lblEtaTitle = new Label { Text = "⏱️ ESTIMATED TIME LEFT", Location = new Point(12, 10), Size = new Size(170, 15), Font = fontCardLabel, ForeColor = Color.FromArgb(148, 163, 184) };
            _lblEtaValue = new Label { Text = "--", Location = new Point(10, 26), Size = new Size(180, 32), Font = fontCardValue, ForeColor = Color.FromArgb(52, 211, 153) };
            pnlEta.Controls.Add(lblEtaTitle);
            pnlEta.Controls.Add(_lblEtaValue);
            this.Controls.Add(pnlEta);

            Panel pnlProg = CreateCard(440, 248, 205, 70);
            Label lblProgTitle = new Label { Text = "📊 QUEUE / PROGRESS", Location = new Point(12, 10), Size = new Size(170, 15), Font = fontCardLabel, ForeColor = Color.FromArgb(148, 163, 184) };
            _lblProgressValue = new Label { Text = "Idle", Location = new Point(10, 26), Size = new Size(185, 32), Font = new Font("Segoe UI", 11.5f, FontStyle.Bold), ForeColor = Color.FromArgb(244, 244, 245) };
            pnlProg.Controls.Add(lblProgTitle);
            pnlProg.Controls.Add(_lblProgressValue);
            this.Controls.Add(pnlProg);

            // Progress Bar & Percentage
            _progressBar = new ProgressBar();
            _progressBar.Location = new Point(20, 330);
            _progressBar.Size = new Size(575, 14);
            _progressBar.Style = ProgressBarStyle.Continuous;
            this.Controls.Add(_progressBar);

            _lblPercent = new Label();
            _lblPercent.Text = "0%";
            _lblPercent.Location = new Point(600, 328);
            _lblPercent.Size = new Size(50, 18);
            _lblPercent.Font = fontBold;
            _lblPercent.ForeColor = Color.FromArgb(56, 189, 248);
            this.Controls.Add(_lblPercent);

            // Active File Label
            _lblActiveFile = new Label();
            _lblActiveFile.Text = "Ready. Select files, check Archive Mode if transferring big folders to cloud.";
            _lblActiveFile.Location = new Point(22, 352);
            _lblActiveFile.Size = new Size(420, 20);
            _lblActiveFile.Font = fontSub;
            _lblActiveFile.ForeColor = Color.FromArgb(203, 213, 225);
            this.Controls.Add(_lblActiveFile);

            // Toggle Log Button
            _btnToggleLog = new Button();
            _btnToggleLog.Text = "📋 Show Technical Stream Log";
            _btnToggleLog.Location = new Point(445, 350);
            _btnToggleLog.Size = new Size(200, 24);
            _btnToggleLog.BackColor = Color.FromArgb(30, 41, 59);
            _btnToggleLog.FlatStyle = FlatStyle.Flat;
            _btnToggleLog.FlatAppearance.BorderSize = 0;
            _btnToggleLog.Font = fontSub;
            _btnToggleLog.Click += OnToggleLogClick;
            this.Controls.Add(_btnToggleLog);

            // Collapsible Log Box
            _txtLog = new TextBox();
            _txtLog.Location = new Point(20, 382);
            _txtLog.Size = new Size(625, 105);
            _txtLog.Multiline = true;
            _txtLog.ReadOnly = true;
            _txtLog.ScrollBars = ScrollBars.Vertical;
            _txtLog.BackColor = Color.FromArgb(2, 6, 23);
            _txtLog.ForeColor = Color.FromArgb(52, 211, 153);
            _txtLog.Font = fontConsole;
            _txtLog.BorderStyle = BorderStyle.FixedSingle;
            _txtLog.Visible = false;
            this.Controls.Add(_txtLog);

            // Drag and Drop Events
            this.DragEnter += OnDragEnter;
            this.DragDrop += OnDragDrop;
            _txtSource.DragEnter += OnDragEnter;
            _txtSource.DragDrop += OnDragDrop;
        }

        public void AddFilesToQueue(IEnumerable<string> paths)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<IEnumerable<string>>(AddFilesToQueue), paths);
                return;
            }

            if (paths == null) return;

            int addedCount = 0;
            foreach (string p in paths)
            {
                if (string.IsNullOrEmpty(p)) continue;
                string clean = p.Trim('"', ' ', '\t', '\r', '\n');
                if (clean.StartsWith("--")) continue;

                if (File.Exists(clean) || Directory.Exists(clean))
                {
                    if (!_queuedPaths.Contains(clean))
                    {
                        _queuedPaths.Add(clean);
                        addedCount++;
                    }
                }
            }

            UpdateQueueDisplay();
        }

        private void UpdateQueueDisplay()
        {
            if (_queuedPaths.Count == 0)
            {
                _txtSource.Text = "";
                _lblActiveFile.Text = "Ready. Drag & drop files or choose source and destination.";
                _lblProgressValue.Text = "Idle";
            }
            else if (_queuedPaths.Count == 1)
            {
                _txtSource.Text = _queuedPaths[0];
                _lblActiveFile.Text = "Ready: " + Path.GetFileName(_queuedPaths[0]);
                _lblProgressValue.Text = "1 item queued";
            }
            else
            {
                string first = Path.GetFileName(_queuedPaths[0]);
                string second = Path.GetFileName(_queuedPaths[1]);
                _txtSource.Text = string.Format("[{0} items queued] - {1}, {2}...", _queuedPaths.Count, first, second);
                _lblActiveFile.Text = string.Format("Queue ready: {0} files/folders queued in this window.", _queuedPaths.Count);
                _lblProgressValue.Text = string.Format("{0} items queued", _queuedPaths.Count);
            }
        }

        private void OnClearQueueClick(object sender, EventArgs e)
        {
            _queuedPaths.Clear();
            UpdateQueueDisplay();
        }

        private Panel CreateCard(int x, int y, int w, int h)
        {
            Panel p = new Panel();
            p.Location = new Point(x, y);
            p.Size = new Size(w, h);
            p.BackColor = Color.FromArgb(30, 41, 59);
            return p;
        }

        private void OnToggleLogClick(object sender, EventArgs e)
        {
            _isLogExpanded = !_isLogExpanded;
            _txtLog.Visible = _isLogExpanded;
            _btnToggleLog.Text = _isLogExpanded ? "▲ Hide Technical Stream Log" : "📋 Show Technical Stream Log";
            this.Size = _isLogExpanded ? new Size(680, 545) : new Size(680, 435);
        }

        private void PopulateDrives()
        {
            _cmbDest.Items.Clear();
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                if (d.IsReady)
                {
                    string label = string.IsNullOrEmpty(d.VolumeLabel) ? d.DriveType.ToString() : d.VolumeLabel;
                    double freeGb = Math.Round(d.TotalFreeSpace / (1024.0 * 1024 * 1024), 1);
                    _cmbDest.Items.Add(string.Format("{0} ({1} - {2} GB Free)", d.RootDirectory.FullName, label, freeGb));
                }
            }
            if (_cmbDest.Items.Count > 0)
            {
                _cmbDest.SelectedIndex = 0;
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                AddFilesToQueue(files);
            }
        }

        private void OnBrowseSourceClick(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Multiselect = true;
                ofd.Title = "Select Files to Transfer (Multi-Select Enabled)";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    AddFilesToQueue(ofd.FileNames);
                    return;
                }
            }

            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Or Select a Folder to Transfer";
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    AddFilesToQueue(new string[] { fbd.SelectedPath });
                }
            }
        }

        private void OnBrowseDestClick(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select Target Destination Folder";
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    _cmbDest.Items.Insert(0, fbd.SelectedPath);
                    _cmbDest.SelectedIndex = 0;
                }
            }
        }

        private void OnContextMenuCheckedChanged(object sender, EventArgs e)
        {
            if (_chkContextMenu.Checked)
            {
                ShellIntegrator.Register();
                _lblActiveFile.Text = "Right-click context menu enabled.";
            }
            else
            {
                ShellIntegrator.Unregister();
                _lblActiveFile.Text = "Right-click context menu disabled.";
            }
        }

        private StreamSpeed GetSelectedSpeed()
        {
            if (_cmbSpeed.SelectedIndex == 0) return StreamSpeed.Hyper16x; // 32 streams
            if (_cmbSpeed.SelectedIndex == 2) return StreamSpeed.Eco4x;    // 8 streams
            return StreamSpeed.Turbo8x;                                    // 16 streams
        }

        private string GetCleanDestinationPath()
        {
            string selected = _cmbDest.SelectedItem as string;
            if (string.IsNullOrEmpty(selected)) return null;

            int parenIndex = selected.IndexOf(" (");
            if (parenIndex > 0)
            {
                return selected.Substring(0, parenIndex).Trim();
            }
            return selected.Trim();
        }

        private void OnCopyClick(object sender, EventArgs e)
        {
            StartTransfer(TransferMode.Copy);
        }

        private void OnMoveClick(object sender, EventArgs e)
        {
            StartTransfer(TransferMode.Move);
        }

        private void OnPurgeClick(object sender, EventArgs e)
        {
            List<string> targets = GetActiveTargets();
            if (targets.Count == 0)
            {
                MessageBox.Show("Please select or queue at least one valid source to delete!", "Target Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string preview = string.Join("\n", targets.ToArray());
            if (preview.Length > 200) preview = preview.Substring(0, 200) + "...";

            DialogResult res = MessageBox.Show(string.Format("Are you sure you want to permanently fast-purge {0} item(s):\n\n{1}\n\nThis bypasses the Recycle Bin for maximum speed and cannot be undone!", targets.Count, preview),
                "Confirm Instant Purge", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (res == DialogResult.Yes)
            {
                SetUiBusy(true);
                _lblSpeedValue.Text = "Purging...";
                _lblEtaValue.Text = "Fast";
                _lblProgressValue.Text = "Instant Clean";
                _engine.ExecuteQueue(targets, null, TransferMode.Purge, GetSelectedSpeed(), false);
            }
        }

        private List<string> GetActiveTargets()
        {
            List<string> list = new List<string>(_queuedPaths);
            if (list.Count == 0)
            {
                string raw = _txtSource.Text.Trim();
                if (!string.IsNullOrEmpty(raw) && (File.Exists(raw) || Directory.Exists(raw)))
                {
                    list.Add(raw);
                }
            }
            return list;
        }

        private void StartTransfer(TransferMode mode)
        {
            List<string> targets = GetActiveTargets();
            if (targets.Count == 0)
            {
                MessageBox.Show("Please select or queue at least one source file or folder first!", "Source Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string dest = GetCleanDestinationPath();
            if (string.IsNullOrEmpty(dest))
            {
                MessageBox.Show("Please select a valid destination target!", "Destination Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetUiBusy(true);
            _lblSpeedValue.Text = "Connecting...";
            _lblEtaValue.Text = "Calculating...";
            _lblProgressValue.Text = string.Format("0 / {0} items", targets.Count);
            _lblActiveFile.Text = string.Format("Connecting pipeline for {0} queued item(s)...", targets.Count);
            _engine.ExecuteQueue(targets, dest, mode, GetSelectedSpeed(), _chkBundleArchive.Checked);
        }

        private void OnCancelClick(object sender, EventArgs e)
        {
            _engine.Cancel();
            _lblActiveFile.Text = "Cancelling transfer...";
        }

        private void SetUiBusy(bool busy)
        {
            _btnCopy.Enabled = !busy;
            _btnMove.Enabled = !busy;
            _btnPurge.Enabled = !busy;
            _btnClearQueue.Enabled = !busy;
            _chkBundleArchive.Enabled = !busy;
            _btnCancel.Enabled = busy;
            if (busy)
            {
                _progressBar.Value = 0;
            }
        }

        private void OnEngineProgressReceived(object sender, TransferProgressEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<object, TransferProgressEventArgs>(OnEngineProgressReceived), sender, e);
                return;
            }

            if (!string.IsNullOrEmpty(e.Speed))
            {
                _lblSpeedValue.Text = e.Speed;
            }

            if (!string.IsNullOrEmpty(e.Eta))
            {
                _lblEtaValue.Text = e.Eta;
            }

            if (!string.IsNullOrEmpty(e.TransferredStats))
            {
                _lblProgressValue.Text = e.TransferredStats;
            }

            if (e.Percentage >= 0 && e.Percentage <= 100)
            {
                _progressBar.Value = e.Percentage;
                _lblPercent.Text = e.Percentage + "%";
            }

            if (!string.IsNullOrEmpty(e.ActiveFile))
            {
                _lblActiveFile.Text = e.ActiveFile;
            }

            if (!string.IsNullOrEmpty(e.Message))
            {
                AppendLog(e.Message);
            }

            if (e.IsCompleted)
            {
                SetUiBusy(false);
                _lblSpeedValue.Text = "0.0 MB/s";
                _lblEtaValue.Text = "Done";
                _lblActiveFile.Text = "Transfer completed successfully!";
                _progressBar.Value = 100;
                _lblPercent.Text = "100%";
                _queuedPaths.Clear();
                UpdateQueueDisplay();
                PopulateDrives();
            }
        }

        private void AppendLog(string text)
        {
            if (_txtLog.TextLength > 30000)
            {
                _txtLog.Text = _txtLog.Text.Substring(15000);
            }
            _txtLog.AppendText(text + Environment.NewLine);
        }
    }
}

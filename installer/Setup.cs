using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ZynCopySetup
{
    public class SetupForm : Form
    {
        private TextBox _txtInstallDir;
        private CheckBox _chkDesktopShortcut;
        private CheckBox _chkContextMenu;
        private Button _btnInstall;
        private ProgressBar _progressBar;
        private Label _lblStatus;

        public SetupForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "ZynCopy Setup - Fast File Transfer";
            this.Size = new Size(500, 360);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(22, 24, 29);
            this.ForeColor = Color.White;

            Font fontTitle = new Font("Segoe UI", 13, FontStyle.Bold);
            Font fontSub = new Font("Segoe UI", 9.5f);
            Font fontBold = new Font("Segoe UI", 9.5f, FontStyle.Bold);

            Label lblTitle = new Label();
            lblTitle.Text = "⚡ Install ZynCopy";
            lblTitle.Font = fontTitle;
            lblTitle.ForeColor = Color.FromArgb(56, 189, 248);
            lblTitle.Location = new Point(25, 20);
            lblTitle.Size = new Size(300, 28);
            this.Controls.Add(lblTitle);

            Label lblDesc = new Label();
            lblDesc.Text = "Install ZynCopy multi-threaded file streaming engine with zero disk caching.";
            lblDesc.Font = fontSub;
            lblDesc.ForeColor = Color.FromArgb(148, 163, 184);
            lblDesc.Location = new Point(27, 50);
            lblDesc.Size = new Size(440, 35);
            this.Controls.Add(lblDesc);

            Label lblPath = new Label();
            lblPath.Text = "Installation Folder:";
            lblPath.Font = fontBold;
            lblPath.Location = new Point(25, 95);
            lblPath.Size = new Size(200, 20);
            this.Controls.Add(lblPath);

            string defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\ZynCopy");
            _txtInstallDir = new TextBox();
            _txtInstallDir.Text = defaultDir;
            _txtInstallDir.Location = new Point(25, 120);
            _txtInstallDir.Size = new Size(340, 25);
            _txtInstallDir.BackColor = Color.FromArgb(30, 41, 59);
            _txtInstallDir.ForeColor = Color.White;
            _txtInstallDir.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(_txtInstallDir);

            Button btnBrowse = new Button();
            btnBrowse.Text = "Browse...";
            btnBrowse.Location = new Point(375, 119);
            btnBrowse.Size = new Size(90, 27);
            btnBrowse.BackColor = Color.FromArgb(51, 65, 85);
            btnBrowse.FlatStyle = FlatStyle.Flat;
            btnBrowse.FlatAppearance.BorderSize = 0;
            btnBrowse.Click += delegate
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        _txtInstallDir.Text = Path.Combine(fbd.SelectedPath, "ZynCopy");
                    }
                }
            };
            this.Controls.Add(btnBrowse);

            _chkDesktopShortcut = new CheckBox();
            _chkDesktopShortcut.Text = "Create Desktop Shortcut";
            _chkDesktopShortcut.Location = new Point(27, 160);
            _chkDesktopShortcut.Size = new Size(300, 22);
            _chkDesktopShortcut.Checked = true;
            this.Controls.Add(_chkDesktopShortcut);

            _chkContextMenu = new CheckBox();
            _chkContextMenu.Text = "Add 'Copy with ZynCopy' to File Explorer right-click menu";
            _chkContextMenu.Location = new Point(27, 185);
            _chkContextMenu.Size = new Size(420, 22);
            _chkContextMenu.Checked = true;
            this.Controls.Add(_chkContextMenu);

            _progressBar = new ProgressBar();
            _progressBar.Location = new Point(25, 225);
            _progressBar.Size = new Size(440, 10);
            this.Controls.Add(_progressBar);

            _lblStatus = new Label();
            _lblStatus.Text = "Ready to install.";
            _lblStatus.Location = new Point(27, 240);
            _lblStatus.Size = new Size(440, 20);
            _lblStatus.ForeColor = Color.FromArgb(148, 163, 184);
            this.Controls.Add(_lblStatus);

            _btnInstall = new Button();
            _btnInstall.Text = "Install ZynCopy";
            _btnInstall.Location = new Point(325, 270);
            _btnInstall.Size = new Size(140, 36);
            _btnInstall.BackColor = Color.FromArgb(14, 165, 233);
            _btnInstall.ForeColor = Color.White;
            _btnInstall.FlatStyle = FlatStyle.Flat;
            _btnInstall.FlatAppearance.BorderSize = 0;
            _btnInstall.Font = fontBold;
            _btnInstall.Click += OnInstallClick;
            this.Controls.Add(_btnInstall);
        }

        private void OnInstallClick(object sender, EventArgs e)
        {
            _btnInstall.Enabled = false;
            _progressBar.Style = ProgressBarStyle.Marquee;
            _lblStatus.Text = "Installing ZynCopy binaries...";

            string targetDir = _txtInstallDir.Text.Trim();
            try
            {
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                string sourceExe = Path.Combine(currentDir, "ZynCopy.exe");
                string destExe = Path.Combine(targetDir, "ZynCopy.exe");

                if (File.Exists(sourceExe))
                {
                    File.Copy(sourceExe, destExe, true);
                }
                else
                {
                    // If running standalone setup, check sibling or parent bin
                    string altSource = Path.Combine(currentDir, @"..\bin\ZynCopy.exe");
                    if (File.Exists(altSource))
                    {
                        File.Copy(altSource, destExe, true);
                    }
                }

                // Copy uninstaller
                string uninstallerExe = Path.Combine(targetDir, "Uninstall.exe");
                string currentSetup = Process.GetCurrentProcess().MainModule.FileName;
                File.Copy(currentSetup, uninstallerExe, true);

                // Desktop shortcut
                if (_chkDesktopShortcut.Checked)
                {
                    CreateShortcut(destExe, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ZynCopy.lnk"));
                }

                // Start Menu shortcut
                string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "ZynCopy.lnk");
                CreateShortcut(destExe, startMenu);

                // Context menu
                if (_chkContextMenu.Checked)
                {
                    RegisterContextMenu(destExe);
                }

                // Register in Windows Add/Remove Programs
                RegisterUninstaller(targetDir, destExe, uninstallerExe);

                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.Value = 100;
                _lblStatus.Text = "Installation complete!";
                _lblStatus.ForeColor = Color.FromArgb(52, 211, 153);

                DialogResult res = MessageBox.Show("ZynCopy installed successfully!\n\nWould you like to launch ZynCopy now?", "Installation Success", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (res == DialogResult.Yes)
                {
                    Process.Start(destExe);
                }
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Installation Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnInstall.Enabled = true;
                _progressBar.Style = ProgressBarStyle.Continuous;
            }
        }

        private void CreateShortcut(string targetPath, string shortcutPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Description = "ZynCopy - Ultra-Fast Cloud & Local File Transfer";
                shortcut.Save();
            }
            catch { }
        }

        private void RegisterContextMenu(string exePath)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\*\shell\ZynCopy"))
                {
                    key.SetValue("", "Copy with ZynCopy");
                    key.SetValue("Icon", "\"" + exePath + "\",0");
                    using (RegistryKey cmd = key.CreateSubKey("command"))
                    {
                        cmd.SetValue("", "\"" + exePath + "\" \"%1\"");
                    }
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Directory\shell\ZynCopy"))
                {
                    key.SetValue("", "Copy with ZynCopy");
                    key.SetValue("Icon", "\"" + exePath + "\",0");
                    using (RegistryKey cmd = key.CreateSubKey("command"))
                    {
                        cmd.SetValue("", "\"" + exePath + "\" \"%1\"");
                    }
                }
            }
            catch { }
        }

        private void RegisterUninstaller(string installDir, string exePath, string uninstallerPath)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\ZynCopy"))
                {
                    key.SetValue("DisplayName", "ZynCopy");
                    key.SetValue("DisplayVersion", "1.0.0");
                    key.SetValue("Publisher", "ZynOps Solutions");
                    key.SetValue("DisplayIcon", exePath);
                    key.SetValue("InstallLocation", installDir);
                    key.SetValue("UninstallString", "\"" + uninstallerPath + "\" --uninstall");
                }
            }
            catch { }
        }
    }

    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args != null && args.Length > 0 && args[0] == "--uninstall")
            {
                DoUninstall();
                return;
            }

            Application.Run(new SetupForm());
        }

        static void DoUninstall()
        {
            DialogResult res = MessageBox.Show("Are you sure you want to uninstall ZynCopy?", "Uninstall ZynCopy", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res != DialogResult.Yes) return;

            try
            {
                // Remove shortcuts
                string desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ZynCopy.lnk");
                if (File.Exists(desktop)) File.Delete(desktop);

                string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "ZynCopy.lnk");
                if (File.Exists(startMenu)) File.Delete(startMenu);

                // Remove context menu
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\*\shell\ZynCopy", false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\shell\ZynCopy", false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\Background\shell\ZynCopy", false);

                // Remove uninstall entry
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\ZynCopy", false);

                MessageBox.Show("ZynCopy has been successfully removed.", "Uninstall Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Uninstall error: " + ex.Message);
            }
        }
    }
}

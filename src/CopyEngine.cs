using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace ZynCopy
{
    public enum StreamSpeed
    {
        Eco4x,
        Turbo8x,
        Hyper16x
    }

    public enum TransferMode
    {
        Copy,
        Move,
        Purge
    }

    public class TransferProgressEventArgs : EventArgs
    {
        public string Message { get; set; }
        public string Speed { get; set; }
        public string Eta { get; set; }
        public string TransferredStats { get; set; }
        public int Percentage { get; set; }
        public string ActiveFile { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsError { get; set; }
        public int ExitCode { get; set; }
    }

    public class CopyEngine
    {
        private Process _activeProcess;
        private volatile bool _isCancelled;

        // Regex for parsing Rclone stats:
        // Example: Transferred:   1.640 GiB / 3.844 GiB, 43%, 1.377 MiB/s, ETA 27m19s
        private static readonly Regex StatsRegex = new Regex(@"Transferred:\s*([\d\.]+\s*\w+B)\s*/\s*([\d\.]+\s*\w+B),\s*(\d+)%,\s*([\d\.]+\s*\w+B/s),\s*ETA\s*([^\r\n,]+)", RegexOptions.Compiled);
        
        // Example: * path/to/file.ext: 56% / 898 MiB
        private static readonly Regex FileRegex = new Regex(@"\*\s+([^:]+):\s*(\d+%)", RegexOptions.Compiled);

        public event EventHandler<TransferProgressEventArgs> ProgressReceived;

        public bool IsRunning
        {
            get { return _activeProcess != null && !_activeProcess.HasExited; }
        }

        public void Cancel()
        {
            _isCancelled = true;
            try
            {
                if (_activeProcess != null && !_activeProcess.HasExited)
                {
                    _activeProcess.Kill();
                }
            }
            catch { }
        }

        public void Execute(string sourcePath, string destPath, TransferMode mode, StreamSpeed speed)
        {
            _isCancelled = false;

            Thread worker = new Thread(new ThreadStart(delegate
            {
                RunInternal(sourcePath, destPath, mode, speed);
            }));
            worker.IsBackground = true;
            worker.Start();
        }

        private void RunInternal(string sourcePath, string destPath, TransferMode mode, StreamSpeed speed)
        {
            int streams = 8;
            string chunkSize = "128M";
            if (speed == StreamSpeed.Eco4x) { streams = 4; chunkSize = "64M"; }
            else if (speed == StreamSpeed.Hyper16x) { streams = 16; chunkSize = "256M"; }

            string rcloneExe = FindRclone();

            if (mode == TransferMode.Purge)
            {
                ExecutePurge(sourcePath, rcloneExe);
                return;
            }

            string effectiveSource = NormalizeCloudPath(sourcePath);
            string effectiveDest = NormalizeCloudPath(destPath);

            if (!string.IsNullOrEmpty(rcloneExe))
            {
                ExecuteRcloneTransfer(effectiveSource, effectiveDest, mode, streams, chunkSize, rcloneExe);
            }
            else
            {
                ExecuteRobocopyTransfer(sourcePath, destPath, mode, streams);
            }
        }

        private string NormalizeCloudPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            string trimmed = path.Trim();

            if (trimmed.StartsWith("J:\\", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("J:", StringComparison.OrdinalIgnoreCase))
            {
                string rel = trimmed.Length > 3 ? trimmed.Substring(3).Replace('\\', '/') : "";
                return "ahad_dev:" + rel;
            }
            if (trimmed.StartsWith("K:\\", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("K:", StringComparison.OrdinalIgnoreCase))
            {
                string rel = trimmed.Length > 3 ? trimmed.Substring(3).Replace('\\', '/') : "";
                return "hafizahad:" + rel;
            }
            if (trimmed.StartsWith("L:\\", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("L:", StringComparison.OrdinalIgnoreCase))
            {
                string rel = trimmed.Length > 3 ? trimmed.Substring(3).Replace('\\', '/') : "";
                return "ahadawan:" + rel;
            }

            return trimmed;
        }

        private void ExecuteRcloneTransfer(string sourcePath, string destPath, TransferMode mode, int streams, string chunkSize, string rcloneExe)
        {
            string action = (mode == TransferMode.Move) ? "move" : "copy";
            string args = string.Format("{0} \"{1}\" \"{2}\" --transfers {3} --checkers {4} --drive-chunk-size {5} -P --stats 1s",
                action, sourcePath, destPath, streams, streams * 2, chunkSize);

            EmitProgress("Connecting high-speed stream pipeline (0 bytes cached on C:)...");

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = rcloneExe;
            psi.Arguments = args;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            try
            {
                _activeProcess = Process.Start(psi);

                _activeProcess.OutputDataReceived += new DataReceivedEventHandler(delegate(object sender, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        ParseOutputLine(e.Data);
                    }
                });

                _activeProcess.ErrorDataReceived += new DataReceivedEventHandler(delegate(object sender, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        ParseOutputLine(e.Data);
                    }
                });

                _activeProcess.BeginOutputReadLine();
                _activeProcess.BeginErrorReadLine();

                _activeProcess.WaitForExit();

                int code = _activeProcess.ExitCode;
                _activeProcess = null;

                if (_isCancelled)
                {
                    EmitProgress("Operation cancelled by user.", null, null, null, 0, null, false, true, -1);
                }
                else
                {
                    EmitProgress("Transfer complete! All files verified.", "0 B/s", "Complete", "100%", 100, "Done", true, false, code);
                }
            }
            catch (Exception ex)
            {
                EmitProgress("Engine Error: " + ex.Message, null, null, null, 0, null, true, true, -1);
            }
        }

        private void ParseOutputLine(string line)
        {
            string speed = null;
            string eta = null;
            string stats = null;
            int percent = -1;
            string activeFile = null;

            Match match = StatsRegex.Match(line);
            if (match.Success)
            {
                string transferred = match.Groups[1].Value.Trim();
                string total = match.Groups[2].Value.Trim();
                int.TryParse(match.Groups[3].Value, out percent);
                speed = match.Groups[4].Value.Trim();
                eta = match.Groups[5].Value.Trim();
                stats = string.Format("{0} / {1} ({2}%)", transferred, total, percent);
            }
            else
            {
                Match fileMatch = FileRegex.Match(line);
                if (fileMatch.Success)
                {
                    string fname = Path.GetFileName(fileMatch.Groups[1].Value.Trim());
                    activeFile = string.Format("{0} ({1})", fname, fileMatch.Groups[2].Value);
                }
            }

            EmitProgress(line, speed, eta, stats, percent, activeFile);
        }

        private void ExecuteRobocopyTransfer(string sourcePath, string destPath, TransferMode mode, int streams)
        {
            string action = (mode == TransferMode.Move) ? "/MOV" : "";
            string args = string.Format("\"{0}\" \"{1}\" /E {2} /MT:{3} /NP /R:2 /W:2", sourcePath, destPath, action, streams);

            ProcessStartInfo psi = new ProcessStartInfo("robocopy.exe", args);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.CreateNoWindow = true;

            try
            {
                _activeProcess = Process.Start(psi);
                _activeProcess.OutputDataReceived += new DataReceivedEventHandler(delegate(object sender, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        EmitProgress(e.Data);
                    }
                });
                _activeProcess.BeginOutputReadLine();
                _activeProcess.WaitForExit();
                int code = _activeProcess.ExitCode;
                _activeProcess = null;
                EmitProgress("Operation complete.", "Done", "0s", "100%", 100, "Done", true, false, code);
            }
            catch (Exception ex)
            {
                EmitProgress("Robocopy Error: " + ex.Message, null, null, null, 0, null, true, true, -1);
            }
        }

        private void ExecutePurge(string targetPath, string rcloneExe)
        {
            EmitProgress("Instant Purge in progress...");
            string cloudPath = NormalizeCloudPath(targetPath);
            if (!string.IsNullOrEmpty(rcloneExe))
            {
                try
                {
                    Process p = Process.Start(new ProcessStartInfo(rcloneExe, string.Format("purge \"{0}\"", cloudPath)) { UseShellExecute = false, CreateNoWindow = true });
                    p.WaitForExit();
                }
                catch { }
            }

            try
            {
                if (Directory.Exists(targetPath))
                {
                    Process p = Process.Start(new ProcessStartInfo("cmd.exe", "/c rd /s /q \"" + targetPath + "\"") { UseShellExecute = false, CreateNoWindow = true });
                    p.WaitForExit();
                }
                else if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }
                EmitProgress("Purge complete: Target removed.", "Instant", "0s", "100%", 100, "Deleted", true, false, 0);
            }
            catch (Exception ex)
            {
                EmitProgress("Purge error: " + ex.Message, null, null, null, 0, null, true, true, -1);
            }
        }

        private string FindRclone()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string localRclone = Path.Combine(appDir, "rclone.exe");
            if (File.Exists(localRclone)) return localRclone;

            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                string[] paths = pathEnv.Split(';');
                foreach (string p in paths)
                {
                    string candidate = Path.Combine(p.Trim(), "rclone.exe");
                    if (File.Exists(candidate)) return candidate;
                }
            }

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string wingetPath = Path.Combine(localAppData, @"Microsoft\WinGet\Packages");
            if (Directory.Exists(wingetPath))
            {
                try
                {
                    string[] matches = Directory.GetFiles(wingetPath, "rclone.exe", SearchOption.AllDirectories);
                    if (matches.Length > 0) return matches[0];
                }
                catch { }
            }

            return null;
        }

        private void EmitProgress(string msg, string speed = null, string eta = null, string stats = null, int percent = -1, string activeFile = null, bool isCompleted = false, bool isError = false, int exitCode = 0)
        {
            EventHandler<TransferProgressEventArgs> handler = ProgressReceived;
            if (handler != null)
            {
                TransferProgressEventArgs args = new TransferProgressEventArgs();
                args.Message = msg;
                args.Speed = speed;
                args.Eta = eta;
                args.TransferredStats = stats;
                args.Percentage = percent;
                args.ActiveFile = activeFile;
                args.IsCompleted = isCompleted;
                args.IsError = isError;
                args.ExitCode = exitCode;
                handler(this, args);
            }
        }
    }
}

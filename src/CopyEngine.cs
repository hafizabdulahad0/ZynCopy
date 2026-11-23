using System;
using System.Collections.Generic;
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
        public int CurrentItemIndex { get; set; }
        public int TotalItems { get; set; }
    }

    public class CopyEngine
    {
        private Process _activeProcess;
        private volatile bool _isCancelled;

        private static readonly Regex StatsRegex = new Regex(@"Transferred:\s*([\d\.]+\s*\w+B)\s*/\s*([\d\.]+\s*\w+B),\s*(\d+)%,\s*([\d\.]+\s*\w+B/s),\s*ETA\s*([^\r\n,]+)", RegexOptions.Compiled);
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

        public void ExecuteQueue(List<string> sourcePaths, string destFolder, TransferMode mode, StreamSpeed speed, bool bundleAsArchive = false)
        {
            _isCancelled = false;

            Thread worker = new Thread(new ThreadStart(delegate
            {
                RunQueueInternal(sourcePaths, destFolder, mode, speed, bundleAsArchive);
            }));
            worker.IsBackground = true;
            worker.Start();
        }

        private void RunQueueInternal(List<string> sourcePaths, string destFolder, TransferMode mode, StreamSpeed speed, bool bundleAsArchive)
        {
            if (sourcePaths == null || sourcePaths.Count == 0) return;

            int totalItems = sourcePaths.Count;
            string rcloneExe = FindRclone();

            int streams = 16;
            string chunkSize = "128M";
            if (speed == StreamSpeed.Eco4x) { streams = 8; chunkSize = "64M"; }
            else if (speed == StreamSpeed.Hyper16x) { streams = 32; chunkSize = "256M"; }

            List<string> tempZipsToClean = new List<string>();

            for (int i = 0; i < totalItems; i++)
            {
                if (_isCancelled) break;

                string src = sourcePaths[i];
                if (string.IsNullOrEmpty(src)) continue;

                string actualSourceToTransfer = src;
                string baseName = Path.GetFileName(src.TrimEnd('\\', '/'));
                if (string.IsNullOrEmpty(baseName)) baseName = "item_" + i;

                // Handle Archive Bundle Mode for folders
                if (bundleAsArchive && Directory.Exists(src) && mode != TransferMode.Purge)
                {
                    EmitProgress(new TransferProgressEventArgs
                    {
                        Message = string.Format(">>> Packing folder into high-speed archive: {0}.zip", baseName),
                        ActiveFile = string.Format("Packing: {0}.zip (Native Compression)", baseName),
                        Speed = "Packing...",
                        Eta = "Fast",
                        CurrentItemIndex = i,
                        TotalItems = totalItems,
                        Percentage = (int)((i / (double)totalItems) * 100)
                    });

                    string parentDir = Directory.GetParent(src) != null ? Directory.GetParent(src).FullName : Path.GetTempPath();
                    string tempZip = Path.Combine(parentDir, baseName + ".zip");

                    bool zipSuccess = CreateZipArchive(src, tempZip);
                    if (zipSuccess && File.Exists(tempZip))
                    {
                        actualSourceToTransfer = tempZip;
                        baseName = baseName + ".zip";
                        tempZipsToClean.Add(tempZip);
                    }
                }

                string itemTarget = !string.IsNullOrEmpty(destFolder) ? Path.Combine(destFolder, baseName) : null;

                EmitProgress(new TransferProgressEventArgs
                {
                    Message = string.Format(">>> Starting [{0}/{1}]: {2}", i + 1, totalItems, baseName),
                    ActiveFile = string.Format("[{0}/{1}] {2}", i + 1, totalItems, baseName),
                    CurrentItemIndex = i,
                    TotalItems = totalItems,
                    Percentage = (int)((i / (double)totalItems) * 100)
                });

                if (mode == TransferMode.Purge)
                {
                    ExecutePurge(src, rcloneExe);
                }
                else
                {
                    string effectiveSource = NormalizeCloudPath(actualSourceToTransfer);
                    string effectiveDest = NormalizeCloudPath(itemTarget);

                    if (!string.IsNullOrEmpty(rcloneExe))
                    {
                        ExecuteRcloneTransfer(actualSourceToTransfer, effectiveSource, effectiveDest, mode, streams, chunkSize, rcloneExe, i, totalItems);
                    }
                    else
                    {
                        ExecuteRobocopyTransfer(actualSourceToTransfer, itemTarget, mode, streams, i, totalItems);
                    }
                }
            }

            // Clean up temporary local archives if any were generated
            foreach (string tmp in tempZipsToClean)
            {
                try
                {
                    if (File.Exists(tmp)) File.Delete(tmp);
                }
                catch { }
            }

            if (!_isCancelled)
            {
                EmitProgress(new TransferProgressEventArgs
                {
                    Message = string.Format(">>> Batch completed successfully! All {0} items transferred.", totalItems),
                    ActiveFile = string.Format("All {0} items transferred successfully!", totalItems),
                    Speed = "0.0 MB/s",
                    Eta = "Complete",
                    Percentage = 100,
                    IsCompleted = true
                });
            }
            else
            {
                EmitProgress(new TransferProgressEventArgs
                {
                    Message = ">>> Transfer cancelled by user.",
                    ActiveFile = "Transfer cancelled",
                    Speed = "0.0 MB/s",
                    Eta = "Stopped",
                    IsCompleted = true
                });
            }
        }

        private bool CreateZipArchive(string folderPath, string zipPath)
        {
            try
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);

                string parentDir = Path.GetDirectoryName(folderPath);
                string folderName = Path.GetFileName(folderPath.TrimEnd('\\', '/'));

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "tar.exe";
                psi.Arguments = string.Format("-a -cf \"{0}\" -C \"{1}\" \"{2}\"", zipPath, parentDir, folderName);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;

                using (Process p = Process.Start(psi))
                {
                    if (p != null)
                    {
                        p.WaitForExit();
                        return p.ExitCode == 0 && File.Exists(zipPath);
                    }
                }
            }
            catch { }
            return false;
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

        private void ExecuteRcloneTransfer(string rawSource, string sourcePath, string destPath, TransferMode mode, int streams, string chunkSize, string rcloneExe, int itemIndex, int totalItems)
        {
            bool isFile = File.Exists(rawSource);
            string action;
            if (mode == TransferMode.Move)
            {
                action = isFile ? "moveto" : "move";
            }
            else
            {
                action = isFile ? "copyto" : "copy";
            }

            string cloudAccel = "";
            if (sourcePath.Contains(":") || destPath.Contains(":"))
            {
                cloudAccel = "--drive-pacer-min-sleep 10ms --drive-pacer-burst 100 --fast-list --buffer-size 64M";
            }

            string args = string.Format("{0} \"{1}\" \"{2}\" --transfers {3} --checkers {4} --drive-chunk-size {5} {6} -P --stats 1s",
                action, sourcePath, destPath, streams, streams * 2, chunkSize, cloudAccel);

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
                if (_activeProcess == null) return;

                _activeProcess.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        ParseOutputLine(e.Data, itemIndex, totalItems);
                    }
                };

                _activeProcess.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        ParseOutputLine(e.Data, itemIndex, totalItems);
                    }
                };

                _activeProcess.BeginOutputReadLine();
                _activeProcess.BeginErrorReadLine();
                _activeProcess.WaitForExit();
            }
            catch (Exception ex)
            {
                EmitProgress(new TransferProgressEventArgs { Message = "Transfer exception: " + ex.Message, IsError = true });
            }
        }

        private void ExecuteRobocopyTransfer(string sourcePath, string destPath, TransferMode mode, int threads, int itemIndex, int totalItems)
        {
            if (File.Exists(sourcePath))
            {
                string srcDir = Path.GetDirectoryName(sourcePath);
                string fileName = Path.GetFileName(sourcePath);
                string destDir = Path.GetDirectoryName(destPath);
                if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

                string moveFlag = (mode == TransferMode.Move) ? "/MOV" : "";
                string args = string.Format("\"{0}\" \"{1}\" \"{2}\" /MT:{3} /NP /R:2 /W:2 {4}", srcDir, destDir, fileName, threads, moveFlag);
                RunCommandLine("robocopy.exe", args, itemIndex, totalItems);
            }
            else
            {
                if (!Directory.Exists(destPath)) Directory.CreateDirectory(destPath);
                string moveFlag = (mode == TransferMode.Move) ? "/MOVE" : "";
                string args = string.Format("\"{0}\" \"{1}\" /E /MT:{2} /NP /R:2 /W:2 {3}", sourcePath, destPath, threads, moveFlag);
                RunCommandLine("robocopy.exe", args, itemIndex, totalItems);
            }
        }

        private void ExecutePurge(string path, string rcloneExe)
        {
            EmitProgress(new TransferProgressEventArgs { Message = "Fast-purging target: " + path, ActiveFile = "Purging: " + Path.GetFileName(path) });

            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                    EmitProgress(new TransferProgressEventArgs { Message = "Purged file: " + path });
                }
                catch (Exception ex)
                {
                    EmitProgress(new TransferProgressEventArgs { Message = "Purge error: " + ex.Message, IsError = true });
                }
                return;
            }

            if (Directory.Exists(path))
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "cmd.exe";
                psi.Arguments = string.Format("/c \"rd /s /q \"\"{0}\"\"\"", path);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;

                try
                {
                    _activeProcess = Process.Start(psi);
                    if (_activeProcess != null)
                    {
                        _activeProcess.WaitForExit();
                        EmitProgress(new TransferProgressEventArgs { Message = "Purged directory: " + path });
                    }
                }
                catch (Exception ex)
                {
                    EmitProgress(new TransferProgressEventArgs { Message = "Purge error: " + ex.Message, IsError = true });
                }
            }
        }

        private void RunCommandLine(string exe, string args, int itemIndex, int totalItems)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = exe;
            psi.Arguments = args;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.CreateNoWindow = true;

            try
            {
                _activeProcess = Process.Start(psi);
                if (_activeProcess != null)
                {
                    _activeProcess.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            EmitProgress(new TransferProgressEventArgs
                            {
                                Message = e.Data,
                                CurrentItemIndex = itemIndex,
                                TotalItems = totalItems,
                                Percentage = (int)(((itemIndex + 0.5) / totalItems) * 100)
                            });
                        }
                    };
                    _activeProcess.BeginOutputReadLine();
                    _activeProcess.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                EmitProgress(new TransferProgressEventArgs { Message = "Command error: " + ex.Message, IsError = true });
            }
        }

        private void ParseOutputLine(string line, int itemIndex, int totalItems)
        {
            TransferProgressEventArgs args = new TransferProgressEventArgs
            {
                Message = line,
                CurrentItemIndex = itemIndex,
                TotalItems = totalItems
            };

            Match statsMatch = StatsRegex.Match(line);
            if (statsMatch.Success)
            {
                string transferred = statsMatch.Groups[1].Value.Trim();
                string total = statsMatch.Groups[2].Value.Trim();
                string percentStr = statsMatch.Groups[3].Value.Trim();
                string speed = statsMatch.Groups[4].Value.Trim();
                string eta = statsMatch.Groups[5].Value.Trim();

                int itemPercent;
                int.TryParse(percentStr, out itemPercent);

                double overall = ((itemIndex * 100.0) + itemPercent) / totalItems;
                args.Percentage = (int)Math.Min(99, overall);

                args.TransferredStats = string.Format("{0} / {1} (Item {2}/{3})", transferred, total, itemIndex + 1, totalItems);
                args.Speed = speed;
                args.Eta = eta;
            }

            Match fileMatch = FileRegex.Match(line);
            if (fileMatch.Success)
            {
                args.ActiveFile = string.Format("[{0}/{1}] {2}", itemIndex + 1, totalItems, fileMatch.Groups[1].Value.Trim());
            }

            EmitProgress(args);
        }

        private void EmitProgress(string msg)
        {
            EmitProgress(new TransferProgressEventArgs { Message = msg });
        }

        private void EmitProgress(TransferProgressEventArgs args)
        {
            EventHandler<TransferProgressEventArgs> handler = ProgressReceived;
            if (handler != null)
            {
                handler(this, args);
            }
        }

        private string FindRclone()
        {
            string localRclone = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rclone.exe");
            if (File.Exists(localRclone)) return localRclone;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string wingetPath = Path.Combine(localAppData, @"Microsoft\WinGet\Packages\Rclone.Rclone_Microsoft.Winget.Source_8wekyb3d8bbwe\rclone-v1.76.0-windows-amd64\rclone.exe");
            if (File.Exists(wingetPath)) return wingetPath;

            string[] paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';');
            foreach (string p in paths)
            {
                try
                {
                    string candidate = Path.Combine(p.Trim(), "rclone.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }

            return null;
        }
    }
}

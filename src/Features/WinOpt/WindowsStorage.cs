using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Toolbox.WinOpt
{
    internal sealed class DriveSummary
    {
        public string Volume { get; set; }
        public string Media { get; set; }
        public string Capacity { get; set; }
        public string Used { get; set; }
        public string Free { get; set; }
        public double UsedPercent { get; set; }
    }

    internal static class WindowsStorage
    {
        internal static List<DriveSummary> ReadDrives(CancellationToken cancellation)
        {
            var rows = new List<DriveSummary>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (cancellation.IsCancellationRequested) return rows;
                try
                {
                    if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                    long size = drive.TotalSize, free = drive.TotalFreeSpace;
                    rows.Add(new DriveSummary { Volume = drive.Name, Media = "Unknown", Capacity = StorageFormat.Bytes(size),
                        Used = StorageFormat.Bytes(size - free), Free = StorageFormat.Bytes(free), UsedPercent = size > 0 ? (size - free) * 100.0 / size : 0 });
                }
                catch (Exception e) { if (!PathPolicy.Recoverable(e)) throw; }
            }
            // Read-only query. Never interpolate labels, paths or other user input into PowerShell.
            const string script = "$ErrorActionPreference='Stop'; $r=@(Get-Partition | Where-Object DriveLetter | ForEach-Object { $p=$_; $d=$p | Get-Disk; $m='Unknown'; if ([string]$d.BusType -notin @('0','14','15','16','Unknown','Virtual','File Backed Virtual','Storage Spaces')) { $pd=@(Get-PhysicalDisk | Where-Object { [string]$_.DeviceId -eq [string]$d.Number }); if ($pd.Count -eq 1) { $t=[string]$pd[0].MediaType; if ($t -in @('4','SSD')) {$m='SSD'} elseif ($t -in @('3','HDD')) {$m='HDD'} } }; [pscustomobject]@{Letter=[string]$p.DriveLetter;Media=$m} }); ConvertTo-Json -InputObject $r -Compress";
            try
            {
                var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"),
                    "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)))
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (var process = Process.Start(start))
                {
                    var output = process.StandardOutput.ReadToEndAsync();
                    var errors = process.StandardError.ReadToEndAsync();
                    var timer = Stopwatch.StartNew();
                    while (!process.WaitForExit(100))
                    {
                        if (cancellation.IsCancellationRequested || timer.Elapsed > TimeSpan.FromSeconds(12))
                        { process.Kill(); process.WaitForExit(2000); return rows; }
                    }
                    if (process.ExitCode != 0) return rows;
                    Task.WaitAll(output, errors);
                    var items = new JavaScriptSerializer().Deserialize<List<Dictionary<string, string>>>(output.Result);
                    if (items != null) foreach (var row in rows)
                    {
                        var matches = items.Where(i => i.ContainsKey("Letter") && string.Equals(i["Letter"], row.Volume.Substring(0, 1), StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (matches.Length == 1 && matches[0].ContainsKey("Media") && new[] { "SSD", "HDD" }.Contains(matches[0]["Media"])) row.Media = matches[0]["Media"];
                    }
                }
            }
            catch (Exception e) { if (!PathPolicy.Recoverable(e) && !(e is InvalidOperationException)) throw; }
            return rows;
        }

        internal static string NativeTarget(string action)
        {
            switch (action)
            {
                case "Optimize": return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "dfrgui.exe");
                case "Storage": return "ms-settings:storagepolicies";
                case "Recommendations": return "ms-settings:storagerecommendations";
                case "Recycle": return "shell:RecycleBinFolder";
                case "TaskManager": return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "taskmgr.exe");
                case "StartupApps": return "ms-settings:startupapps";
                case "InstalledApps": return "ms-settings:appsfeatures";
                default: throw new ArgumentException("Unknown Windows maintenance action.");
            }
        }
        internal static void Open(string action)
        {
            var target = NativeTarget(action);
            if (action == "Recycle") Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), target) { UseShellExecute = true });
            else Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
    }
}

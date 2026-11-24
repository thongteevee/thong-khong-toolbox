using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Toolbox
{
    internal sealed class ToolboxUpdater
    {
        private const string InstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{8E75B3C2-11DB-4AB1-9D06-1BFCC9D1E407}_is1";
        private readonly string executable;
        private readonly string installer;

        private ToolboxUpdater(string executable, string installer)
        {
            this.executable = executable;
            this.installer = installer;
        }

        internal static bool IsInstalled(string target)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(InstallKey))
            {
                var location = key == null ? null : key.GetValue("InstallLocation") as string;
                return !String.IsNullOrEmpty(location) && String.Equals(
                    Path.GetFullPath(Path.Combine(location, "ThongKhongToolbox.exe")),
                    Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase);
            }
        }

        internal static async Task<ToolboxUpdater> Download(Version version, Action<string> progress)
        {
            if (version <= ToolboxReleases.CurrentVersion)
                throw new InvalidOperationException("Choose a newer version to update Toolbox.");
            var target = typeof(ToolboxUpdater).Assembly.Location;
            var installed = IsInstalled(target);
            var folder = Path.Combine(DownloadRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var root = "https://github.com/thongteevee/thong-khong-toolbox/releases/download/v" + version + "/";
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("ThongKhongToolbox/" + ToolboxReleases.CurrentVersion);
                var checksums = await http.GetStringAsync(root + "SHA256SUMS.txt");
                var executable = Path.Combine(folder, "ThongKhongToolbox.exe");
                progress("Downloading version " + version + "…");
                await DownloadAsset(http, root, executable, checksums);
                Version downloaded;
                if (!Version.TryParse(FileVersionInfo.GetVersionInfo(executable).FileVersion, out downloaded) ||
                    downloaded != new Version(version.Major, version.Minor, version.Build, 0))
                    throw new InvalidOperationException("The downloaded app version does not match the release.");

                string installer = null;
                if (installed)
                {
                    progress("Downloading the Windows installer…");
                    installer = Path.Combine(folder, "ThongKhongToolboxSetup.exe");
                    await DownloadAsset(http, root, installer, checksums);
                }
                return new ToolboxUpdater(executable, installer);
            }
        }

        private static async Task DownloadAsset(HttpClient http, string root, string destination, string checksums)
        {
            var name = Path.GetFileName(destination);
            var lines = checksums.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var matches = lines.Select(line => line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length == 2 && parts[1] == name).ToList();
            if (matches.Count != 1 || matches[0][0].Length != 64 || !matches[0][0].All(Uri.IsHexDigit))
                throw new InvalidOperationException("The release has no valid checksum for " + name + ".");

            using (var response = await http.GetAsync(root + name))
            {
                response.EnsureSuccessStatusCode();
                using (var output = File.Create(destination))
                    await response.Content.CopyToAsync(output);
            }
            string actual;
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(destination))
                actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            if (!String.Equals(actual, matches[0][0], StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(destination);
                throw new InvalidOperationException("The download failed verification. Try the update again.");
            }
        }

        internal async Task Launch()
        {
            var target = typeof(ToolboxUpdater).Assembly.Location;
            var signal = @"Local\ThongKhongToolbox.Update." + Guid.NewGuid().ToString("N");
            using (var ready = new EventWaitHandle(false, EventResetMode.ManualReset, signal))
            {
                var arguments = "--finish-update " + Process.GetCurrentProcess().Id + " " + Quote(target) + " " + Quote(signal);
                if (installer != null) arguments += " " + Quote(installer);
                using (Process.Start(new ProcessStartInfo(executable, arguments) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden })) {}
                if (!await Task.Run(() => ready.WaitOne(15000)))
                    throw new InvalidOperationException("The updater could not start. Try again or use View release downloads.");
            }
        }

        internal static void Finish(string[] args)
        {
            if (args.Length != 4 && args.Length != 5) throw new InvalidOperationException("Invalid update request.");
            var target = Path.GetFullPath(args[2]);
            var source = typeof(ToolboxUpdater).Assembly.Location;
            if (Path.GetFileName(target) != "ThongKhongToolbox.exe" || String.Equals(source, target, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid Toolbox update location.");
            using (var parent = Process.GetProcessById(Int32.Parse(args[1])))
            {
                if (!String.Equals(parent.MainModule.FileName, target, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The update request does not match the running app.");
                using (var ready = EventWaitHandle.OpenExisting(args[3])) ready.Set();
                if (!parent.WaitForExit(60000)) throw new InvalidOperationException("Toolbox is still running. Close it and try again.");
            }

            if (args.Length == 5)
            {
                var installer = Path.GetFullPath(args[4]);
                if (Path.GetDirectoryName(installer) != Path.GetDirectoryName(source) || !IsInstalled(target))
                    throw new InvalidOperationException("Invalid installer update location.");
                var options = "/SILENT /SUPPRESSMSGBOXES /NORESTART /NOCLOSEAPPLICATIONS /NORESTARTAPPLICATIONS /DIR=" +
                    Quote(Path.GetDirectoryName(target));
                using (var setup = Process.Start(new ProcessStartInfo(installer, options) { UseShellExecute = false }))
                {
                    setup.WaitForExit();
                    if (setup.ExitCode != 0) throw new InvalidOperationException("The Windows update did not complete (code " + setup.ExitCode + ").");
                }
            }
            else ReplacePortable(source, target);

            if (FileVersionInfo.GetVersionInfo(target).FileVersion != FileVersionInfo.GetVersionInfo(source).FileVersion)
                throw new InvalidOperationException("The installed app version could not be verified. Run the update again from release downloads.");
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(target) });
        }

        private static void ReplacePortable(string source, string target)
        {
            var staged = target + "." + Guid.NewGuid().ToString("N") + ".update";
            var backup = staged + ".backup";
            try
            {
                File.Copy(source, staged);
                File.Replace(staged, target, backup);
            }
            finally
            {
                if (File.Exists(staged)) File.Delete(staged);
            }
        }

        internal static string DownloadRoot { get { return Path.Combine(Path.GetTempPath(), "ThongKhongToolbox"); } }

        // Retained for compatibility. Age is never permission to delete existing downloads.
        internal static void RemoveOldDownloads(string root, DateTime nowUtc)
        {
            // Intentionally no filesystem access or mutation.
        }

        private static string Quote(string value) { return "\"" + value + "\""; }
    }
}

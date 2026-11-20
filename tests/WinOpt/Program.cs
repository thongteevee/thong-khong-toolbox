using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Toolbox.WinOpt;

internal static class WinOptTests
{
    private static int passed, failed;
    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly string FixtureBase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures");
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLink(string newName, string existingName, IntPtr security);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateSymbolicLink(string link, string target, uint flags);

    private sealed class Fixture : IDisposable
    {
        internal readonly string Root;
        internal Fixture() { Root = Path.Combine(FixtureBase, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Root); }
        internal string FileAt(string relative, double days)
        {
            var path = Path.Combine(Root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "Disposable WinOpt fixture contents.");
            File.SetCreationTimeUtc(path, Now.AddDays(-days)); File.SetLastWriteTimeUtc(path, Now.AddDays(-days)); return path;
        }
        internal ScanPolicy Policy(CleanupMode mode = CleanupMode.Safe, params string[] exclusions)
        { return new ScanPolicy(mode, new[] { new TempRoot(Root, "Fixture") }, exclusions, Now); }
        internal ScanResult Scan(CleanupMode mode = CleanupMode.Safe)
        { return new TempScanner().Scan(Policy(mode), CancellationToken.None, null); }
        public void Dispose()
        {
            if (!PathPolicy.Within(Path.GetFullPath(Root), Path.GetFullPath(FixtureBase), false)) throw new InvalidOperationException("Unsafe fixture teardown.");
            TearDown(Root);
        }
        private static void TearDown(string directory)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    if ((attributes & FileAttributes.Directory) != 0) Directory.Delete(entry); else File.Delete(entry);
                }
                else if ((attributes & FileAttributes.Directory) != 0) TearDown(entry);
                else { File.SetAttributes(entry, FileAttributes.Normal); File.Delete(entry); }
            }
            Directory.Delete(directory);
        }
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Throws(Action action)
    { bool thrown = false; try { action(); } catch (ArgumentException) { thrown = true; } catch (InvalidOperationException) { thrown = true; } Assert(thrown, "Expected refusal."); }
    private static void Test(string name, Action action)
    { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); } }
    private static CleanupService Cleaner() { return new CleanupService(v => 1000L, () => Now); }
    private sealed class CancelProgress : IProgress<int>
    {
        private readonly CancellationTokenSource source;
        internal CancelProgress(CancellationTokenSource value) { source = value; }
        public void Report(int value) { source.Cancel(); }
    }
    private static CleanupResult Run(ScanResult scan, bool dry = false, IEnumerable<Candidate> files = null)
    { return Cleaner().Run(scan, files ?? scan.Files, dry, CancellationToken.None, null); }

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--read-only-drives"))
        {
            foreach (var row in WindowsStorage.ReadDrives(CancellationToken.None))
                Console.WriteLine(row.Volume + " " + row.Media + " " + row.Capacity + " / free " + row.Free);
            return 0;
        }
        Directory.CreateDirectory(FixtureBase);
        Test("strict Safe/Standard age boundaries and Aggressive eligibility", () =>
        {
            using (var f = new Fixture())
            {
                var safe = f.Policy(); Assert(!safe.OldEnough(Now.AddDays(-7)), "7-day boundary must survive.");
                Assert(safe.OldEnough(Now.AddDays(-7).AddTicks(-1)), "Older must qualify.");
                var standard = f.Policy(CleanupMode.Standard); Assert(!standard.OldEnough(Now.AddDays(-1)), "24-hour boundary.");
                Assert(standard.OldEnough(Now.AddDays(-1).AddTicks(-1)), "Older Standard.");
                var aggressive = f.Policy(CleanupMode.Aggressive); Assert(aggressive.OldEnough(Now), "Aggressive now.");
                Assert(!aggressive.OldEnough(Now.AddSeconds(1)), "Future timestamp.");
            }
        });
        Test("canonical case-insensitive directory boundaries", () =>
        {
            Assert(PathPolicy.Within(@"C:\TEMP\a.tmp", @"c:\temp", false), "Case handling.");
            Assert(!PathPolicy.Within(@"C:\temporary\a", @"C:\temp", true), "Prefix collision.");
            Assert(!PathPolicy.Within(@"C:\temp", @"C:\temp", false), "Root cannot be a file candidate.");
        });
        Test("reject traversal, ADS, UNC, device, relative, trailing-dot paths", () =>
        {
            foreach (var path in new[] { @"C:\temp\..\secret", @"C:\temp\a:stream", @"\\server\share", @"\\?\C:\temp", @"C:temp", @"temp", @"C:\temp\ambiguous.", @"C:\temp\space " })
                Throws(() => PathPolicy.Canonical(path));
        });
        Test("reject unsafe roots and invalid cleanup modes", () =>
        {
            Throws(() => PathPolicy.ValidateRoot(@"C:\"));
            Throws(() => PathPolicy.ValidateRoot(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
            Throws(() => PathPolicy.ValidateRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)));
            Throws(() => PathPolicy.ValidateRoot(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")));
            using (var f = new Fixture()) Throws(() => f.Policy((CleanupMode)999));
        });
        Test("scan is read-only and accurately accounts eligible bytes", () =>
        {
            using (var f = new Fixture())
            {
                var a = f.FileAt("a.tmp", 10); var b = f.FileAt("nested\\b.tmp", 11); f.FileAt("recent.tmp", .1);
                var result = f.Scan(); Assert(result.Files.Count == 2, "Eligibility.");
                Assert(result.Bytes == new FileInfo(a).Length + new FileInfo(b).Length, "Byte sum.");
                Assert(File.Exists(a) && File.Exists(b), "Read-only scan.");
            }
        });
        Test("production TEMP redirects into general-purpose and project folders are refused", () =>
        {
            using (var f = new Fixture())
            {
                Throws(() => PathPolicy.ValidateProductionTemp(f.Root));
                var redirected = Path.Combine(f.Root, "Temp"); Directory.CreateDirectory(redirected);
                Throws(() => PathPolicy.ValidateProductionTemp(redirected));
            }
        });
        Test("creation time protects newly created files with old write times", () =>
        {
            using (var f = new Fixture()) { var a = f.FileAt("new.tmp", 10); File.SetCreationTimeUtc(a, Now); Assert(f.Scan().Files.Count == 0, "New creation must survive."); }
        });
        Test("Standard includes 2-day files; Aggressive includes fresh files", () =>
        {
            using (var f = new Fixture()) { f.FileAt("two.tmp", 2); f.FileAt("fresh.tmp", .01); Assert(f.Scan().Files.Count == 0, "Safe."); Assert(f.Scan(CleanupMode.Standard).Files.Count == 1, "Standard."); Assert(f.Scan(CleanupMode.Aggressive).Files.Count == 2, "Aggressive."); }
        });
        Test("exclusions protect entire subtree and single file", () =>
        {
            using (var f = new Fixture())
            {
                f.FileAt("keep\\a.tmp", 10); var b = f.FileAt("b.tmp", 10); f.FileAt("c.tmp", 10);
                var result = new TempScanner().Scan(f.Policy(CleanupMode.Aggressive, Path.Combine(f.Root, "keep"), b), CancellationToken.None, null);
                Assert(result.Files.Count == 1 && result.Files[0].Path.EndsWith("c.tmp"), "Exclusions.");
            }
        });
        Test("protected attributes, installers and project folders survive every mode", () =>
        {
            using (var f = new Fixture())
            {
                var a = f.FileAt("hidden.tmp", 10); File.SetAttributes(a, FileAttributes.Hidden);
                var b = f.FileAt("readonly.tmp", 10); File.SetAttributes(b, FileAttributes.ReadOnly);
                var c = f.FileAt("system.tmp", 10); File.SetAttributes(c, FileAttributes.System);
                f.FileAt("payload.msi", 10); f.FileAt("payload.exe", 10); f.FileAt("setup-cache\\data.tmp", 10);
                f.FileAt("project\\.git\\HEAD", 10); f.FileAt("project\\source.tmp", 10);
                foreach (CleanupMode mode in Enum.GetValues(typeof(CleanupMode))) Assert(f.Scan(mode).Files.Count == 0, "Protected mode " + mode);
            }
        });
        Test("locked files are excluded from preview", () =>
        {
            using (var f = new Fixture()) { var a = f.FileAt("locked.tmp", 10); using (File.Open(a, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Assert(f.Scan().Files.Count == 0, "Locked preview."); }
        });
        Test("dry run validates but never deletes", () =>
        {
            using (var f = new Fixture()) { var a = f.FileAt("a.tmp", 10); var scan = f.Scan(); var result = Run(scan, true); Assert(result.Completed == 1 && File.Exists(a) && !scan.Consumed, "Dry run."); }
        });
        Test("cleanup deletes only selected files, once", () =>
        {
            using (var f = new Fixture())
            {
                var a = f.FileAt("a.tmp", 10); var b = f.FileAt("b.tmp", 10); var scan = f.Scan();
                var result = Run(scan, false, scan.Files.Where(c => c.Path == a));
                Assert(result.Completed == 1 && !File.Exists(a) && File.Exists(b), "Selection boundary.");
                Throws(() => Run(scan));
            }
        });
        Test("changed file is skipped", () =>
        {
            using (var f = new Fixture()) { var a = f.FileAt("a.tmp", 10); var scan = f.Scan(); File.AppendAllText(a, "changed"); var result = Run(scan); Assert(result.Completed == 0 && result.Skipped == 1 && File.Exists(a), "Changed file."); }
        });
        Test("replaced file with identical contents and times is skipped", () =>
        {
            using (var f = new Fixture())
            {
                var a = f.FileAt("a.tmp", 10); var scan = f.Scan(); File.Move(a, a + ".old"); f.FileAt("a.tmp", 10);
                var result = Run(scan); Assert(result.Completed == 0 && result.Skipped == 1 && File.Exists(a), "Identity replacement.");
            }
        });
        Test("locked candidate survives cleanup with reported failure", () =>
        {
            using (var f = new Fixture())
            {
                var a = f.FileAt("a.tmp", 10); var scan = f.Scan();
                using (File.Open(a, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { var result = Run(scan); Assert(result.Failed == 1 && File.Exists(a), "Locked cleanup."); }
            }
        });
        Test("missing candidate failure does not stop remaining files", () =>
        {
            using (var f = new Fixture())
            {
                var a = f.FileAt("a.tmp", 10); var b = f.FileAt("b.tmp", 10); var scan = f.Scan(); File.Delete(a);
                var result = Run(scan); Assert(result.Failed == 1 && result.Completed == 1 && !File.Exists(b), "Continue on failure.");
            }
        });
        Test("protection added after scan is respected by dry run and cleanup", () =>
        {
            using (var f = new Fixture())
            {
                var a = f.FileAt("a.tmp", 10); var scan = f.Scan(); File.SetAttributes(a, FileAttributes.ReadOnly);
                var dry = Run(scan, true); var result = Run(scan);
                Assert(dry.Completed == 0 && result.Completed == 0 && File.Exists(a), "New protection must survive.");
            }
        });
        Test("expired and clock-regressed previews are refused", () =>
        {
            using (var f = new Fixture()) { var a = f.FileAt("a.tmp", 10); var scan = f.Scan(); Throws(() => new CleanupService(v => null, () => Now.AddMinutes(31)).Run(scan, scan.Files, false, CancellationToken.None, null)); Throws(() => new CleanupService(v => null, () => Now.AddMinutes(-1)).Run(scan, scan.Files, false, CancellationToken.None, null)); Assert(File.Exists(a), "Expired file survives."); }
        });
        Test("foreign and empty selections are refused", () =>
        {
            using (var f = new Fixture()) { f.FileAt("a.tmp", 10); var a = f.Scan(); var b = f.Scan(); Throws(() => Run(a, false, b.Files)); Throws(() => Run(a, false, new Candidate[0])); }
        });
        Test("pre-cancelled scan and cleanup preserve files", () =>
        {
            using (var f = new Fixture())
            using (var cancel = new CancellationTokenSource())
            {
                var a = f.FileAt("a.tmp", 10); cancel.Cancel(); var scan = new TempScanner().Scan(f.Policy(), cancel.Token, null);
                Assert(scan.Cancelled && scan.Files.Count == 0, "Scan cancellation.");
                scan = f.Scan(); var result = Cleaner().Run(scan, scan.Files, false, cancel.Token, null);
                Assert(result.Cancelled && result.Completed == 0 && File.Exists(a), "Cleanup cancellation.");
            }
        });
        Test("hard links are refused in preview and when introduced after preview", () =>
        {
            using (var f = new Fixture())
            {
                var a = f.FileAt("a.tmp", 10); var scan = f.Scan(); var link = Path.Combine(f.Root, "alias.tmp");
                Assert(CreateHardLink(link, a, IntPtr.Zero), "Fixture hard-link creation: " + Marshal.GetLastWin32Error());
                Assert(f.Scan().Files.Count == 0, "Hard-link preview.");
                var result = Run(scan); Assert(result.Completed == 0 && File.Exists(a) && File.Exists(link), "Hard-link cleanup.");
            }
        });
        Test("mid-cleanup cancellation retains completed results and remaining files", () =>
        {
            using (var f = new Fixture())
            using (var source = new CancellationTokenSource())
            {
                for (int i = 0; i < 30; i++) f.FileAt("file" + i + ".tmp", 10);
                var scan = f.Scan(); var result = Cleaner().Run(scan, scan.Files, false, source.Token, new CancelProgress(source));
                Assert(result.Cancelled && result.Completed == 25 && Directory.GetFiles(f.Root).Length == 5, "Partial cancellation accounting.");
            }
        });
        Test("ancestor pins prevent directory replacement", () =>
        {
            using (var f = new Fixture())
            {
                var a = f.FileAt("nested\\a.tmp", 10); var parent = Path.GetDirectoryName(a); bool refused = false;
                using (NativeFiles.PinDirectory(parent)) { try { Directory.Move(parent, parent + "-moved"); } catch (IOException) { refused = true; } }
                Assert(refused && File.Exists(a), "Ancestor rename must be refused.");
            }
        });
        Test("symlink traversal and link swapped after preview are refused", () =>
        {
            using (var f = new Fixture())
            using (var outside = new Fixture())
            {
                var target = outside.FileAt("outside.tmp", 10); var a = f.FileAt("a.tmp", 10); var scan = f.Scan(); File.Delete(a);
                if (!CreateSymbolicLink(a, target, 2)) throw new Exception("Fixture symlink creation unavailable, Windows error " + Marshal.GetLastWin32Error());
                Assert(f.Scan().Files.Count == 0, "Symlink scan."); var result = Run(scan);
                Assert(result.Completed == 0 && File.Exists(target), "Symlink target must survive.");
                var directoryLink = Path.Combine(f.Root, "linked");
                Assert(CreateSymbolicLink(directoryLink, outside.Root, 3), "Directory symlink fixture.");
                Assert(f.Scan().Files.Count == 0, "Directory symlink must not be followed.");
                bool refused = false; try { using (NativeFiles.PinDirectory(directoryLink)) { } } catch (IOException) { refused = true; }
                Assert(refused, "Pin linked ancestor.");
            }
        });
        Test("project marker added after scan prevents cleanup", () =>
        {
            using (var f = new Fixture()) { var a = f.FileAt("nested\\a.tmp", 10); var scan = f.Scan(); Directory.CreateDirectory(Path.Combine(f.Root, ".git")); var result = Run(scan); Assert(result.Completed == 0 && result.Skipped == 1 && File.Exists(a), "New project marker."); }
        });
        Test("observed free-space delta stays distinct from logical bytes", () =>
        {
            using (var f = new Fixture()) { f.FileAt("a.tmp", 10); var scan = f.Scan(); int calls = 0; var result = new CleanupService(v => ++calls == 1 ? 1000L : 900L, () => Now).Run(scan, scan.Files, true, CancellationToken.None, null); Assert(result.Volumes.Single().Delta == -100 && result.Bytes > 0, "Negative measured delta must remain visible."); }
        });
        Test("unavailable free-space measurement stays unavailable", () =>
        {
            using (var f = new Fixture()) { f.FileAt("a.tmp", 10); var scan = f.Scan(); var result = new CleanupService(v => null, () => Now).Run(scan, scan.Files, true, CancellationToken.None, null); Assert(result.Volumes.Single().Delta == null, "Unknown delta."); }
        });
        Test("logs contain aggregate results without candidate filenames", () =>
        {
            using (var f = new Fixture())
            {
                var secret = f.FileAt("private-name.tmp", 10); var scan = f.Scan(); var result = Run(scan, true); var log = new MaintenanceLog(Path.Combine(f.Root, "logs"));
                Assert(log.Write("dry-run", result.Completed, 0, 0, result.Bytes, false, result.Reasons) == null, "Log write.");
                var text = File.ReadAllText(Path.Combine(log.Folder, "maintenance.jsonl")); Assert(!text.Contains(secret) && !text.Contains("private-name"), "Filename privacy.");
                Assert(log.Recent().Contains("dry-run"), "Recent history.");
            }
        });
        Test("logging failures are warnings and rotation is bounded", () =>
        {
            using (var f = new Fixture())
            {
                var blocker = f.FileAt("blocker", 10); Assert(new MaintenanceLog(blocker).Write("scan", 0, 0, 0, 0, false, new Dictionary<string, int>()) != null, "Log failure surfaced.");
                var folder = Path.Combine(f.Root, "logs"); Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder, "maintenance.jsonl"), new string('x', 1024 * 1024 + 1));
                var log = new MaintenanceLog(folder); Assert(log.Write("scan", 0, 0, 0, 0, false, new Dictionary<string, int>()) == null, "Rotation.");
                Assert(Directory.GetFiles(folder).Length == 2, "Bounded log files.");
            }
        });
        Test("native integration targets are fixed and non-destructive", () =>
        {
            Assert(WindowsStorage.NativeTarget("Optimize").EndsWith("dfrgui.exe"), "Optimizer.");
            Assert(WindowsStorage.NativeTarget("Recommendations") == "ms-settings:storagerecommendations", "Recommendations URI.");
            Assert(WindowsStorage.NativeTarget("Storage") == "ms-settings:storagepolicies", "Sense URI.");
            Throws(() => WindowsStorage.NativeTarget("arbitrary command"));
        });
        Test("corrupt history never prevents maintenance UI loading", () =>
        {
            using (var f = new Fixture())
            {
                var log = new MaintenanceLog(f.Root); var path = Path.Combine(f.Root, "maintenance.jsonl");
                foreach (var text in new[] { "null", "{}", "invalid JSON", "{\"timestampUtc\":\"x\",\"action\":\"cleanup\",\"completed\":1,\"logicalBytes\":\"invalid\"}" })
                { File.WriteAllText(path, text); Assert(!string.IsNullOrWhiteSpace(log.Recent()), "Corrupt record should return a diagnostic."); }
            }
        });
        Test("rounded tab borders fit and wrap; cleanup starts disabled", LayoutChecks.TabsAndInitialState);
        Test("Grid/List switching and tile commands work in WPF", LayoutChecks.CatalogSwitchAndCommands);
        Test("simple mode, advanced policy, ten-row preview, resizing and expanded selections", LayoutChecks.SimpleModeAndFileList);
        Test("completed progress stays full; cancellation and failure do not show completion", LayoutChecks.CompletedProgress);
        Test("largest-first preview, truthful empty states and simple/advanced result copying", LayoutChecks.SortEmptyAndCopy);
        Console.WriteLine(passed + " passed; " + failed + " failed. All cleanup used isolated repository fixtures.");
        return failed == 0 ? 0 : 1;
    }
}

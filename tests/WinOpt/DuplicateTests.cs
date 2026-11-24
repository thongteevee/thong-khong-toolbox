using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Toolbox.WinOpt;

internal static class DuplicateTests
{
    private static string runRoot, manifest;
    private static int passed, failed, skipped;
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLink(string link, string target, IntPtr security);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateSymbolicLink(string link, string target, uint flags);
    private sealed class Skip : Exception { internal Skip(string message) : base(message) { } }
    private sealed class Fixture
    {
        internal readonly string Root;
        internal Fixture()
        {
            Root = Path.Combine(runRoot, "fixtures", Guid.NewGuid().ToString("N"));
            if (Directory.Exists(Root)) throw new InvalidOperationException("Fixture must be new.");
            Directory.CreateDirectory(Root); Record("directory", Root);
        }
        internal string Add(string relative, string contents)
        {
            var path = Path.Combine(Root, relative); var parent = Path.GetDirectoryName(path);
            if (!PathPolicy.Within(path, Root, false) || File.Exists(path)) throw new InvalidOperationException("Fixture must be new and in scope.");
            Directory.CreateDirectory(parent); File.WriteAllText(path, contents); Record("file", path); return path;
        }
        internal DuplicateOptions Options(bool recursive = true, int limit = 100000, params string[] exclusions)
        { return new DuplicateOptions(new[] { Root }, exclusions, recursive, limit); }
        internal DuplicateResult Scan(bool recursive = true)
        { return new DuplicateScanner().Scan(Options(recursive), CancellationToken.None, null); }
    }
    private static void Record(string kind, string path)
    { File.AppendAllText(manifest, kind + "\t" + path + Environment.NewLine); }
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Skip e) { skipped++; Console.WriteLine("SKIP " + name + ": " + e.Message); }
        catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); }
    }
    private static void Refused(Action action)
    { bool refused = false; try { action(); } catch (ArgumentException) { refused = true; } catch (NotSupportedException) { refused = true; } Assert(refused, "Expected refusal."); }
    private static string Fingerprint(string path)
    {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(path)) return Convert.ToBase64String(sha.ComputeHash(stream)) + ":" + File.GetLastWriteTimeUtc(path).Ticks + ":" + File.GetAttributes(path);
    }
    private static void Layout(FrameworkElement view, double width)
    {
        view.Measure(new Size(width, 750)); view.Arrange(new Rect(0, 0, width, 750)); view.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { })); view.UpdateLayout();
    }
    private static IEnumerable<T> Children<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var child = VisualTreeHelper.GetChild(parent, i); if (child is T) yield return (T)child; foreach (var nested in Children<T>(child)) yield return nested; }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Pass the new test-run directory.");
        runRoot = Path.GetFullPath(args[0]);
        if (!Directory.Exists(runRoot) || !File.Exists(Path.Combine(runRoot, "created-by-this-test-run.txt")))
            throw new InvalidOperationException("A new test-run directory is required.");
        manifest = Path.Combine(runRoot, "created-fixtures.tsv");
        if (File.Exists(manifest)) throw new InvalidOperationException("Never reuse an existing fixture manifest.");
        File.WriteAllText(manifest, "kind\tpath" + Environment.NewLine);
        Test("different names and dates with identical bytes form a group", () =>
        {
            var f = new Fixture(); var a = f.Add("old-not-disposable.txt", "identical contents"); var b = f.Add("renamed-copy.bin", "identical contents");
            File.SetLastWriteTimeUtc(a, DateTime.UtcNow.AddYears(-15));
            var beforeA = Fingerprint(a); var beforeB = Fingerprint(b); var result = f.Scan();
            Assert(result.Groups.Count == 1 && result.Groups[0].Count == 2, "Matching contents.");
            Assert(Fingerprint(a) == beforeA && Fingerprint(b) == beforeB, "Contents, last-write times and attributes must remain unchanged.");
        });
        Test("same size with different bytes is not a duplicate", () =>
        { var f = new Fixture(); f.Add("a.txt", "abcd"); f.Add("b.txt", "abce"); Assert(f.Scan().Groups.Count == 0, "Equal size is not proof."); });
        Test("large matching contents are checked across buffer boundaries", () =>
        {
            var f = new Fixture(); var contents = new string('x', 65536) + "boundary" + new string('y', 70000);
            var a = f.Add("a", contents); f.Add("b", contents); f.Add("c", contents.Substring(0, contents.Length - 1) + "z");
            var before = Fingerprint(a); var result = f.Scan();
            Assert(result.Groups.Count == 1 && result.Groups[0].Count == 2 && result.Hashed == 3 && Fingerprint(a) == before, "Full-file comparison and buffer reuse.");
        });
        Test("same filename in different folders is not proof", () =>
        { var f = new Fixture(); f.Add("one\\same.txt", "1234"); f.Add("two\\same.txt", "5678"); Assert(f.Scan().Groups.Count == 0, "Names are not proof."); });
        Test("size prefilter avoids hashing files with unique sizes", () =>
        { var f = new Fixture(); f.Add("a", "a"); f.Add("b", "bb"); Assert(f.Scan().Hashed == 0, "Unique lengths should not be hashed."); });
        Test("recursive scan and flat scan honor scope", () =>
        { var f = new Fixture(); f.Add("a", "same"); f.Add("nested\\b", "same"); Assert(f.Scan().Groups.Count == 1 && f.Scan(false).Groups.Count == 0, "Subfolder option."); });
        Test("overlapping selected roots do not invent extra copies", () =>
        {
            var f = new Fixture(); f.Add("a", "same"); f.Add("nested\\b", "same");
            var options = new DuplicateOptions(new[] { f.Root, f.Root, Path.Combine(f.Root, "nested") }, new string[0], true);
            var result = new DuplicateScanner().Scan(options, CancellationToken.None, null);
            Assert(options.Roots.Count == 1 && result.Groups.Single().Count == 2, "Nested scope deduplication.");
        });
        Test("file and subtree exclusions preserve prefix boundaries", () =>
        {
            var f = new Fixture(); f.Add("skip\\a", "same"); f.Add("skipper\\b", "same"); var c = f.Add("c", "same"); f.Add("d", "same");
            var result = new DuplicateScanner().Scan(f.Options(true, 1000, Path.Combine(f.Root, "skip"), c), CancellationToken.None, null);
            Assert(result.Groups.Single().Count == 2 && result.Groups[0].Files.Any(p => p.Path.Contains("skipper")), "Exclusion boundary.");
        });
        Test("locked files are skipped without changing them", () =>
        {
            var f = new Fixture(); var a = f.Add("a", "same"); f.Add("b", "same");
            using (File.Open(a, FileMode.Open, FileAccess.Read, FileShare.None)) Assert(f.Scan().Groups.Count == 0, "Locked file must not be read.");
            Assert(File.ReadAllText(a) == "same", "Locked fixture survives.");
        });
        Test("file changed between discovery and hashing is excluded", () =>
        {
            var f = new Fixture(); var a = f.Add("a", "same"); f.Add("b", "same"); bool changed = false;
            var scanner = new DuplicateScanner(path => { if (!changed && path == a) { changed = true; File.WriteAllText(a, "DIFFERENT"); } });
            var result = scanner.Scan(f.Options(), CancellationToken.None, null);
            Assert(changed && result.Groups.Count == 0 && result.Reasons.Count > 0, "Changed file cannot be confirmed.");
        });
        Test("empty files can be reported as matching without a space claim", () =>
        { var f = new Fixture(); f.Add("a", ""); f.Add("b", ""); var result = f.Scan(); Assert(result.Groups.Single().Files[0].Bytes == 0, "Empty contents."); });
        Test("read-only file attributes are preserved", () =>
        {
            var f = new Fixture(); var a = f.Add("a", "same"); f.Add("b", "same"); File.SetAttributes(a, FileAttributes.ReadOnly);
            var before = Fingerprint(a); Assert(f.Scan().Groups.Count == 1 && Fingerprint(a) == before, "Read-only data must remain unchanged.");
        });
        Test("pre-cancelled discovery reads no file contents", () =>
        {
            var f = new Fixture(); f.Add("a", "same"); f.Add("b", "same");
            using (var cancel = new CancellationTokenSource()) { cancel.Cancel(); var result = new DuplicateScanner().Scan(f.Options(), cancel.Token, null); Assert(result.Cancelled && result.Hashed == 0, "Cancellation."); }
        });
        Test("cancellation during hashing leaves all fixture contents intact", () =>
        {
            var f = new Fixture(); var a = f.Add("a", new string('a', 200000)); var b = f.Add("b", new string('a', 200000)); var before = Fingerprint(a);
            using (var cancel = new CancellationTokenSource())
            { var result = new DuplicateScanner(path => cancel.Cancel()).Scan(f.Options(), cancel.Token, null); Assert(result.Cancelled && result.Hashed == 0, "Hash cancellation."); }
            Assert(Fingerprint(a) == before && File.Exists(b), "Cancellation must not mutate data.");
        });
        Test("entry limits produce explicitly partial results", () =>
        { var f = new Fixture(); for (int i = 0; i < 5; i++) f.Add("file" + i, "same"); var result = new DuplicateScanner().Scan(f.Options(true, 3), CancellationToken.None, null); Assert(result.Limited && result.Entries == 3, "Limit."); });
        Test("network, device, traversing and empty scopes are refused", () =>
        {
            foreach (var path in new[] { @"\\server\share", @"\\?\C:\Temp", @"C:\a\..\b" }) Refused(() => new DuplicateOptions(new[] { path }, new string[0], true));
            Refused(() => new DuplicateOptions(new string[0], new string[0], true));
        });
        Test("hard links are skipped rather than counted as extra copies", () =>
        {
            var f = new Fixture(); var a = f.Add("a", "same"); var alias = Path.Combine(f.Root, "alias");
            if (!CreateHardLink(alias, a, IntPtr.Zero)) throw new Skip("Native hard-link fixture creation blocked, Windows error " + Marshal.GetLastWin32Error());
            Record("hardlink", alias); f.Add("b", "same"); var result = f.Scan(); Assert(result.Groups.Count == 0 && result.Reasons.ContainsKey("Hard-linked file"), "Hard links are not independent copies.");
        });
        Test("symbolic links are not followed outside the selected root", () =>
        {
            var f = new Fixture(); var outside = new Fixture(); var target = outside.Add("target", "same"); f.Add("inside", "same"); var link = Path.Combine(f.Root, "link");
            if (!CreateSymbolicLink(link, target, 2)) throw new Skip("Native symbolic-link fixture unavailable, Windows error " + Marshal.GetLastWin32Error());
            Record("symlink", link); Assert(f.Scan().Groups.Count == 0 && File.ReadAllText(target) == "same", "Outside target must not be followed or modified.");
        });
        Test("legacy cleanup and native write/deletion entry points are disabled", () =>
        {
            Refused(() => new CleanupService().Run(null, new Candidate[0], false, CancellationToken.None, null));
            Refused(() => new CleanupService().Run(null, new Candidate[0], true, CancellationToken.None, null));
            Refused(() => NativeFiles.Delete(null)); Refused(() => NativeFiles.OpenFile(@"C:\never-opened", true));
        });
        Test("old updater downloads are preserved regardless of age", () =>
        {
            var f = new Fixture(); var name = Guid.NewGuid().ToString("N");
            var executable = f.Add(name + "\\ThongKhongToolbox.exe", "Disposable updater fixture, preserve it.");
            var checksum = f.Add(name + "\\SHA256SUMS.txt", "Disposable checksum fixture, preserve it.");
            Directory.SetCreationTimeUtc(Path.GetDirectoryName(executable), DateTime.UtcNow.AddYears(-10));
            var before = Fingerprint(executable);
            Toolbox.ToolboxUpdater.RemoveOldDownloads(f.Root, DateTime.UtcNow);
            Assert(File.Exists(executable) && File.Exists(checksum) && Fingerprint(executable) == before, "Age must never cause automatic pruning.");
        });
        Test("duplicate UI has no age-based cleanup or delete controls", () =>
        {
            var view = new WinOptView(); var grid = (Grid)view.Content;
            foreach (var name in new[] { "Clean", "DryRun", "Mode", "WindowsTemp", "SelectAll" }) Assert(grid.FindName(name) == null, "Legacy control must not exist: " + name);
            Assert(!((Button)grid.FindName("Scan")).IsEnabled, "No automatic scan scope.");
            Assert(((DataGrid)grid.FindName("Matches")).IsReadOnly && !((DataGrid)grid.FindName("Matches")).CanUserDeleteRows, "Results are read-only.");
            Assert(((ListBox)grid.FindName("Folders")).Items.Count == 0, "No default user folders.");
        });
        Test("rounded tabs fit and wrap without clipping", () =>
        {
            var view = new WinOptView(); var root = (Grid)view.Content; var sections = (TabControl)root.FindName("Sections");
            foreach (var width in new[] { 960.0, 590.0, 300.0 })
            {
                Layout(view, width); var panel = Children<WrapPanel>(sections).First(p => p.IsItemsHost);
                foreach (TabItem header in sections.Items)
                {
                    var bounds = header.TransformToAncestor(panel).TransformBounds(new Rect(header.RenderSize));
                    Assert(bounds.Left >= -.1 && bounds.Right <= panel.ActualWidth + .1, "Tab boundary.");
                    var border = Children<Border>(header).Single(b => b.Name == "TabSurface");
                    Assert(border.TransformToAncestor(header).TransformBounds(new Rect(border.RenderSize)).Right <= header.ActualWidth + .1, "Rounded edge.");
                }
            }
        });
        Test("selecting a duplicate group shows its paths without removal choices", () =>
        {
            var f = new Fixture(); f.Add("a", "same"); f.Add("b", "same"); var result = f.Scan();
            var view = new WinOptView(); var root = (Grid)view.Content;
            ((TabControl)root.FindName("Sections")).SelectedIndex = 1;
            var groups = (ListBox)root.FindName("Groups"); groups.ItemsSource = result.Groups; groups.SelectedIndex = 0;
            Layout(view, 960);
            var matches = (DataGrid)root.FindName("Matches"); Assert(matches.Items.Count == 2, "Group members.");
            matches.SelectedIndex = 0;
            Assert(((TextBox)root.FindName("SelectedPath")).Text == ((DuplicateFile)matches.SelectedItem).Path, "Full-path review.");
            Assert(!Children<CheckBox>(matches).Any(), "No delete/keep selection checkboxes.");
        });
        Console.WriteLine(passed + " passed; " + failed + " failed; " + skipped + " skipped. No test deletes files. Fixtures retained at " + runRoot);
        return failed == 0 ? 0 : 1;
    }
}

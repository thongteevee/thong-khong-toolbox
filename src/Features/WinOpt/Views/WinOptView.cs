using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Toolbox.WinOpt
{
    internal sealed class CandidateRow : INotifyPropertyChanged
    {
        internal Candidate File { get; private set; }
        private bool selected;
        public bool Selected { get { return selected; } set { selected = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Selected")); } }
        public string Path { get { return File.Path; } }
        public string Category { get { return File.Category; } }
        public string Size { get { return File.Size; } }
        public long Bytes { get { return File.Bytes; } }
        public DateTime Modified { get { return File.Modified; } }
        public event PropertyChangedEventHandler PropertyChanged;
        internal CandidateRow(Candidate file) { File = file; }
    }

    internal sealed class WinOptView : UserControl
    {
        private readonly Grid root;
        private ObservableCollection<CandidateRow> rows = new ObservableCollection<CandidateRow>();
        private readonly MaintenanceLog log;
        private ScanResult preview;
        private CancellationTokenSource cancellation;
        private bool busy;
        private bool updatingSelection;
        internal bool IsWorking { get { return busy; } }
        internal void CancelOperation()
        { if (cancellation != null) cancellation.Cancel(); Status("Cancelling… Close again after the operation finishes."); }

        internal WinOptView()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WinOpt.xaml")) root = (Grid)XamlReader.Load(stream);
            Content = root; Background = root.Background; FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
            log = new MaintenanceLog(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThongKhongToolbox", "WinOpt", "Logs"));
            Get<DataGrid>("Candidates").ItemsSource = rows;
            Get<ComboBox>("Mode").SelectionChanged += delegate { InvalidatePreview(); };
            Get<CheckBox>("WindowsTemp").Checked += delegate { InvalidatePreview(); };
            Get<CheckBox>("WindowsTemp").Unchecked += delegate { InvalidatePreview(); };
            Get<TextBox>("Exclusions").TextChanged += delegate { InvalidatePreview(); };
            Get<Button>("Scan").Click += async delegate { await Scan(); };
            Get<Button>("DryRun").Click += async delegate { await Clean(true); };
            Get<Button>("Clean").Click += async delegate { await Clean(false); };
            Get<Button>("Cancel").Click += delegate { if (cancellation != null) { cancellation.Cancel(); Status("Cancelling… Completed work will be reported."); } };
            Get<Button>("SelectAll").Click += delegate { SetAll(true); };
            Get<Button>("SelectNone").Click += delegate { SetAll(false); };
            Get<Button>("RefreshDrives").Click += async delegate { await RefreshDrives(); };
            foreach (var action in new[] { "Optimize", "Storage", "Recommendations", "Recycle", "TaskManager", "StartupApps", "InstalledApps" })
            {
                var target = action;
                Get<Button>(action).Click += delegate
                {
                    try { WindowsStorage.Open(target); }
                    catch (Exception e) { MessageBox.Show(Window.GetWindow(this), "Windows could not open this page. " + e.GetBaseException().Message, "WinOpt", MessageBoxButton.OK, MessageBoxImage.Information); }
                };
            }
            Loaded += async delegate { Get<TextBlock>("Recent").Text = log.Recent(); await RefreshDrives(); };
            Get<TextBlock>("TempLocation").Text = "User temporary folder: " + System.IO.Path.GetTempPath();
            UpdateSelection();
        }

        private T Get<T>(string name) where T : FrameworkElement { return (T)root.FindName(name); }
        private void Status(string text) { Get<TextBlock>("Status").Text = text; }
        private void InvalidatePreview()
        {
            if (busy) return;
            preview = null; rows.Clear(); Get<TextBlock>("PreviewSummary").Text = "Scan to build a preview. Nothing is selected automatically.";
            Get<TextBlock>("Categories").Text = ""; Get<TextBlock>("Skipped").Text = ""; UpdateSelection();
        }
        private void UpdateSelection()
        {
            if (updatingSelection) return;
            var selected = rows.Where(r => r.Selected).ToArray();
            Get<TextBlock>("Selection").Text = selected.Length + " selected · " + StorageFormat.Bytes(selected.Sum(r => r.File.Bytes));
            var eligible = !busy && preview != null && !preview.Consumed && selected.Length > 0;
            Get<Button>("Clean").IsEnabled = eligible; Get<Button>("DryRun").IsEnabled = eligible;
            Get<Button>("SelectAll").IsEnabled = !busy && rows.Count > 0; Get<Button>("SelectNone").IsEnabled = !busy && rows.Count > 0;
        }
        private void SetAll(bool selected)
        {
            updatingSelection = true;
            try { foreach (var row in rows) row.Selected = selected; }
            finally { updatingSelection = false; UpdateSelection(); }
        }
        private async Task Operation(string message, Func<CancellationToken, Task> action)
        {
            if (busy) return;
            busy = true; cancellation = new CancellationTokenSource();
            foreach (var name in new[] { "Policy", "Scan", "Candidates", "RefreshDrives" }) Get<FrameworkElement>(name).IsEnabled = false;
            Get<Button>("Cancel").IsEnabled = true; Get<ProgressBar>("Progress").IsIndeterminate = true;
            UpdateSelection(); Status(message);
            try { await action(cancellation.Token); }
            catch (Exception e) { Status("Operation stopped: " + e.GetBaseException().Message); }
            finally
            {
                busy = false; cancellation.Dispose(); cancellation = null;
                foreach (var name in new[] { "Policy", "Scan", "Candidates", "RefreshDrives" }) Get<FrameworkElement>(name).IsEnabled = true;
                Get<Button>("Cancel").IsEnabled = false; Get<ProgressBar>("Progress").IsIndeterminate = false;
                Get<TextBlock>("Recent").Text = log.Recent(); UpdateSelection();
            }
        }
        private async Task Scan()
        {
            await Operation("Scanning temporary folders. No files are being deleted…", async token =>
            {
                preview = null; rows.Clear();
                var mode = (CleanupMode)Get<ComboBox>("Mode").SelectedIndex;
                var exclusions = Get<TextBox>("Exclusions").Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var policy = new ScanPolicy(mode, PathPolicy.ProductionRoots(Get<CheckBox>("WindowsTemp").IsChecked == true), exclusions, DateTime.UtcNow);
                var progress = new Progress<int>(count => Status("Scanning… " + count.ToString("N0") + " entries inspected. No files deleted."));
                preview = await Task.Run(() => new TempScanner().Scan(policy, token, progress));
                // Build once and swap the collection: per-item Add raises one UI notification per candidate.
                var built = preview.Files.Select(file => new CandidateRow(file)).ToList();
                foreach (var row in built) row.PropertyChanged += delegate { UpdateSelection(); };
                rows = new ObservableCollection<CandidateRow>(built);
                Get<DataGrid>("Candidates").ItemsSource = rows;
                var partial = preview.Cancelled ? "Cancelled · partial preview. " : preview.Limited ? "Scan limit reached · partial preview. " : "Scan complete. ";
                Get<TextBlock>("PreviewSummary").Text = partial + rows.Count.ToString("N0") + " eligible files · " + StorageFormat.Bytes(preview.Bytes) + " estimated. Preview valid until " + preview.Policy.CreatedUtc.Add(CleanupService.PreviewLifetime).ToLocalTime().ToString("t") + ".";
                Get<TextBlock>("Categories").Text = string.Join("   •   ", preview.Files.GroupBy(f => f.Category).Select(g => g.Key + ": " + g.Count() + " files / " + StorageFormat.Bytes(g.Sum(f => f.Bytes))));
                Get<TextBlock>("Skipped").Text = "Skipped entries: " + (preview.Reasons.Count == 0 ? "none" : StorageFormat.Reasons(preview.Reasons));
                var warning = log.Write("scan", rows.Count, preview.Reasons.Values.Sum(), 0, preview.Bytes, preview.Cancelled, preview.Reasons);
                Status(partial + "Review files and select what to clean. " + warning);
            });
        }
        private async Task Clean(bool dryRun)
        {
            var selected = rows.Where(r => r.Selected).Select(r => r.File).ToArray();
            if (busy || preview == null || selected.Length == 0) return;
            if (!dryRun && MessageBox.Show(Window.GetWindow(this),
                "Permanently delete " + selected.Length.ToString("N0") + " selected temporary files (" + StorageFormat.Bytes(selected.Sum(f => f.Bytes)) + " estimated)?\n\n" +
                "Files will not go to the Recycle Bin. There is no undo. Close installers and review the file list before continuing. Changed, protected and in-use files will be skipped.",
                "Confirm selected cleanup", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            var snapshot = preview;
            await Operation(dryRun ? "Checking selected files without deletion…" : "Cleaning selected files…", async token =>
            {
                var progress = new Progress<int>(count => Status((dryRun ? "Checking… " : "Cleaning… ") + count + " / " + selected.Length));
                var result = await Task.Run(() => new CleanupService().Run(snapshot, selected, dryRun, token, progress));
                var logWarning = log.Write(dryRun ? "dry-run" : "cleanup", result.Completed, result.Skipped, result.Failed, result.Bytes, result.Cancelled, result.Reasons);
                var summary = (result.Cancelled ? "Cancelled. " : "Complete. ") + result.Completed + (dryRun ? " files currently eligible" : " files deleted") +
                    " · " + StorageFormat.Bytes(result.Bytes) + (dryRun ? " estimated" : " logical bytes deleted") + " · " + result.Skipped + " skipped · " + result.Failed + " failed.";
                var observations = string.Join(Environment.NewLine, result.Volumes.Select(v => v.Volume + " free before: " + (v.Before.HasValue ? StorageFormat.Bytes(v.Before.Value) : "unavailable") +
                    " · after: " + (v.After.HasValue ? StorageFormat.Bytes(v.After.Value) : "unavailable") + " · observed change: " + (v.Delta.HasValue ? StorageFormat.Bytes(v.Delta.Value) : "unavailable")));
                Get<TextBlock>("Outcome").Text = summary + Environment.NewLine + observations + Environment.NewLine +
                    "Free-space changes include other disk activity and may differ from deleted bytes. " + StorageFormat.Reasons(result.Reasons) + " " + logWarning;
                if (!dryRun) { SetAll(false); Get<TextBlock>("PreviewSummary").Text = "Preview used. Scan again before further cleanup."; }
                Get<TabControl>("Sections").SelectedIndex = 2;
                Status(summary + (dryRun ? " No files deleted." : " Scan again to refresh the preview."));
            });
        }
        private async Task RefreshDrives()
        {
            await Operation("Reading local drive capacity and media information…", async token =>
            {
                var drives = await Task.Run(() => WindowsStorage.ReadDrives(token));
                Get<ItemsControl>("Drives").ItemsSource = drives;
                Status(token.IsCancellationRequested ? "Drive refresh cancelled." : "Drive information refreshed. Unknown means Windows could not provide a reliable media mapping.");
            });
        }
    }
}

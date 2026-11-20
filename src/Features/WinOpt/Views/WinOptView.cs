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
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;

namespace Toolbox.WinOpt
{
    internal sealed class CandidateRow : INotifyPropertyChanged
    {
        internal Candidate File { get; private set; }
        private bool selected;
        public bool Selected { get { return selected; } set { selected = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Selected")); } }
        public string Path { get { return File.Path; } }
        public string Name { get { return System.IO.Path.GetFileName(File.Path); } }
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
        private bool changingAdvanced;
        private bool Advanced { get { return Tag is bool && (bool)Tag; } }
        internal bool IsWorking { get { return busy; } }
        internal void CancelOperation()
        { if (cancellation != null) cancellation.Cancel(); Status("Cancelling… Close again after the operation finishes."); }

        internal WinOptView()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WinOpt.xaml")) root = (Grid)XamlReader.Load(stream);
            Content = root; Background = root.Background; FontFamily = new System.Windows.Media.FontFamily("Segoe UI"); Tag = false;
            log = new MaintenanceLog(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThongKhongToolbox", "WinOpt", "Logs"));
            Get<DataGrid>("Candidates").ItemsSource = rows;
            Get<CheckBox>("AdvancedMode").Checked += delegate { ChangeAdvancedMode(); };
            Get<CheckBox>("AdvancedMode").Unchecked += delegate { ChangeAdvancedMode(); };
            Get<Button>("OpenCleanup").Click += delegate { Get<TabControl>("Sections").SelectedIndex = 1; };
            Get<Button>("ExpandFiles").Click += delegate { CreateFileReview().ShowDialog(); };
            Get<Thumb>("FileListGrip").DragDelta += delegate(object sender, DragDeltaEventArgs args)
            {
                var row = (RowDefinition)root.FindName("FileListRow");
                row.Height = new GridLength(Math.Max(400, row.Height.Value + args.VerticalChange));
            };
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
        private void ChangeAdvancedMode()
        {
            if (changingAdvanced) return;
            var toggle = Get<CheckBox>("AdvancedMode");
            if (busy)
            {
                changingAdvanced = true;
                toggle.IsChecked = Advanced;
                changingAdvanced = false;
                return;
            }
            Tag = toggle.IsChecked == true;
            if (!Advanced)
            {
                Get<ComboBox>("Mode").SelectedIndex = 0;
                Get<CheckBox>("WindowsTemp").IsChecked = false;
            }
            var grid = Get<DataGrid>("Candidates");
            ((DataGridTextColumn)grid.Columns[1]).Binding = new Binding(Advanced ? "Path" : "Name");
            grid.Columns[2].Visibility = grid.Columns[4].Visibility = Advanced ? Visibility.Visible : Visibility.Collapsed;
            InvalidatePreview();
        }

        internal Window CreateFileReview()
        {
            var source = Get<DataGrid>("Candidates");
            var grid = new DataGrid { ItemsSource = rows, Style = (Style)root.FindResource(typeof(DataGrid)), MinHeight = 400, ColumnHeaderHeight = 34 };
            foreach (var column in source.Columns)
            {
                var template = column as DataGridTemplateColumn;
                var text = column as DataGridTextColumn;
                DataGridColumn copy = template != null ? (DataGridColumn)new DataGridTemplateColumn { CellTemplate = template.CellTemplate } :
                    new DataGridTextColumn { Binding = text.Binding, ElementStyle = text.ElementStyle, IsReadOnly = true };
                copy.Header = column.Header; copy.Width = column.Width; copy.MinWidth = column.MinWidth;
                copy.Visibility = column.Visibility; copy.SortMemberPath = column.SortMemberPath;
                grid.Columns.Add(copy);
            }
            var panel = new DockPanel { Margin = new Thickness(16) };
            var done = new Button { Content = "Done", Style = (Style)root.FindResource(typeof(Button)), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            DockPanel.SetDock(done, Dock.Bottom); panel.Children.Add(done); panel.Children.Add(grid);
            var area = SystemParameters.WorkArea;
            var window = new Window { Title = "Temporary files", Content = panel, Width = Math.Min(1200, area.Width * .94), Height = Math.Min(820, area.Height * .9),
                MinWidth = Math.Min(600, area.Width * .94), MinHeight = Math.Min(520, area.Height * .9), FontFamily = FontFamily, FontSize = 14,
                ResizeMode = ResizeMode.CanResize, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            foreach (var key in new[] { "WindowFill", "SurfaceFill", "Ink", "Muted", "ControlLine", "ControlHover", "PrimaryFill", "PrimaryInk", "Line" })
            {
                var resource = TryFindResource(key);
                if (resource != null) window.Resources[key] = resource;
            }
            window.SetResourceReference(Window.BackgroundProperty, "WindowFill");
            window.SetResourceReference(Window.ForegroundProperty, "Ink");
            var owner = Window.GetWindow(this);
            if (owner != null) window.Owner = owner;
            done.Click += delegate { window.Close(); };
            return window;
        }
        private void Status(string text) { Get<TextBlock>("Status").Text = text; }
        private void InvalidatePreview()
        {
            if (busy) return;
            preview = null; rows.Clear(); Get<TextBlock>("PreviewSummary").Text = "Scan to find temporary files.";
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
            foreach (var name in new[] { "Policy", "Scan", "Candidates", "RefreshDrives", "AdvancedMode", "ExpandFiles", "FileListGrip" }) Get<FrameworkElement>(name).IsEnabled = false;
            Get<Button>("Cancel").IsEnabled = true; Get<ProgressBar>("Progress").Value = 0; Get<ProgressBar>("Progress").IsIndeterminate = true;
            UpdateSelection(); Status(message);
            var complete = false;
            try { await action(cancellation.Token); complete = !cancellation.IsCancellationRequested; }
            catch (Exception e) { Status("Operation stopped: " + e.GetBaseException().Message); }
            finally
            {
                busy = false; cancellation.Dispose(); cancellation = null;
                foreach (var name in new[] { "Policy", "Scan", "Candidates", "RefreshDrives", "AdvancedMode", "ExpandFiles", "FileListGrip" }) Get<FrameworkElement>(name).IsEnabled = true;
                Get<Button>("Cancel").IsEnabled = false; Get<ProgressBar>("Progress").IsIndeterminate = false;
                Get<ProgressBar>("Progress").Value = complete ? 100 : 0;
                Get<TextBlock>("Recent").Text = log.Recent(); UpdateSelection();
            }
        }
        private async Task Scan()
        {
            await Operation("Scanning…", async token =>
            {
                preview = null; rows.Clear();
                var mode = Advanced ? (CleanupMode)Get<ComboBox>("Mode").SelectedIndex : CleanupMode.Safe;
                var exclusions = Get<TextBox>("Exclusions").Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var policy = new ScanPolicy(mode, PathPolicy.ProductionRoots(Advanced && Get<CheckBox>("WindowsTemp").IsChecked == true), exclusions, DateTime.UtcNow);
                var progress = new Progress<int>(count => Status("Scanning… " + count.ToString("N0") + " files checked"));
                preview = await Task.Run(() => new TempScanner().Scan(policy, token, progress));
                // Build once and swap the collection: per-item Add raises one UI notification per candidate.
                var built = preview.Files.Select(file => new CandidateRow(file)).ToList();
                foreach (var row in built) row.PropertyChanged += delegate { UpdateSelection(); };
                rows = new ObservableCollection<CandidateRow>(built);
                Get<DataGrid>("Candidates").ItemsSource = rows;
                var partial = preview.Cancelled ? "Cancelled · partial results. " : preview.Limited ? "Partial results. " : "";
                Get<TextBlock>("PreviewSummary").Text = partial + rows.Count.ToString("N0") + " files · " + StorageFormat.Bytes(preview.Bytes);
                Get<TextBlock>("Categories").Text = string.Join("   •   ", preview.Files.GroupBy(f => f.Category).Select(g => g.Key + ": " + g.Count() + " files / " + StorageFormat.Bytes(g.Sum(f => f.Bytes))));
                Get<TextBlock>("Skipped").Text = "Skipped: " + (preview.Reasons.Count == 0 ? "none" : StorageFormat.Reasons(preview.Reasons)) +
                    " · Rescan after " + preview.Policy.CreatedUtc.Add(CleanupService.PreviewLifetime).ToLocalTime().ToString("t");
                var warning = log.Write("scan", rows.Count, preview.Reasons.Values.Sum(), 0, preview.Bytes, preview.Cancelled, preview.Reasons);
                Status(partial + "Select files to clean. " + warning);
            });
        }
        private async Task Clean(bool dryRun)
        {
            var selected = rows.Where(r => r.Selected).Select(r => r.File).ToArray();
            if (busy || preview == null || selected.Length == 0) return;
            if (!dryRun && MessageBox.Show(Window.GetWindow(this),
                "Delete " + selected.Length.ToString("N0") + " temporary files (" + StorageFormat.Bytes(selected.Sum(f => f.Bytes)) + ")?\n\nPermanent deletion. This cannot be undone.",
                "Confirm selected cleanup", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            var snapshot = preview;
            await Operation(dryRun ? "Checking selected files without deletion…" : "Cleaning selected files…", async token =>
            {
                var progress = new Progress<int>(count => Status((dryRun ? "Checking… " : "Cleaning… ") + count + " / " + selected.Length));
                var result = await Task.Run(() => new CleanupService().Run(snapshot, selected, dryRun, token, progress));
                var logWarning = log.Write(dryRun ? "dry-run" : "cleanup", result.Completed, result.Skipped, result.Failed, result.Bytes, result.Cancelled, result.Reasons);
                var summary = (result.Cancelled ? "Cancelled · " : "") + result.Completed + (dryRun ? " files ready" : " files deleted") +
                    " · " + StorageFormat.Bytes(result.Bytes) + (result.Skipped > 0 ? " · " + result.Skipped + " skipped" : "") + (result.Failed > 0 ? " · " + result.Failed + " failed" : "");
                var observations = string.Join(Environment.NewLine, result.Volumes.Select(v => v.Volume + " free before: " + (v.Before.HasValue ? StorageFormat.Bytes(v.Before.Value) : "unavailable") +
                    " · after: " + (v.After.HasValue ? StorageFormat.Bytes(v.After.Value) : "unavailable") + " · observed change: " + (v.Delta.HasValue ? StorageFormat.Bytes(v.Delta.Value) : "unavailable")));
                Get<TextBlock>("Outcome").Text = summary;
                Get<TextBlock>("OutcomeDetails").Text = observations + Environment.NewLine +
                    "Free-space changes include other disk activity and may differ from deleted bytes. " + StorageFormat.Reasons(result.Reasons) + " " + logWarning;
                if (!dryRun) { SetAll(false); Get<TextBlock>("PreviewSummary").Text = "Scan again to refresh."; }
                Get<TabControl>("Sections").SelectedIndex = 2;
                Status(summary + (dryRun ? " · No files deleted" : "") + (String.IsNullOrEmpty(logWarning) ? "" : " · " + logWarning));
            });
        }
        private async Task RefreshDrives()
        {
            await Operation("Loading drives…", async token =>
            {
                var drives = await Task.Run(() => WindowsStorage.ReadDrives(token));
                Get<ItemsControl>("Drives").ItemsSource = drives;
                Status(token.IsCancellationRequested ? "Cancelled" : "Drives updated");
            });
        }
    }
}

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Forms = System.Windows.Forms;

namespace Toolbox.WinOpt
{
    internal sealed class WinOptView : UserControl
    {
        private readonly Grid root;
        private readonly ObservableCollection<string> folders = new ObservableCollection<string>();
        private CancellationTokenSource cancellation;
        private bool busy;
        internal bool IsWorking { get { return busy; } }
        internal void CancelOperation()
        { if (cancellation != null) cancellation.Cancel(); Status("Cancelling the read-only scan…"); }

        internal WinOptView()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WinOpt.xaml")) root = (Grid)XamlReader.Load(stream);
            Content = root; Background = root.Background;
            Get<ListBox>("Folders").ItemsSource = folders;
            Get<Button>("AddFolder").Click += delegate { ChooseFolder(); };
            Get<Button>("UnselectFolder").Click += delegate
            {
                var selected = Get<ListBox>("Folders").SelectedItem as string;
                if (selected != null) { folders.Remove(selected); ClearResults(); }
            };
            Get<Button>("OpenDuplicates").Click += delegate { Get<TabControl>("Sections").SelectedIndex = 1; };
            Get<Button>("Scan").Click += async delegate { await FindDuplicates(); };
            Get<Button>("Cancel").Click += delegate { CancelOperation(); };
            Get<TextBox>("Exclusions").TextChanged += delegate { if (!busy) ClearResults(); };
            Get<CheckBox>("Subfolders").Checked += delegate { if (!busy) ClearResults(); };
            Get<CheckBox>("Subfolders").Unchecked += delegate { if (!busy) ClearResults(); };
            Get<ListBox>("Groups").SelectionChanged += delegate
            {
                var group = Get<ListBox>("Groups").SelectedItem as DuplicateGroup;
                Get<DataGrid>("Matches").ItemsSource = group == null ? null : group.Files;
                Get<TextBlock>("GroupDescription").Text = group == null ? "Choose a matching group to review its files." : group.Description;
                Get<TextBox>("SelectedPath").Text = "";
            };
            Get<DataGrid>("Matches").SelectionChanged += delegate
            {
                var file = Get<DataGrid>("Matches").SelectedItem as DuplicateFile;
                Get<TextBox>("SelectedPath").Text = file == null ? "" : file.Path;
            };
            Get<Button>("RefreshDrives").Click += async delegate
            {
                await Operation("Reading drive information…", async token =>
                {
                    Get<ItemsControl>("Drives").ItemsSource = await Task.Run(() => WindowsStorage.ReadDrives(token));
                    Status(token.IsCancellationRequested ? "Drive refresh cancelled." : "Drive information refreshed. No files changed.");
                });
            };
            foreach (var action in new[] { "TaskManager", "StartupApps", "InstalledApps" })
            {
                var target = action;
                Get<Button>(action).Click += delegate
                {
                    try { WindowsStorage.Open(target); }
                    catch (Exception e) { Status("Windows could not open this tool: " + e.GetBaseException().Message); }
                };
            }
            UpdateControls();
        }
        private T Get<T>(string name) where T : FrameworkElement { return (T)root.FindName(name); }
        private void Status(string value) { Get<TextBlock>("Status").Text = value; }
        private void ChooseFolder()
        {
            if (busy) return;
            using (var picker = new Forms.FolderBrowserDialog { Description = "Choose a folder for read-only duplicate discovery", ShowNewFolderButton = false })
            {
                if (picker.ShowDialog() != Forms.DialogResult.OK) return;
                try
                {
                    var path = PathPolicy.Canonical(picker.SelectedPath);
                    if (!folders.Contains(path, StringComparer.OrdinalIgnoreCase)) folders.Add(path);
                    ClearResults();
                }
                catch (Exception e) { Status("Folder could not be selected: " + e.GetBaseException().Message); }
            }
        }
        private void ClearResults()
        {
            Get<ListBox>("Groups").ItemsSource = null; Get<DataGrid>("Matches").ItemsSource = null;
            Get<TextBox>("SelectedPath").Text = "";
            Get<TextBlock>("GroupDescription").Text = "Choose a matching group to review its files.";
            Get<TextBlock>("Summary").Text = "Choose folders, then find duplicate contents. No file is selected for removal.";
            Get<TextBlock>("Outcome").Text = "No duplicate scan yet.";
            UpdateControls();
        }
        private void UpdateControls()
        {
            Get<FrameworkElement>("Scope").IsEnabled = !busy;
            Get<Button>("Scan").IsEnabled = !busy && folders.Count > 0;
            Get<Button>("RefreshDrives").IsEnabled = !busy;
            Get<Button>("Cancel").IsEnabled = busy;
        }
        private async Task Operation(string message, Func<CancellationToken, Task> action)
        {
            if (busy) return;
            busy = true; cancellation = new CancellationTokenSource(); UpdateControls(); Status(message);
            Get<ProgressBar>("Progress").IsIndeterminate = true;
            try { await action(cancellation.Token); }
            catch (Exception e) { Status("Scan stopped: " + e.GetBaseException().Message + " No files were changed."); }
            finally
            {
                busy = false; cancellation.Dispose(); cancellation = null;
                Get<ProgressBar>("Progress").IsIndeterminate = false; UpdateControls();
            }
        }
        private async Task FindDuplicates()
        {
            if (busy || folders.Count == 0) return;
            ClearResults();
            await Operation("Discovering files. Read-only…", async token =>
            {
                var exclusions = Get<TextBox>("Exclusions").Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var options = new DuplicateOptions(folders, exclusions, Get<CheckBox>("Subfolders").IsChecked == true);
                var progress = new Progress<DuplicateProgress>(p => Status(p.Phase + " · " + p.Entries.ToString("N0") + " entries · " + p.Hashed.ToString("N0") + " files hashed"));
                var result = await Task.Run(() => new DuplicateScanner().Scan(options, token, progress));
                Get<ListBox>("Groups").ItemsSource = result.Groups;
                if (result.Groups.Count > 0) Get<ListBox>("Groups").SelectedIndex = 0;
                var partial = result.Cancelled ? "Cancelled · partial results. " : result.Limited ? "Entry limit reached · partial results. " : "Scan complete. ";
                var summary = partial + result.Groups.Count.ToString("N0") + " duplicate-content groups · " + result.Groups.Sum(g => g.Count).ToString("N0") + " matching files. No files changed.";
                Get<TextBlock>("Summary").Text = summary;
                Get<TextBlock>("Outcome").Text = summary + Environment.NewLine + result.Entries.ToString("N0") + " entries inspected; " + result.Hashed.ToString("N0") + " files hashed." +
                    Environment.NewLine + "Skipped: " + (result.Reasons.Count == 0 ? "none" : string.Join(" · ", result.Reasons.OrderBy(p => p.Key).Select(p => p.Key + ": " + p.Value))) +
                    Environment.NewLine + "Content equality was verified while scanning. Files can change afterward. Matching content is not permission to remove a file.";
                Status(summary);
            });
        }
    }
}
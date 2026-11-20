using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Toolbox;
using Toolbox.WinOpt;

internal static class LayoutChecks
{
    // These tests construct disconnected WPF component trees. They never show a
    // window, send input, scan production temp folders, or open Windows tools.
    private static void Layout(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));
        element.UpdateLayout();
    }
    private static IEnumerable<T> Children<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T) yield return (T)child;
            foreach (var descendant in Children<T>(child)) yield return descendant;
        }
    }
    internal static void TabsAndInitialState()
    {
        var view = new WinOptView();
        var root = (Grid)view.Content;
        var sections = (TabControl)root.FindName("Sections");
        foreach (double width in new[] { 960.0, 590.0, 280.0 })
        {
            Layout(view, width, 680);
            var panel = Children<WrapPanel>(sections).First(p => p.IsItemsHost);
            var headers = sections.Items.Cast<TabItem>().ToArray();
            foreach (var header in headers)
            {
                var bounds = header.TransformToAncestor(panel).TransformBounds(new Rect(header.RenderSize));
                if (bounds.Left < -.1 || bounds.Right > panel.ActualWidth + .1)
                    throw new Exception("Tab header exceeds its panel at width " + width);
                var border = Children<Border>(header).Single(b => b.Name == "TabSurface");
                var borderBounds = border.TransformToAncestor(header).TransformBounds(new Rect(border.RenderSize));
                if (borderBounds.Right > header.ActualWidth + .1 || borderBounds.Bottom > header.ActualHeight + .1)
                    throw new Exception("Rounded tab border is cropped at width " + width);
            }
            if (width == 280 && headers[2].TranslatePoint(new Point(0, 0), panel).Y <= 0)
                throw new Exception("Narrow tabs must wrap.");
        }
        sections.SelectedIndex = 1; Layout(view, 850, 680);
        if (((Button)root.FindName("Clean")).IsEnabled || ((Button)root.FindName("DryRun")).IsEnabled)
            throw new Exception("Cleanup must be disabled before a preview and selection.");
        if (!((Button)root.FindName("Scan")).IsEnabled) throw new Exception("Scan should initially be enabled.");
    }

    internal static void SimpleModeAndFileList()
    {
        var view = new WinOptView();
        var root = (Grid)view.Content;
        var sections = (TabControl)root.FindName("Sections");
        sections.SelectedIndex = 1; Layout(view, 1000, 750);
        var toggle = (CheckBox)root.FindName("AdvancedMode");
        var policy = (StackPanel)root.FindName("AdvancedPolicy");
        var grid = (DataGrid)root.FindName("Candidates");
        if (toggle.IsChecked == true || policy.Visibility != Visibility.Collapsed || grid.Columns[2].Visibility != Visibility.Collapsed)
            throw new Exception("Default UI exposed advanced options.");
        var rows = (ObservableCollection<CandidateRow>)typeof(WinOptView).GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
        var fixture = new TempRoot(System.IO.Path.GetFullPath("dist\\WinOptTests\\display-fixtures"), "Fixture");
        for (int index = 0; index < 20; index++) rows.Add(new CandidateRow(new Candidate(System.IO.Path.Combine(fixture.Path, "sample-" + index + ".tmp"), fixture,
            new FileStamp { Length = 1024, Links = 1, CreatedUtc = DateTime.UtcNow.AddDays(-10), ModifiedUtc = DateTime.UtcNow.AddDays(-10) })));
        Layout(view, 1000, 750);
        var scroll = ((Grid)((TabItem)sections.Items[1]).Content).Children.OfType<ScrollViewer>().Single();
        var visible = Children<DataGridRow>(grid).Count(row => {
            var bounds = row.TransformToAncestor(grid).TransformBounds(new Rect(row.RenderSize));
            var viewport = row.TransformToAncestor(scroll).TransformBounds(new Rect(row.RenderSize));
            return bounds.Top >= grid.ColumnHeaderHeight && bounds.Bottom <= grid.ActualHeight && viewport.Top >= 0 && viewport.Bottom <= scroll.ActualHeight;
        });
        if (visible < 10) throw new Exception("File list must fit ten complete rows; fitted " + visible);
        var clean = (Button)root.FindName("Clean");
        var cleanBounds = clean.TransformToAncestor(view).TransformBounds(new Rect(clean.RenderSize));
        if (cleanBounds.Bottom > view.ActualHeight || cleanBounds.Top < 0) throw new Exception("Clean action was clipped.");
        var listRow = (RowDefinition)root.FindName("FileListRow");
        ((Thumb)root.FindName("FileListGrip")).RaiseEvent(new DragDeltaEventArgs(0, 120) { RoutedEvent = Thumb.DragDeltaEvent });
        if (listRow.Height.Value != 520) throw new Exception("File list did not grow when dragged.");
        ((Thumb)root.FindName("FileListGrip")).RaiseEvent(new DragDeltaEventArgs(0, -1000) { RoutedEvent = Thumb.DragDeltaEvent });
        if (listRow.Height.Value < 400) throw new Exception("File list shrank below ten rows.");
        var review = view.CreateFileReview();
        var reviewGrid = Children<DataGrid>((DependencyObject)review.Content).Single();
        if (review.ResizeMode != ResizeMode.CanResize || !Object.ReferenceEquals(reviewGrid.ItemsSource, grid.ItemsSource))
            throw new Exception("Expanded review must be resizable and preserve selections.");
        var same = reviewGrid.Items.Cast<CandidateRow>().First(); same.Selected = true;
        if (!rows[0].Selected) throw new Exception("Expanded review did not share selection state.");
        review.Close();
        toggle.IsChecked = true; Layout(view, 1000, 800);
        if (policy.Visibility != Visibility.Visible || grid.Columns[2].Visibility != Visibility.Visible)
            throw new Exception("Advanced mode did not reveal detailed options.");
        ((ComboBox)root.FindName("Mode")).SelectedIndex = 2;
        ((CheckBox)root.FindName("WindowsTemp")).IsChecked = true;
        ((TextBox)root.FindName("Exclusions")).Text = fixture.Path;
        var previewField = typeof(WinOptView).GetField("preview", BindingFlags.Instance | BindingFlags.NonPublic);
        previewField.SetValue(view, new ScanResult(null, new List<Candidate>(), new Dictionary<string, int>(), false, false));
        toggle.IsChecked = false; Layout(view, 1000, 800);
        if (((ComboBox)root.FindName("Mode")).SelectedIndex != 0 || ((CheckBox)root.FindName("WindowsTemp")).IsChecked == true ||
            previewField.GetValue(view) != null || rows.Count != 0 || ((TextBox)root.FindName("Exclusions")).Text != fixture.Path)
            throw new Exception("Simple mode did not restore Safe policy, invalidate preview, and retain exclusions.");
    }

    internal static void CompletedProgress()
    {
        var view = new WinOptView();
        var root = (Grid)view.Content;
        var progress = (ProgressBar)root.FindName("Progress");
        var operation = typeof(WinOptView).GetMethod("Operation", BindingFlags.Instance | BindingFlags.NonPublic);
        Func<CancellationToken, Task> success = token => {
            if (!progress.IsIndeterminate || progress.Value != 0 || ((CheckBox)root.FindName("AdvancedMode")).IsEnabled)
                throw new Exception("Running state did not reset progress and lock policy.");
            return Task.FromResult(0);
        };
        ((Task)operation.Invoke(view, new object[] { "Fixture", success })).GetAwaiter().GetResult();
        if (progress.IsIndeterminate || progress.Value != 100) throw new Exception("Completed operation must leave a full progress bar.");
        Func<CancellationToken, Task> cancelled = token => { view.CancelOperation(); return Task.FromResult(0); };
        ((Task)operation.Invoke(view, new object[] { "Fixture", cancelled })).GetAwaiter().GetResult();
        if (progress.IsIndeterminate || progress.Value == 100) throw new Exception("Cancelled operation was marked complete.");
        Func<CancellationToken, Task> failed = token => { throw new InvalidOperationException("Fixture failure"); };
        ((Task)operation.Invoke(view, new object[] { "Fixture", failed })).GetAwaiter().GetResult();
        if (progress.IsIndeterminate || progress.Value == 100) throw new Exception("Failed operation was marked complete.");
    }

    internal static void SortEmptyAndCopy()
    {
        var view = new WinOptView();
        var root = (Grid)view.Content;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var update = typeof(WinOptView).GetMethod("UpdateSelection", flags);
        var preview = typeof(WinOptView).GetField("preview", flags);
        var rows = (ObservableCollection<CandidateRow>)typeof(WinOptView).GetField("rows", flags).GetValue(view);
        if (((Border)root.FindName("EmptyFiles")).Visibility != Visibility.Visible ||
            !((TextBlock)root.FindName("EmptyFilesText")).Text.Contains("Scan")) throw new Exception("Initial empty list did not suggest scanning.");
        var fixture = new TempRoot(System.IO.Path.GetFullPath("dist\\WinOptTests\\display-fixtures"), "Fixture");
        foreach (var bytes in new[] { 9L, 1000L, 20L }) rows.Add(new CandidateRow(new Candidate(System.IO.Path.Combine(fixture.Path, bytes + ".tmp"), fixture,
            new FileStamp { Length = bytes, Links = 1, CreatedUtc = DateTime.UtcNow.AddDays(-10), ModifiedUtc = DateTime.UtcNow.AddDays(-10) })));
        update.Invoke(view, null);
        var grid = (DataGrid)root.FindName("Candidates");
        var ordered = CollectionViewSource.GetDefaultView(grid.ItemsSource).Cast<CandidateRow>().Select(row => row.Bytes).ToArray();
        if (!ordered.SequenceEqual(new[] { 1000L, 20L, 9L }) || grid.Columns[3].SortDirection != ListSortDirection.Descending)
            throw new Exception("Preview did not sort numeric file sizes largest first.");
        if (((Border)root.FindName("EmptyFiles")).Visibility != Visibility.Collapsed) throw new Exception("Empty overlay hid populated results.");
        rows.Clear();
        preview.SetValue(view, new ScanResult(null, new List<Candidate>(), new Dictionary<string, int>(), false, false));
        update.Invoke(view, null);
        if (((TextBlock)root.FindName("EmptyFilesText")).Text != "Nothing to clean") throw new Exception("Successful empty scan was not explained.");
        preview.SetValue(view, new ScanResult(null, new List<Candidate>(), new Dictionary<string, int>(), true, false));
        update.Invoke(view, null);
        if (((TextBlock)root.FindName("EmptyFilesText")).Text == "Nothing to clean") throw new Exception("Cancelled scan was mistaken for a clean system.");

        string copied = null;
        view.CopyLatestResult(text => copied = text);
        if (copied != null) throw new Exception("Copy allowed an absent result.");
        ((TextBlock)root.FindName("Outcome")).Text = "2 files deleted · 20 KiB";
        ((TextBlock)root.FindName("OutcomeDetails")).Text = "Fixture volume details";
        ((Button)root.FindName("CopyOutcome")).IsEnabled = true;
        view.CopyLatestResult(text => copied = text);
        if (copied != "2 files deleted · 20 KiB") throw new Exception("Simple mode copied extra technical detail.");
        ((CheckBox)root.FindName("AdvancedMode")).IsChecked = true;
        view.CopyLatestResult(text => copied = text);
        if (!copied.Contains("Fixture volume details")) throw new Exception("Advanced copy omitted the displayed details.");
        view.CopyLatestResult(text => { throw new ExternalException("Fixture busy clipboard"); });
        if (!((TextBlock)root.FindName("Status")).Text.Contains("Clipboard busy")) throw new Exception("Busy clipboard was not handled.");
    }

    internal static void CatalogSwitchAndCommands()
    {
        Grid root;
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Toolbox.xaml")) root = (Grid)XamlReader.Load(stream);
        root.Resources["AppTileSize"] = 168.0; root.Resources["RailExpanded"] = false;
        var catalog = (ItemsControl)root.FindName("AppCatalog");
        int activated = 0;
        catalog.ItemsSource = new[] { new { Name = "WinOpt", Detail = "Fixture app", Icon = "x", Open = new ToolCommand(() => activated++) } };
        Layout(root, 1100, 800);
        if (!Children<WrapPanel>(catalog).Any(p => p.IsItemsHost)) throw new Exception("Grid mode must use wrapping tiles.");
        var grid = (RadioButton)root.FindName("AppGridView");
        var list = (RadioButton)root.FindName("AppListView");
        list.IsChecked = true; Layout(root, 1100, 800);
        if (Children<WrapPanel>(catalog).Any(p => p.IsItemsHost) || !Children<StackPanel>(catalog).Any(p => p.IsItemsHost))
            throw new Exception("List mode must switch to stacked rows.");
        var button = Children<Button>(catalog).Single(b => b.Name == "AppCard");
        if (button.Command == null || !button.Command.CanExecute(null)) throw new Exception("App tile command did not bind.");
        button.Command.Execute(null);
        if (activated != 1) throw new Exception("App tile must activate exactly once.");
        grid.IsChecked = true; Layout(root, 1100, 800);
        if (!Children<WrapPanel>(catalog).Any(p => p.IsItemsHost)) throw new Exception("Returning to Grid must restore tiles.");
        var header = (WrapPanel)root.FindName("AppsHeader");
        var title = (TextBlock)root.FindName("AppsTitle");
        var toggle = (Border)root.FindName("AppViewSwitch");
        if (toggle.TranslatePoint(new Point(0, 0), header).X - title.ActualWidth > 25)
            throw new Exception("View switch should stay beside Apps.");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
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
        foreach (double width in new[] { 960.0, 590.0, 400.0 })
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
            if (width == 400 && headers[2].TranslatePoint(new Point(0, 0), panel).Y <= 0)
                throw new Exception("Narrow tabs must wrap.");
        }
        sections.SelectedIndex = 1; Layout(view, 850, 680);
        if (((Button)root.FindName("Clean")).IsEnabled || ((Button)root.FindName("DryRun")).IsEnabled)
            throw new Exception("Cleanup must be disabled before a preview and selection.");
        if (!((Button)root.FindName("Scan")).IsEnabled) throw new Exception("Scan should initially be enabled.");
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

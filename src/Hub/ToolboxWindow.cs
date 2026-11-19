using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Toolbox
{
    internal sealed partial class ToolboxWindow : Window
    {
        private readonly Grid root;
        private bool busy;
        private bool googleConnected;
        private bool slingConnected;
        private readonly HubAccounts accounts;
        private static readonly string[] HubViews = { "Home", "Sling", "Settings" };

        internal ToolboxWindow(double? textScale = null, HubAccounts accountStore = null)
        {
            accounts = accountStore ?? new HubAccounts(LocalData.ToolboxFolder);
            settings = LocalData.Load("settings", new Settings());
            if (settings.Version < Settings.CurrentVersion)
            {
                settings.Demo = false;
                settings.Version = Settings.CurrentVersion;
            }
            rules = new ObservableCollection<Rule>(settings.Rules);
            Title = "Thong Khong Toolbox";
            using (var icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("Toolbox.ico"))
                Icon = BitmapFrame.Create(icon, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var available = SystemParameters.WorkArea;
            Width = Math.Min(1160, available.Width * .94);
            Height = Math.Min(840, available.Height * .93);
            MinWidth = Math.Min(640, Width);
            MinHeight = Math.Min(470, Height);
            using (var layout = Assembly.GetExecutingAssembly().GetManifestResourceStream("Toolbox.xaml"))
                root = (Grid)XamlReader.Load(layout);
            Background = root.Background;
            root.Resources["AppTileSize"] = 168.0;
            root.Resources["RailExpanded"] = false;

            var scale = textScale ?? WindowsTextScale();
            if (scale != 1) root.LayoutTransform = new ScaleTransform(scale, scale);
            var shell = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            root.Height = 440;
            shell.SizeChanged += delegate
            {
                var minimum = shell.ActualWidth / scale < 420 ? 570 : 440;
                root.Height = Math.Max(minimum, shell.ActualHeight / scale);
            };
            Content = shell;
            var legend = Get<WrapPanel>("Legend");
            schedule = new WeekTimeline(Get<ContentControl>("ScheduleHost"), legend);
            resultSchedule = new WeekTimeline(Get<ContentControl>("ResultScheduleHost"), Get<WrapPanel>("ResultLegend"), true);
            root.SizeChanged += delegate { AdaptLayout(root.ActualWidth); };
            Get<Border>("ToolRail").SizeChanged += delegate(object sender, SizeChangedEventArgs args)
            {
                if (args.WidthChanged) AdaptLayout(root.ActualWidth);
            };
            foreach (var page in HubViews)
            {
                var target = page;
                Get<RadioButton>(target + "Nav").Checked += async delegate
                {
                    ShowHubPage(target);
                    if (target == "Sling" && slingView == "Week") await RefreshWeek();
                };
            }
            var apps = Get<ItemsControl>("AppCatalog");
            apps.ItemsSource = new[] {
                new { Name = "Sling Shift Auto Apply", Detail = "Preview and apply for shifts around your calendar.",
                    Icon = "\uE787", Page = "Sling" }
            };
            apps.AddHandler(Button.ClickEvent, new RoutedEventHandler(delegate(object sender, RoutedEventArgs args)
            {
                var button = args.OriginalSource as Button;
                var page = button == null ? null : button.Tag as string;
                if (page != null) Get<RadioButton>(page + "Nav").IsChecked = true;
            }));
            Get<Button>("OpenSettings").Click += delegate { Get<RadioButton>("SettingsNav").IsChecked = true; };
            WireSlingActions();
            WireSettings();
            UpdateConnectionLabels();
            Closing += delegate(object sender, CancelEventArgs args)
            {
                if (busy) { args.Cancel = true; SetStatus("Wait for the current operation to finish."); }
            };
            ResetReport();
            ShowHubPage("Home");
        }

        private static double WindowsTextScale()
        {
            try
            {
                var value = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Accessibility", "TextScaleFactor", 100);
                return value is int ? Math.Max(1, Math.Min(2.25, (int)value / 100.0)) : 1;
            }
            catch (SecurityException) { return 1; }
            catch (UnauthorizedAccessException) { return 1; }
        }

        private void AdaptLayout(double width)
        {
            if (width <= 0) return;
            var rail = (ColumnDefinition)root.FindName("NavigationColumn");
            rail.MaxWidth = Math.Max(65, Math.Min(205, width - 330));
            var railWidth = rail.ActualWidth > 0 ? rail.ActualWidth : rail.Width.Value;
            root.Resources["RailExpanded"] = railWidth >= 140;
            width -= railWidth;
            root.Resources["FieldMaxWidth"] = Math.Max(120, width - 106);
            root.Resources["AppTileSize"] = Math.Min(168, Math.Max(100, width - 78));
            root.Resources["AppViewRow"] = width < 265 ? 1 : 0;
            AdaptSlingLayout(width);
        }

        private T Get<T>(string name) where T : FrameworkElement { return (T)root.FindName(name); }

        private void ShowHubPage(string active)
        {
            var sling = active == "Sling";
            Get<FrameworkElement>("HomePage").Visibility = active == "Home" ? Visibility.Visible : Visibility.Collapsed;
            Get<FrameworkElement>("SettingsPage").Visibility = active == "Settings" ? Visibility.Visible : Visibility.Collapsed;
            Get<FrameworkElement>("Heading").Visibility = sling ? Visibility.Visible : Visibility.Collapsed;
            Get<FrameworkElement>("ModeBadge").Visibility = sling ? Visibility.Visible : Visibility.Collapsed;
            foreach (var page in SlingViews)
                Get<FrameworkElement>(page + "Page").Visibility = sling && page == slingView ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task Operation(string message, Func<Task> action)
        {
            if (busy) return;
            busy = true;
            OverrideCursor = Cursors.Wait;
            foreach (var name in new[] { "PreviewButton", "ApplyButton", "Pages", "Navigation" }) Get<FrameworkElement>(name).IsEnabled = false;
            SetStatus(message);
            try { await action(); }
            catch (Exception exception) { ReportError(exception); }
            finally
            {
                busy = false;
                foreach (var name in new[] { "PreviewButton", "ApplyButton", "Pages", "Navigation" }) Get<FrameworkElement>(name).IsEnabled = true;
                OverrideCursor = null;
                UpdateApplyState();
            }
        }

        private Cursor OverrideCursor { set { Mouse.OverrideCursor = value; } }
        private void SetStatus(string message) { Get<TextBlock>("Status").Text = message; }
        private void Guard(Action action) { try { action(); } catch (Exception exception) { ReportError(exception); } }
        private void ReportError(Exception exception)
        {
            SetStatus(exception.Message);
            MessageBox.Show(this, exception.Message, "Thong Khong Toolbox", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

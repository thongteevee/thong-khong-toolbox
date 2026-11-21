using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Toolbox.Tests
{
    internal static class NavigationChecks
    {
        internal static void Run()
        {
            var window = new ToolboxWindow(1, new HubAccounts(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "navigation-accounts")),
                token => Task.FromResult(ToolboxReleases.CurrentVersion));
            var root = (Grid)((ScrollViewer)window.Content).Content;
            var catalog = (ItemsControl)root.FindName("AppCatalog");
            var search = (TextBox)root.FindName("AppSearch");
            Require(catalog.Items.Count == 2, "Home lost a tool.");
            search.Text = "  wInOpT  ";
            Require(catalog.Items.Count == 1 && ((HomeApp)catalog.Items[0]).Name == "WinOpt", "App name search failed.");
            ((RadioButton)root.FindName("AppListView")).IsChecked = true;
            search.Text = "calendar";
            Require(catalog.Items.Count == 1 && ((HomeApp)catalog.Items[0]).Name.Contains("Sling"), "App description search failed in List view.");
            search.Text = "no matching app";
            Require(catalog.Items.Count == 0 && ((TextBlock)root.FindName("NoAppsFound")).Visibility == Visibility.Visible,
                "No-match search did not explain the empty list.");
            Click(root, "ClearAppSearch");
            Require(search.Text == "" && catalog.Items.Count == 2 && ((TextBlock)root.FindName("NoAppsFound")).Visibility == Visibility.Collapsed,
                "Clearing app search did not restore both tools.");
            var keys = new[] { Key.D2, Key.D3, Key.OemComma, Key.D1, Key.F };
            var pages = new[] { "Sling", "WinOpt", "Settings", "Home", "Home" };
            for (var index = 0; index < keys.Length; index++)
                Require(window.HandleNavigationShortcut(keys[index], ModifierKeys.Control) && ((RadioButton)root.FindName(pages[index] + "Nav")).IsChecked == true,
                    "Navigation shortcut failed for " + pages[index]);
            Require(!window.HandleNavigationShortcut(Key.D2, ModifierKeys.None) && !window.HandleNavigationShortcut(Key.C, ModifierKeys.Control),
                "Navigation intercepted unrelated typing or copy.");

            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(ToolboxWindow);
            var settings = (Settings)type.GetField("settings", flags).GetValue(window);
            settings.Demo = true;
            ((RadioButton)root.FindName("SlingNav")).IsChecked = true;
            var today = Planner.Monday(Planner.Local(DateTimeOffset.UtcNow).Date);
            Click(root, "NextWeek");
            Click(root, "TodayWeek");
            Require((DateTime)type.GetField("visibleMonday", flags).GetValue(window) == today, "Today did not restore the current Eastern-time week.");
            Click(root, "WeekRefresh");
            var original = type.GetField("weekReport", flags).GetValue(window);
            Click(root, "WeekRefresh");
            Require(original != null && !Object.ReferenceEquals(original, type.GetField("weekReport", flags).GetValue(window)),
                "Refresh reused stale schedule data.");
            type.GetField("busy", flags).SetValue(window, true);
            Click(root, "NextWeek");
            Require(!window.HandleNavigationShortcut(Key.D1, ModifierKeys.Control) && (DateTime)type.GetField("visibleMonday", flags).GetValue(window) == today,
                "Navigation or week changes interrupted an active task.");
            type.GetField("busy", flags).SetValue(window, false);
            settings.Demo = false;
            type.GetField("loadedMonday", flags).SetValue(window, (DateTime?)today);
            type.GetField("weekReport", flags).SetValue(window, null);
            Click(root, "NextWeek");
            Require(type.GetField("loadedMonday", flags).GetValue(window) == null, "Changing week retained a cache marker without its report.");
            Click(root, "PreviousWeek");
            Require(type.GetField("loadedMonday", flags).GetValue(window) == null, "Returning to a failed week's view incorrectly skipped refresh.");
            window.Close();
            Console.WriteLine("Passed app search, empty/clear states, keyboard navigation, Today, Refresh, and active-task guards.");
        }

        private static void Click(Grid root, string name) { ((Button)root.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}

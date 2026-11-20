using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Toolbox
{
    internal sealed class HomeApp
    {
        public string Name { get; set; }
        public string Detail { get; set; }
        public string Icon { get; set; }
        public ToolCommand Open { get; set; }
    }

    internal sealed partial class ToolboxWindow
    {
        private HomeApp[] homeApps;

        private void WireHomeNavigation()
        {
            homeApps = new[] {
                new HomeApp { Name = "Sling Shift Auto Apply", Detail = "Preview and apply for shifts around your calendar.",
                    Icon = "\uE787", Open = new ToolCommand(() => Get<RadioButton>("SlingNav").IsChecked = true) },
                new HomeApp { Name = "WinOpt", Detail = "Review temporary files and maintain Windows storage.",
                    Icon = "\uEDA2", Open = new ToolCommand(() => Get<RadioButton>("WinOptNav").IsChecked = true) }
            };
            var search = Get<TextBox>("AppSearch");
            search.TextChanged += delegate { FilterApps(); };
            Get<Button>("ClearAppSearch").Click += delegate { search.Clear(); search.Focus(); };
            search.KeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.Key == Key.Escape) { search.Clear(); args.Handled = true; }
                if (args.Key == Key.Enter && !busy)
                {
                    var matches = Get<ItemsControl>("AppCatalog").Items.Cast<HomeApp>().ToList();
                    if (matches.Count == 1) { matches[0].Open.Execute(null); args.Handled = true; }
                }
            };
            PreviewKeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (HandleNavigationShortcut(args.Key, Keyboard.Modifiers)) args.Handled = true;
            };
            Get<RadioButton>("HomeNav").ToolTip = "Home · Ctrl+1";
            Get<RadioButton>("SlingNav").ToolTip = "Sling · Ctrl+2";
            Get<RadioButton>("WinOptNav").ToolTip = "WinOpt · Ctrl+3";
            Get<RadioButton>("SettingsNav").ToolTip = "Settings · Ctrl+,";
            FilterApps();
        }

        private void FilterApps()
        {
            var search = Get<TextBox>("AppSearch").Text.Trim();
            var matches = homeApps.Where(app => app.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                app.Detail.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            Get<ItemsControl>("AppCatalog").ItemsSource = matches;
            Get<TextBlock>("NoAppsFound").Visibility = matches.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            Get<Button>("ClearAppSearch").Visibility = search.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        internal bool HandleNavigationShortcut(Key key, ModifierKeys modifiers)
        {
            if (busy || modifiers != ModifierKeys.Control) return false;
            var target = key == Key.D1 ? "Home" : key == Key.D2 ? "Sling" : key == Key.D3 ? "WinOpt" : key == Key.OemComma ? "Settings" :
                key == Key.F ? "Home" : null;
            if (target == null) return false;
            Get<RadioButton>(target + "Nav").IsChecked = true;
            if (key == Key.F) { Get<TextBox>("AppSearch").Focus(); Get<TextBox>("AppSearch").SelectAll(); }
            return true;
        }
    }
}

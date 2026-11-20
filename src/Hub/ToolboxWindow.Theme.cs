using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Toolbox
{
    public sealed class AppearanceSettings
    {
        public bool MikuPink { get; set; }
        public bool Artwork { get; set; }
        public AppearanceSettings() { MikuPink = true; Artwork = true; }
    }

    internal sealed partial class ToolboxWindow
    {
        private void WireMikuTheme()
        {
            var appearance = LocalData.Load("appearance", new AppearanceSettings());
            var pink = Get<CheckBox>("MikuPinkTheme");
            var artwork = Get<CheckBox>("MikuArtwork");
            pink.IsChecked = appearance.MikuPink;
            artwork.IsChecked = appearance.Artwork;
            LoadMikuPicture("MikuSettingsPicture", "original-miku.jpg");
            Get<Button>("MikuSettingsCredit").Click += delegate
            {
                Guard(delegate { Process.Start(new ProcessStartInfo("https://piapro.net/intl/en_for_creators.html") { UseShellExecute = true }); });
            };
            RoutedEventHandler changed = delegate
            {
                Guard(delegate
                {
                    var next = new AppearanceSettings { MikuPink = pink.IsChecked == true, Artwork = artwork.IsChecked == true };
                    LocalData.Save("appearance", next);
                    ApplyMikuTheme(next);
                });
            };
            pink.Click += changed;
            artwork.Click += changed;
            ApplyMikuTheme(appearance);
        }

        private void LoadMikuPicture(string name, string asset)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Miku." + asset))
            {
                var bitmap = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                bitmap.Freeze();
                Get<Image>(name).Source = bitmap;
            }
        }

        private void ApplyMikuTheme(AppearanceSettings appearance)
        {
            var keys = new[] { "WindowFill", "Ink", "Muted", "Line", "Accent", "SurfaceFill", "HeaderFill",
                "RailFill", "NavHover", "NavSelected", "NavInk", "ControlLine", "ControlHover", "GripBrush",
                "CheckRing", "RowHover", "DisabledFill", "DisabledLine", "ModeFill", "PinkAccent",
                "HeaderInk", "PrimaryFill", "PrimaryInk", "PrimaryLine" };
            var miku = new[] { "#FFF4F9", "#213E43", "#52696F", "#EACDDD", "#007F83", "#FFFBFD", "#D8F2EE",
                "#DEF4F0", "#E8F8F5", "#BDEBE4", "#0D6161", "#DBADC3", "#B62F74", "#5CA99F",
                "#007F83", "#FCE6F0", "#F1EBEF", "#DCC8D4", "#F8D3E5", "#E8A2C4",
                "#214F53", "#BDEBE4", "#164F54", "#74BBB0" };
            var classic = new[] { "#F3F6FA", "#17263E", "#52647D", "#D8E2EF", "#2256B4", "#FFFFFF", "#17263E",
                "#EAF0F7", "#EDF2FB", "#DDE9FE", "#1749A9", "#BFD1EF", "#98B2D9", "#849AB7",
                "#6B81A1", "#F0F5FC", "#F0F3F7", "#CAD3E0", "#2D4566", "#AD286A",
                "#FFFFFF", "#2256B4", "#FFFFFF", "#2256B4" };
            var colors = appearance.MikuPink ? miku : classic;
            for (var i = 0; i < keys.Length; i++) root.Resources[keys[i]] = WeekTimeline.Brush(colors[i]);
            Background = root.Background;
            var visibility = appearance.MikuPink && appearance.Artwork ? Visibility.Visible : Visibility.Collapsed;
            Get<FrameworkElement>("MikuHomeStickers").Visibility = visibility;
            Get<FrameworkElement>("MikuArtworkPreview").Visibility = visibility;
            Get<CheckBox>("MikuArtwork").IsEnabled = appearance.MikuPink;
            WeekTimeline.SetMikuTheme(appearance.MikuPink);
            if (visibleMonday != default(DateTime))
            {
                UpdateWeek();
                UpdateResultsWeek();
            }
        }
    }
}

using System;
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
            LoadMikuPicture("MikuHomePicture", "desk.png");
            LoadMikuPicture("MikuHomeStickers", "stickers.png");
            LoadMikuPicture("MikuSettingsPicture", "desk.png");
            LoadMikuPicture("MikuSettingsStickers", "stickers.png");
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
                "CheckRing", "RowHover", "DisabledFill", "DisabledLine", "ModeFill" };
            var pink = new[] { "#FFF3F8", "#46293B", "#77566B", "#EED0DF", "#AD286A", "#FFFBFD", "#772649",
                "#FBE4EF", "#FCE3F0", "#F8CDDF", "#842452", "#DBADC3", "#C43B80", "#B87D99",
                "#AA6B8B", "#FFF0F7", "#F4EAF0", "#DCC8D4", "#985476" };
            var classic = new[] { "#F3F6FA", "#17263E", "#52647D", "#D8E2EF", "#2256B4", "#FFFFFF", "#17263E",
                "#EAF0F7", "#EDF2FB", "#DDE9FE", "#1749A9", "#BFD1EF", "#98B2D9", "#849AB7",
                "#6B81A1", "#F0F5FC", "#F0F3F7", "#CAD3E0", "#2D4566" };
            var colors = appearance.MikuPink ? pink : classic;
            for (var i = 0; i < keys.Length; i++) root.Resources[keys[i]] = WeekTimeline.Brush(colors[i]);
            Background = root.Background;
            var visibility = appearance.MikuPink && appearance.Artwork ? Visibility.Visible : Visibility.Collapsed;
            Get<FrameworkElement>("MikuHomeBanner").Visibility = visibility;
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

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Toolbox.Tests
{
    internal static class UpdateChecks
    {
        [STAThread]
        private static int Main()
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            try
            {
                var calls = 0;
                var pending = new TaskCompletionSource<Version>();
                var window = Create(token => { calls++; return pending.Task; });
                var root = Root(window);
                window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                Require(calls == 1 && !Get<Button>(root, "CheckForUpdates").IsEnabled, "Startup did not check asynchronously.");
                Require(Get<RadioButton>(root, "HomeNav").IsEnabled && Get<Border>(root, "UpdateBanner").Visibility == Visibility.Collapsed,
                    "Startup check blocked navigation or showed an early prompt.");
                window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                Click(root, "CheckForUpdates");
                Require(calls == 1, "Duplicate Loaded/manual events started competing checks.");
                pending.SetResult(new Version(999, 0, 0));
                Pump(() => Get<Button>(root, "CheckForUpdates").IsEnabled, "startup completion");
                Require(Get<Border>(root, "UpdateBanner").Visibility == Visibility.Visible &&
                    Get<Button>(root, "InstallUpdate").Tag.Equals(new Version(999, 0, 0)), "New release did not show a usable update suggestion.");
                Require(Get<RadioButton>(root, "HomeNav").IsChecked == true, "Prompt changed the active page.");
                foreach (var width in new[] { 640.0, 1160.0 })
                {
                    window.Measure(new Size(width, 840)); window.Arrange(new Rect(0, 0, width, 840)); window.UpdateLayout();
                    var banner = Get<Border>(root, "UpdateBanner");
                    foreach (var name in new[] { "StartupUpdateNow", "DismissUpdate" })
                    {
                        var button = Get<Button>(root, name);
                        var bounds = button.TransformToAncestor(banner).TransformBounds(new Rect(button.RenderSize));
                        Require(bounds.Left >= 0 && bounds.Right <= banner.ActualWidth && bounds.Bottom <= banner.ActualHeight,
                            "Update action was cropped at width " + width);
                    }
                }
                typeof(ToolboxWindow).GetField("busy", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, true);
                Click(root, "StartupUpdateNow");
                Require(Get<TextBlock>(root, "UpdateBannerText").Text.Contains("Finish"), "Update failed to respect an active task.");
                typeof(ToolboxWindow).GetField("busy", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, false);
                Click(root, "DismissUpdate");
                Require(Get<Border>(root, "UpdateBanner").Visibility == Visibility.Collapsed, "Later did not dismiss the prompt.");
                window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                Require(calls == 1, "Dismissed prompt rechecked in the same startup.");
                Click(root, "CheckForUpdates");
                Require(calls == 2 && Get<Border>(root, "UpdateBanner").Visibility == Visibility.Visible, "Manual retry did not restore the available update.");
                window.Close();

                foreach (var version in new[] { ToolboxReleases.CurrentVersion, new Version(0, 0, 0) })
                {
                    window = Create(token => Task.FromResult(version));
                    root = Root(window);
                    window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                    Require(Get<Border>(root, "UpdateBanner").Visibility == Visibility.Collapsed && Get<Button>(root, "InstallUpdate").Visibility == Visibility.Collapsed,
                        "Equal/older release showed an update suggestion.");
                    window.Close();
                }
                foreach (var failure in new Exception[] { new HttpRequestException("Fixture offline"), new TaskCanceledException(), new InvalidOperationException("Fixture invalid release") })
                {
                    window = Create(token => { var failed = new TaskCompletionSource<Version>(); failed.SetException(failure); return failed.Task; });
                    root = Root(window);
                    window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                    Require(Get<Border>(root, "UpdateBanner").Visibility == Visibility.Collapsed && Get<Button>(root, "CheckForUpdates").IsEnabled,
                        "Failed startup check blocked use or showed an update prompt.");
                    window.Close();
                }
                var observedToken = CancellationToken.None;
                window = Create(async token => {
                    observedToken = token;
                    await Task.Delay(Timeout.Infinite, token);
                    return new Version(999, 0, 0);
                });
                window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                // Raise the close lifecycle without showing a test window.
                window.GetType().GetMethod("OnClosed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { EventArgs.Empty });
                Pump(() => observedToken.IsCancellationRequested, "close cancellation");
                Console.WriteLine("Passed startup checks, once-per-launch behavior, nonblocking navigation, update suggestion, dismissal, manual retry, busy guard, layout, offline handling, and cancellation.");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }

        private static ToolboxWindow Create(Func<CancellationToken, Task<Version>> lookup)
        {
            return new ToolboxWindow(1, new HubAccounts(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "disconnected-accounts")), lookup);
        }
        private static Grid Root(ToolboxWindow window) { return (Grid)((ScrollViewer)window.Content).Content; }
        private static T Get<T>(Grid root, string name) { return (T)root.FindName(name); }
        private static void Click(Grid root, string name) { Get<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
        private static void Pump(Func<bool> ready, string stage)
        {
            if (ready()) return;
            var elapsed = Stopwatch.StartNew();
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(5) };
            timer.Tick += delegate { if (ready() || elapsed.Elapsed > TimeSpan.FromSeconds(3)) frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
            timer.Stop();
            Require(ready(), "UI check timed out: " + stage);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}

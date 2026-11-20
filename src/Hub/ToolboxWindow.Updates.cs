using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Toolbox
{
    internal sealed partial class ToolboxWindow
    {
        private readonly Func<CancellationToken, Task<Version>> latestRelease;
        private readonly CancellationTokenSource updateCancellation = new CancellationTokenSource();
        private bool startupUpdateChecked;
        private bool updateChecking;
        private bool updateWindowClosed;

        private void WireUpdateCheck()
        {
            Get<TextBlock>("ToolboxVersion").Text = "Current version " + ToolboxReleases.CurrentVersion;
            Get<Button>("CheckForUpdates").Click += async delegate { await CheckForUpdates(); };
            Get<Button>("InstallUpdate").Click += async delegate { await InstallAvailableUpdate(); };
            Get<Button>("StartupUpdateNow").Click += async delegate { await InstallAvailableUpdate(); };
            Get<Button>("DismissUpdate").Click += delegate { Get<Border>("UpdateBanner").Visibility = Visibility.Collapsed; };
            Get<Button>("OpenLatestRelease").Click += delegate
            {
                Guard(delegate { Process.Start(new ProcessStartInfo((string)Get<Button>("OpenLatestRelease").Tag) { UseShellExecute = true }); });
            };
            Loaded += async delegate
            {
                if (startupUpdateChecked) return;
                startupUpdateChecked = true;
                await CheckForUpdates();
            };
            Closed += delegate
            {
                updateWindowClosed = true;
                updateCancellation.Cancel();
                if (!updateChecking) updateCancellation.Dispose();
            };
        }

        private async Task CheckForUpdates()
        {
            if (updateChecking || updateWindowClosed) return;
            updateChecking = true;
            var check = Get<Button>("CheckForUpdates");
            var release = Get<Button>("OpenLatestRelease");
            var install = Get<Button>("InstallUpdate");
            var status = Get<TextBlock>("UpdateCheckStatus");
            check.IsEnabled = false;
            status.Text = "Checking GitHub for updates…";
            try
            {
                var latest = await latestRelease(updateCancellation.Token);
                if (updateWindowClosed) return;
                var available = latest > ToolboxReleases.CurrentVersion;
                status.Text = available ? "Version " + latest + " is available." : "Toolbox is up to date.";
                release.Tag = available ? ToolboxReleases.GetReleasePage(latest) : null;
                install.Tag = available ? latest : null;
                release.Visibility = install.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
                Get<TextBlock>("UpdateBannerText").Text = available ? "Update available · v" + latest : "";
                Get<Border>("UpdateBanner").Visibility = available ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (HttpRequestException) { if (!updateWindowClosed) status.Text = "Could not reach GitHub. Check for updates to try again."; }
            catch (OperationCanceledException) { if (!updateWindowClosed) status.Text = "The update check timed out. Check for updates to try again."; }
            catch (InvalidOperationException exception) { if (!updateWindowClosed) status.Text = exception.Message; }
            finally
            {
                updateChecking = false;
                if (updateWindowClosed) updateCancellation.Dispose();
                else check.IsEnabled = true;
            }
        }

        private void UpdateMessage(string message)
        {
            Get<TextBlock>("UpdateCheckStatus").Text = message;
            Get<TextBlock>("UpdateBannerText").Text = message;
        }

        private async Task InstallAvailableUpdate()
        {
            var version = Get<Button>("InstallUpdate").Tag as Version;
            if (version == null) return;
            if (busy || winOptView != null && winOptView.IsWorking)
            {
                UpdateMessage("Finish the current task, then choose Update now to update Toolbox.");
                return;
            }
            var ready = false;
            Get<Button>("StartupUpdateNow").IsEnabled = false;
            Get<Button>("CheckForUpdates").IsEnabled = false;
            await Operation("Downloading the Toolbox update…", async delegate
            {
                try
                {
                    var downloaded = await ToolboxUpdater.Download(version, message =>
                    {
                        UpdateMessage(message);
                        SetStatus(message);
                    });
                    UpdateMessage("Restarting Toolbox to install the update…");
                    await downloaded.Launch();
                    ready = true;
                }
                catch
                {
                    UpdateMessage("Update could not complete. Try again or use View release downloads.");
                    throw;
                }
            });
            Get<Button>("StartupUpdateNow").IsEnabled = true;
            Get<Button>("CheckForUpdates").IsEnabled = !updateChecking;
            if (ready) Close();
        }
    }
}

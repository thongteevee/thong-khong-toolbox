using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Toolbox
{
    internal sealed partial class ToolboxWindow
    {
        private void WireSettings()
        {
            WireUpdateCheck();
            var demo = Get<CheckBox>("Demo");
            demo.IsChecked = settings.Demo;
            demo.Click += delegate
            {
                Guard(delegate
                {
                    settings.Demo = demo.IsChecked == true;
                    LocalData.Save("settings", settings);
                    UpdateConnectionLabels();
                    ResetReport();
                });
            };
            Get<Button>("ConnectGoogle").Click += async delegate
            {
                var client = accounts.SignInClient;
                if (client == null)
                {
                    var setup = Get<Expander>("GoogleSignInSetup");
                    setup.IsExpanded = true;
                    Get<TextBox>("GoogleAppId").Focus();
                    setup.BringIntoView();
                    SetStatus("Set up the Toolbox Google OAuth client once, then connect in your browser.");
                    return;
                }
                await Operation("Finish Google sign-in in your browser…", async delegate
                {
                    var credential = await GoogleClient.Connect(client);
                    var list = await new GoogleClient(credential).Calendars();
                    var previous = list.Where(item => settings.Calendars.Contains(item.Id)).Select(item => item.Id).ToArray();
                    var selected = previous.Length > 0 ? previous : list.Where(item => item.Primary).Select(item => item.Id).ToArray();
                    accounts.ConnectGoogle(credential);
                    SaveCalendars(selected);
                    FillCalendars(list);
                    UpdateConnectionLabels();
                    Get<Expander>("GoogleSignInSetup").IsExpanded = false;
                    ResetReport();
                    SetStatus("Google connected. In Sling → Rules, select the calendars that should block work.");
                });
            };
            Get<Button>("SaveGoogleApp").Click += delegate
            {
                Guard(delegate
                {
                    var secret = Get<PasswordBox>("GoogleAppSecret");
                    var client = GoogleClient.DesktopClient(Get<TextBox>("GoogleAppId").Text, secret.Password);
                    accounts.ConfigureGoogle(client);
                    secret.Clear();
                    if (accounts.Google == null) Get<ItemsControl>("CalendarChoices").Items.Clear();
                    UpdateConnectionLabels();
                    ResetReport();
                    Get<Expander>("GoogleSignInSetup").IsExpanded = false;
                    SetStatus("Google OAuth setup saved. Choose Connect Google to sign in.");
                });
            };
            Get<Button>("GoogleCloudClients").Click += delegate
            {
                Guard(delegate { Process.Start(new ProcessStartInfo("https://console.cloud.google.com/auth/clients") { UseShellExecute = true }); });
            };
            Get<Button>("OpenCalendarRules").Click += delegate
            {
                Get<RadioButton>("SlingNav").IsChecked = true;
                Get<RadioButton>("RulesNav").IsChecked = true;
                Get<FrameworkElement>("CalendarRuleCard").BringIntoView();
            };
            Get<Button>("LoadCalendars").Click += async delegate
            {
                await Operation("Loading Google calendars…", async delegate
                {
                    var credential = accounts.Google;
                    if (credential == null) throw new InvalidOperationException("Connect Google Calendar first.");
                    FillCalendars(await new GoogleClient(credential).Calendars());
                    SetStatus("Google calendars loaded.");
                });
            };
            Get<Button>("DisconnectGoogle").Click += delegate
            {
                Guard(delegate
                {
                    accounts.DisconnectGoogle();
                    Get<ItemsControl>("CalendarChoices").Items.Clear();
                    UpdateConnectionLabels();
                    ResetReport();
                    SetStatus("Google disconnected from Toolbox.");
                });
            };
            Get<Button>("ConnectSling").Click += async delegate
            {
                var password = Get<PasswordBox>("Password");
                var token = Get<PasswordBox>("SlingToken");
                await Operation("Verifying Sling employee account…", async delegate
                {
                    try
                    {
                        var connected = await SlingClient.Connect(Get<TextBox>("Email").Text, password.Password, token.Password, Get<TextBox>("Organization").Text);
                        accounts.SetSling(connected);
                        UpdateConnectionLabels();
                        ResetReport();
                        SetStatus("Sling connected. Use Preview matches to read your schedule.");
                    }
                    finally { password.Clear(); token.Clear(); }
                });
            };
            Get<Button>("DisconnectSling").Click += delegate
            {
                Guard(delegate
                {
                    accounts.SetSling(null);
                    UpdateConnectionLabels();
                    ResetReport();
                    SetStatus("Sling disconnected from Toolbox.");
                });
            };
        }

        private void WireUpdateCheck()
        {
            var current = ToolboxReleases.CurrentVersion;
            Get<TextBlock>("ToolboxVersion").Text = "Current version " + current;
            var check = Get<Button>("CheckForUpdates");
            var release = Get<Button>("OpenLatestRelease");
            var install = Get<Button>("InstallUpdate");
            var status = Get<TextBlock>("UpdateCheckStatus");
            install.Click += async delegate
            {
                if (busy) return;
                var ready = false;
                await Operation("Downloading the Toolbox update…", async delegate
                {
                    try
                    {
                        var downloaded = await ToolboxUpdater.Download((Version)install.Tag, message =>
                        {
                            status.Text = message;
                            SetStatus(message);
                        });
                        status.Text = "Restarting Toolbox to install the update…";
                        await downloaded.Launch();
                        ready = true;
                    }
                    catch
                    {
                        status.Text = "Update could not complete. Try again or use View release downloads.";
                        throw;
                    }
                });
                if (ready) Close();
            };

            release.Click += delegate
            {
                Guard(delegate { Process.Start(new ProcessStartInfo((string)release.Tag) { UseShellExecute = true }); });
            };
            check.Click += async delegate
            {
                check.IsEnabled = false;
                release.Visibility = Visibility.Collapsed;
                install.Visibility = Visibility.Collapsed;
                status.Text = "Checking GitHub for updates…";
                try
                {
                    var latest = await ToolboxReleases.GetLatestVersion();
                    if (latest <= current)
                    {
                        status.Text = "Toolbox is up to date.";
                        return;
                    }

                    status.Text = "Version " + latest + " is available.";
                    release.Tag = ToolboxReleases.GetReleasePage(latest);
                    release.Visibility = Visibility.Visible;
                    install.Tag = latest;
                    install.Visibility = Visibility.Visible;
                }
                catch (HttpRequestException) { status.Text = "Could not reach GitHub. Check your connection and try again."; }
                catch (TaskCanceledException) { status.Text = "The update check timed out. Try again."; }
                catch (InvalidOperationException exception) { status.Text = exception.Message; }
                finally { check.IsEnabled = true; }
            };
        }

        private void FillCalendars(List<GoogleCalendar> items)
        {
            var list = Get<ItemsControl>("CalendarChoices");
            list.Items.Clear();
            foreach (var calendar in items.OrderByDescending(item => item.Primary).ThenBy(item => item.Name))
            {
                var checkbox = new CheckBox { Tag = calendar.Id, IsChecked = settings.Calendars.Contains(calendar.Id),
                    Content = new TextBlock { Text = calendar.Name, TextWrapping = TextWrapping.Wrap }, Margin = new Thickness(0, 2, 0, 4) };
                checkbox.Click += delegate
                {
                    Guard(delegate
                    {
                        SaveCalendars(list.Items.Cast<CheckBox>().Where(item => item.IsChecked == true).Select(item => (string)item.Tag).ToArray());
                        UpdateConnectionLabels();
                    });
                };
                list.Items.Add(checkbox);
            }
        }

        private void SaveCalendars(string[] selected)
        {
            var previous = settings.Calendars;
            settings.Calendars = selected;
            try { LocalData.Save("settings", settings); }
            catch { settings.Calendars = previous; throw; }
        }

        private void UpdateConnectionLabels()
        {
            Get<TextBlock>("ModeText").Text = settings.Demo ? "Demo mode" : "Live mode";
            googleConnected = accounts.Google != null;
            var googleLabel = googleConnected ? "Google Calendar connected" : "Google Calendar disconnected";
            Get<TextBlock>("HomeGoogleStatus").Text = googleLabel;
            Get<TextBlock>("GoogleStatus").Text = googleConnected ? googleLabel + " · Read-only access" : googleLabel;
            Get<TextBlock>("CalendarRuleStatus").Text = googleConnected
                ? settings.Calendars.Length + " calendars selected. Load calendars to change the selection."
                : "Connect Google Calendar in Toolbox Settings to select calendars.";
            Get<Button>("LoadCalendars").IsEnabled = googleConnected;
            Get<Button>("DisconnectGoogle").IsEnabled = googleConnected;
            Get<Button>("OpenCalendarRules").IsEnabled = googleConnected;
            Get<TextBlock>("GoogleAppStatus").Text = accounts.SignInClient == null
                ? "Enable the Calendar API in Google Cloud and create a Desktop app OAuth client. Enter its client ID and secret once."
                : "Browser sign-in is configured. Changing the Desktop app client requires reconnecting Google.";
            var sling = accounts.Sling;
            slingConnected = sling != null;
            Get<TextBlock>("SlingStatus").Text = slingConnected ? "Connected to " + sling.Display : "Sling disconnected";
            Get<TextBlock>("HomeSlingStatus").Text = slingConnected ? "Sling connected" : "Sling disconnected";
            Get<Button>("DisconnectSling").IsEnabled = slingConnected;
        }
    }
}

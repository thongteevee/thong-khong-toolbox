using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Toolbox
{
    internal sealed partial class ToolboxWindow
    {
        private readonly ObservableCollection<Rule> rules;
        private readonly List<Entry> demoJournal = new List<Entry>();
        private readonly WeekTimeline schedule;
        private readonly WeekTimeline resultSchedule;
        private List<ResultItem> resultItems = new List<ResultItem>();
        private RunReport weekReport;
        private DateTime? loadedMonday;
        private Settings settings;
        private RunReport report;
        private DateTime visibleMonday;
        private bool compactHeader;
        private bool checklistStacked;
        private GridLength checklistWidth = new GridLength(330);
        private static readonly string[] SlingViews = { "Week", "Rules", "Results" };
        private string slingView = "Week";

        private void WireSlingActions()
        {
            Get<Button>("PreviewButton").Click += async delegate { await Execute(false); };
            Get<Button>("ApplyButton").Click += async delegate { await Execute(true); };
            Get<Button>("SelectEligible").Click += delegate
            {
                foreach (var item in resultItems.Where(item => item.Status == "Can apply")) item.IsChecked = true;
            };
            Get<Button>("ClearSelection").Click += delegate { foreach (var item in resultItems) item.IsChecked = false; };
            foreach (var page in SlingViews)
            {
                var target = page;
                Get<RadioButton>(target + "Nav").Checked += async delegate
                {
                    ShowSlingPage(target);
                    if (target == "Week") await RefreshWeek();
                };
            }
            Get<Button>("PreviousWeek").Click += async delegate { await MoveWeek(-7); };
            Get<Button>("NextWeek").Click += async delegate { await MoveWeek(7); };
            foreach (var mode in new[] { "CalendarMode", "AgendaMode" })
                Get<RadioButton>(mode).Checked += delegate { UpdateWeek(); };

            LoadRuleControls();
        }

        private void AdaptSlingLayout(double width)
        {
            var stacked = width < 780;
            var checklist = Get<Border>("ChecklistPanel");
            var column = (ColumnDefinition)root.FindName("ChecklistColumn");
            var row = (RowDefinition)root.FindName("ChecklistRow");
            if (stacked != checklistStacked)
            {
                if (stacked) checklistWidth = column.Width;
                checklistStacked = stacked;
                column.MinWidth = stacked ? 0 : 240;
                column.Width = stacked ? new GridLength(0) : checklistWidth;
                row.Height = stacked ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
                Grid.SetRow(checklist, stacked ? 1 : 0);
                Grid.SetColumn(checklist, stacked ? 0 : 1);
                checklist.BorderThickness = stacked ? new Thickness(0, 1, 0, 0) : new Thickness(1, 0, 0, 0);
                Get<GridSplitter>("ChecklistSplitter").Visibility = stacked ? Visibility.Collapsed : Visibility.Visible;
            }
            column.MaxWidth = Math.Max(240, width - 400);
            var compact = width < 790;
            if (compact != compactHeader)
            {
                compactHeader = compact;
                var actions = Get<Border>("HeadingActions");
                Grid.SetColumn(actions, compact ? 0 : 1);
                Grid.SetColumnSpan(actions, compact ? 2 : 1);
                Grid.SetRow(actions, compact ? 1 : 0);
                actions.HorizontalAlignment = compact ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
                actions.Margin = compact ? new Thickness(0, 6, 0, 0) : new Thickness(14, 0, 0, 0);
            }
        }

        private void ShowSlingPage(string active)
        {
            slingView = active;
            ShowHubPage("Sling");
            UpdateApplyState();
        }

        private void LoadRuleControls()
        {
            var items = Get<ItemsControl>("RuleList");
            items.ItemsSource = rules;
            items.AddHandler(Button.ClickEvent, new RoutedEventHandler(delegate(object sender, RoutedEventArgs args)
            {
                var button = args.Source as Button;
                if (button != null && button.Tag is Rule) rules.Remove((Rule)button.Tag);
            }));
            Get<Button>("AddRule").Click += delegate { rules.Add(new Rule { Enabled = true, Title = "", Days = "", Ready = "12:30", Mode = "Confirmed dates", Dates = "" }); };
            Get<Button>("SaveRules").Click += delegate { Guard(delegate { SaveRules(); SetStatus("Special rules saved."); }); };
        }

        private void SaveRules()
        {
            var configured = new Settings {
                Version = settings.Version, Demo = settings.Demo, Calendars = settings.Calendars,
                Rules = rules.Select(rule => new Rule { Enabled = rule.Enabled, Title = (rule.Title ?? "").Trim(), Days = rule.Days,
                    Ready = (rule.Ready ?? "").Trim(), Mode = rule.Mode, Dates = rule.Dates }).ToList()
            };
            Planner.Validate(configured);
            LocalData.Save("settings", configured);
            settings = configured;
        }

        private void ResetReport()
        {
            report = null;
            weekReport = settings.Demo ? ShiftRunner.Sample() : null;
            visibleMonday = Planner.Monday(Planner.Local(weekReport == null ? DateTimeOffset.UtcNow : weekReport.Now).Date);
            loadedMonday = weekReport == null ? (DateTime?)null : visibleMonday;
            resultItems.Clear();
            Get<ItemsControl>("ResultList").ItemsSource = null;
            Get<TextBlock>("ResultSummary").Text = "Choose Preview matches to load your options.";
            UpdateWeek();
            UpdateResultsWeek();
            UpdateApplyState();
            SetStatus(settings.Demo ? "Demo mode uses sample schedules and simulated applications." :
                !googleConnected || !slingConnected ? "Connect Google Calendar and Sling in Settings to preview matches." : "Preview matches to check your schedule and rules.");
        }

        private static string WeekLabel(DateTime monday)
        {
            return monday.ToString("MMM d", CultureInfo.CurrentCulture) + " – " +
                monday.AddDays(6).ToString("MMM d, yyyy", CultureInfo.CurrentCulture) + " · ET";
        }

        private void UpdateWeek()
        {
            Get<TextBlock>("WeekText").Text = WeekLabel(visibleMonday);
            schedule.Render(visibleMonday, weekReport, Get<RadioButton>("AgendaMode").IsChecked == true);
        }

        private async Task RefreshWeek()
        {
            if (busy || loadedMonday == visibleMonday || !settings.Demo && !slingConnected && !googleConnected) return;
            var monday = visibleMonday;
            await Operation("Loading the weekly schedule…", async delegate
            {
                weekReport = await ShiftRunner.ReadWeek(settings, accounts, monday);
                loadedMonday = monday;
                UpdateWeek();
                SetStatus("Weekly schedule loaded.");
            });
        }

        private async Task MoveWeek(int days)
        {
            visibleMonday = visibleMonday.AddDays(days);
            weekReport = settings.Demo ? ShiftRunner.Sample() : null;
            UpdateWeek();
            await RefreshWeek();
        }

        private void UpdateResultsWeek()
        {
            Get<TextBlock>("ResultWeekText").Text = report == null ? "" : WeekLabel(report.Monday);
            resultSchedule.Render(report == null ? visibleMonday : report.Monday, report, false);
        }

        private void UpdateApplyState()
        {
            var count = resultItems.Count(item => item.IsChecked);
            var selectable = resultItems.Count(item => item.CanApply);
            Get<TextBlock>("CheckedCount").Text = resultItems.Count == 0 ? "" :
                selectable > 0 ? count + " of " + selectable + " shifts selected" : "No shifts available to select.";
            Get<Button>("SelectEligible").IsEnabled = resultItems.Any(item => item.Status == "Can apply" && !item.IsChecked);
            Get<Button>("ClearSelection").IsEnabled = count > 0;
            Get<Button>("ApplyButton").IsEnabled = !busy && slingView == "Results" && count > 0;
        }

        private async Task Execute(bool submit)
        {
            if (!settings.Demo && (!googleConnected || !slingConnected))
            {
                Get<RadioButton>("SettingsNav").IsChecked = true;
                SetStatus("Connect Google Calendar and Sling before running live.");
                return;
            }
            var selectedItems = resultItems.Where(item => item.IsChecked).ToList();
            var selected = selectedItems.Select(item => item.Shift).ToList();
            var confirmed = selectedItems.Where(item => item.Status == "Needs confirmation").Select(item => item.Decision).ToList();
            if (submit && (selected.Count == 0 || report == null)) return;
            var monday = submit ? report.Monday : visibleMonday;
            await Operation(submit ? "Rechecking selected shifts…" : "Preparing preview…", async delegate
            {
                SaveRules();
                report = await ShiftRunner.Run(settings, accounts, submit, demoJournal, SetStatus, selected, monday, confirmed);
                if (submit)
                {
                    loadedMonday = null;
                    weekReport = null;
                }
                ShowResults(submit);
                UpdateResultsWeek();
                Get<RadioButton>("ResultsNav").IsChecked = true;
                if (submit)
                {
                    var uncertain = report.Results.Count(row => row.Status == "Check Sling" &&
                        selected.Any(shift => ShiftRunner.SameSelection(shift, row.Shift)));
                    var unsent = Math.Max(0, selected.Count - report.ApplicationsSent - uncertain);
                    var status = report.ApplicationsSent + " sent";
                    if (unsent > 0) status += " · " + unsent + " not sent";
                    if (uncertain > 0) status += " · " + uncertain + " need verification";
                    SetStatus(status + (report.Note == null ? "." : ". " + report.Note));
                }
                else SetStatus("Check the shifts you want Auto Apply to submit.");
            });
        }

        private void ShowResults(bool submit)
        {
            var visibleResults = report.Results.Where(row => row.Status == "Eligible" || row.Status == "Needs confirmation" ||
                row.Status == "Check Sling" || submit && new[] { "Submitted", "Assigned", "Declined", "Not sent", "No longer available" }
                    .Contains(row.Status)).ToList();
            var summary = new List<string> { settings.Demo ? "Demo" : submit ? "Auto apply" : "Preview" };
            if (submit) summary.Add(report.ApplicationsSent + " sent");
            else summary.Add(visibleResults.Count + " offers");
            foreach (var pair in new[] { new[] { "Eligible", "eligible" }, new[] { "Assigned", "assigned" },
                new[] { "Declined", "declined" }, new[] { "Not sent", "not sent" }, new[] { "No longer available", "no longer available" } })
            {
                var count = visibleResults.Count(row => row.Status == pair[0]);
                if (pair[0] == "Eligible" && submit) continue;
                if (count > 0 || pair[0] == "Eligible") summary.Add(count + " " + pair[1]);
            }
            var remaining = report.Results.Count(row => row.Status == "Eligible");
            if (submit && remaining > 0) summary.Add(remaining + " still eligible");
            var toReview = report.Results.Count(row => row.Status == "Needs confirmation" || row.Status == "Check Sling");
            if (toReview > 0) summary.Add(toReview + " to review");
            var summaryText = String.Join(" · ", summary);
            Get<TextBlock>("ResultSummary").Text = summaryText;
            resultItems = visibleResults
                .Select(row => new ResultItem(row, !submit, UpdateApplyState))
                .OrderBy(item => item.Status == "Can apply" ? 0 : item.Status == "Needs confirmation" ? 1 : 2)
                .ThenBy(item => item.Shift.Start).ToList();
            if (resultItems.Count == 0) resultItems.Add(new ResultItem(null, false, UpdateApplyState));
            Get<ItemsControl>("ResultList").ItemsSource = resultItems;
            UpdateApplyState();
        }
    }
}

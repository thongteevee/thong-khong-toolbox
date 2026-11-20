using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Toolbox
{
    internal sealed class WeekTimeline
    {
        private static Brush Ink = Brush("#17263E");
        private static Brush Muted = Brush("#52647D");
        private static Brush GridLine = Brush("#E0E8F3");
        private static Brush Heading = Brush("#F0F4FA");
        private static readonly string[] CategoryLabels = { "Calendar events", "Booked shifts", "Available shifts", "Applied", "Needs confirmation" };
        private static readonly Brush[] Fills = { Brush("#E3EDFE"), Brush("#F0E7FA"), Brush("#E2F4EA"), Brush("#DDF2F4"), Brush("#FFF0CF") };
        private static readonly Brush[] Colors = { Brush("#24539B"), Brush("#6A3D97"), Brush("#16654B"), Brush("#17606A"), Brush("#784C14") };
        private const double HourHeight = 36;
        private const double MinimumCalendarWidth = 950;
        private readonly ContentControl host;
        private readonly WrapPanel legend;
        private readonly bool results;

        internal WeekTimeline(ContentControl host, WrapPanel legend, bool results = false)
        {
            this.host = host;
            this.legend = legend;
            this.results = results;
            BuildLegend();
        }

        internal static void SetMikuTheme(bool enabled)
        {
            Ink = Brush(enabled ? "#213E43" : "#17263E");
            Muted = Brush(enabled ? "#52696F" : "#52647D");
            GridLine = Brush(enabled ? "#EED0DF" : "#E0E8F3");
            Heading = Brush(enabled ? "#E3F5F2" : "#F0F4FA");
            Fills[0] = Brush(enabled ? "#FCE1EE" : "#E3EDFE");
            Colors[0] = Brush(enabled ? "#96275D" : "#24539B");
        }

        private void BuildLegend()
        {
            legend.Children.Clear();
            for (var i = 0; i < (results ? CategoryLabels.Length : CategoryLabels.Length - 1); i++)
            {
                var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 16, 5) };
                item.Children.Add(new Border { Width = 8, Height = 8, Background = Colors[i], CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });
                item.Children.Add(Text(results && i == 2 ? "Can apply" : CategoryLabels[i], 12, Muted));
                legend.Children.Add(item);
            }
        }

        internal static Brush Brush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        internal static string TimeRange(Slot slot)
        {
            if (IsAllDay(slot)) return "All day";
            var first = Planner.Local(slot.Start);
            var last = Planner.Local(slot.End);
            return first.ToString("h:mm tt") + " – " + (slot.OpenEnd ? "Open-ended" : (last.Date != first.Date ? last.ToString("ddd h:mm tt") : last.ToString("h:mm tt")));
        }

        internal void Render(DateTime monday, RunReport report, bool agenda)
        {
            BuildLegend();
            var from = Planner.At(monday, TimeSpan.Zero);
            var until = Planner.At(monday.AddDays(7), TimeSpan.Zero);
            var entries = report == null ? new List<Slot>() : report.Calendar.Concat(report.Shifts.Where(shift => shift.Kind == "Available" &&
                (!results || report.Results.Any(row => (row.Status == "Eligible" || row.Status == "Needs confirmation") && ReferenceEquals(row.Shift, shift))) &&
                !report.Calendar.Any(slot => (slot.Kind == "Pending" && Planner.SameOffer(shift, slot)) ||
                    (slot.Kind == "Booked" && (Planner.SameOffer(shift, slot) || Planner.SameShift(shift, slot))))))
                .Where(slot => slot.Start < until && slot.End > from).ToList();
            legend.Visibility = agenda ? Visibility.Collapsed : Visibility.Visible;
            if (entries.Count == 0 && (!results || report == null))
            {
                host.Content = new Border { BorderBrush = GridLine, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(15),
                    Child = Text(report == null ? (results ? "Preview matches to see shifts on the calendar." : "Connect your accounts in Settings to load the schedule.") : "No schedule entries this week.", 14, Muted) };
                return;
            }

            host.Content = agenda ? Agenda(monday, entries, report) : Calendar(monday, entries, report, results);
        }

        private static FrameworkElement Agenda(DateTime monday, List<Slot> entries, RunReport report)
        {
            var stack = new StackPanel { Margin = new Thickness(14, 0, 12, 10) };
            for (var day = 0; day < 7; day++)
            {
                var date = monday.AddDays(day);
                var slots = Day(entries, date).OrderBy(slot => slot.Start).ThenBy(slot => slot.End).ToList();
                if (slots.Count == 0) continue;
                var group = Text(date.ToString("dddd, MMMM d", CultureInfo.CurrentCulture), 15, Ink, true);
                group.Margin = new Thickness(0, 12, 0, 6);
                stack.Children.Add(group);
                foreach (var slot in slots)
                {
                    var kind = Category(slot);
                    var content = new StackPanel();
                    content.Children.Add(Text(slot.Name, 14, Ink, true));
                    var detail = Text(TimeRange(slot) + " · " + KindLabel(slot), 13, Colors[kind]);
                    detail.Margin = new Thickness(0, 3, 0, 0);
                    content.Children.Add(detail);
                    var decision = DecisionFor(report, slot);
                    if (decision != null) content.Children.Add(Text(slot.Kind == "Pending" ? decision.Reason :
                        decision.Status + " · " + decision.Reason, 13, Muted));
                    stack.Children.Add(new Border { Child = content, Padding = new Thickness(9, 7, 9, 7), Margin = new Thickness(0, 0, 0, 6),
                        BorderThickness = new Thickness(3, 0, 0, 0), BorderBrush = Colors[kind], Background = Fills[kind], CornerRadius = new CornerRadius(5) });
                }
            }
            return new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        }

        private static FrameworkElement Calendar(DateTime monday, List<Slot> entries, RunReport report, bool results)
        {
            var minimumWidth = results ? 560 : MinimumCalendarWidth;
            var grid = new Grid { MinWidth = minimumWidth, Background = Brushes.White };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(57) });
            for (var day = 0; day < 7; day++) grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var timed = entries.Where(slot => !IsAllDay(slot)).ToList();
            var firstHour = Math.Min(8, timed.Select(slot => Planner.Local(slot.Start).Date < monday ? 0 : Planner.Local(slot.Start).Hour).DefaultIfEmpty(8).Min());
            var lastHour = Math.Max(21, timed.Select(slot => Planner.Local(slot.End).Date > Planner.Local(slot.Start).Date ? 24 :
                (int)Math.Ceiling(Planner.Local(slot.End).TimeOfDay.TotalHours)).DefaultIfEmpty(21).Max());
            for (var hour = firstHour; hour < lastHour; hour++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(HourHeight) });
                var label = Text(monday.AddHours(hour).ToString("h tt"), 12, Muted);
                Cell(grid, hour - firstHour + 2, 0, label, Brushes.White, new Thickness(7, 3, 4, 0));
            }
            Cell(grid, 0, 0, Text("ET", 12, Muted), Heading, new Thickness(7, 8, 0, 8));
            var detail = Text("", 13, Ink);
            var banner = new Border { Background = Heading, Padding = new Thickness(12, 8, 12, 8), Child = detail, Visibility = Visibility.Collapsed };
            Action<Slot> select = slot =>
            {
                detail.Text = ShiftDetails(slot, DecisionFor(report, slot));
                banner.Visibility = Visibility.Visible;
            };
            for (var day = 0; day < 7; day++)
            {
                var date = monday.AddDays(day);
                var rows = Day(entries, date).ToList();
                var label = Text(date.ToString("ddd d"), 14, Ink, true);
                label.TextAlignment = TextAlignment.Center;
                Cell(grid, 0, day + 1, label, Heading, new Thickness(2, 8, 2, 8));
                var allDay = rows.Where(IsAllDay).ToList();
                if (entries.Any(IsAllDay))
                {
                    var allDayStack = new StackPanel();
                    foreach (var slot in allDay)
                        allDayStack.Children.Add(SlotButton(slot, report, select));
                    Cell(grid, 1, day + 1, allDayStack, Brushes.White, new Thickness(3, 3, 3, 3));
                }
                for (var hour = firstHour; hour < lastHour; hour++)
                    Cell(grid, hour - firstHour + 2, day + 1, null, Brushes.White, new Thickness(0));

                var dayCanvas = new Canvas { ClipToBounds = true };
                Grid.SetRow(dayCanvas, 2);
                Grid.SetRowSpan(dayCanvas, lastHour - firstHour);
                Grid.SetColumn(dayCanvas, day + 1);
                grid.Children.Add(dayCanvas);
                PositionCards(dayCanvas, rows.Where(slot => !IsAllDay(slot)).ToList(), date, firstHour, report, select);
            }
            var scroller = new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scroller.ScrollChanged += delegate(object sender, ScrollChangedEventArgs args)
            {
                if (args.ViewportWidthChange != 0 && args.ViewportWidth > 0) grid.Width = Math.Max(minimumWidth, args.ViewportWidth);
            };
            var panel = new DockPanel();
            DockPanel.SetDock(banner, Dock.Top);
            panel.Children.Add(banner);
            panel.Children.Add(scroller);
            return panel;
        }

        private static void PositionCards(Canvas canvas, List<Slot> slots, DateTime date, int firstHour, RunReport report, Action<Slot> select)
        {
            var ordered = slots.OrderBy(slot => slot.Start).ThenByDescending(slot => slot.End).ToList();
            var groups = new List<List<Slot>>();
            var groupEnd = DateTimeOffset.MinValue;
            foreach (var slot in ordered)
            {
                if (slot.Start >= groupEnd) groups.Add(new List<Slot>());
                groups[groups.Count - 1].Add(slot);
                if (slot.End > groupEnd) groupEnd = slot.End;
            }
            foreach (var group in groups)
            {
                var lanes = new List<DateTimeOffset>();
                var placements = new List<KeyValuePair<int, Button>>();
                foreach (var slot in group)
                {
                    var lane = lanes.FindIndex(laneEnd => laneEnd <= slot.Start);
                    if (lane < 0) { lane = lanes.Count; lanes.Add(slot.End); }
                    else lanes[lane] = slot.End;
                    var start = Planner.Local(slot.Start);
                    var end = Planner.Local(slot.End);
                    var from = start.Date < date ? 0 : start.TimeOfDay.TotalHours;
                    var until = end.Date > date ? 24 : end.TimeOfDay.TotalHours;
                    var peak = Math.Max(25, (until - from) * HourHeight - 3);
                    var button = SlotButton(slot, report, select, peak);
                    button.Height = peak;
                    Canvas.SetTop(button, (from - firstHour) * HourHeight + 1);
                    canvas.Children.Add(button);
                    placements.Add(new KeyValuePair<int, Button>(lane, button));
                }
                var count = lanes.Count;
                canvas.SizeChanged += delegate(object sender, SizeChangedEventArgs args)
                {
                    var laneWidth = args.NewSize.Width / count;
                    foreach (var placement in placements)
                    {
                        Canvas.SetLeft(placement.Value, laneWidth * placement.Key + 2);
                        placement.Value.Width = Math.Max(0, laneWidth - 4);
                    }
                };
            }
        }

        private static Button SlotButton(Slot slot, RunReport report, Action<Slot> select, double? height = null)
        {
            var decision = DecisionFor(report, slot);
            var category = decision != null && decision.Status == "Needs confirmation" ? 4 : Category(slot);
            var body = new StackPanel();
            var name = Text(slot.Name, 12, Colors[category], true);
            if (height.HasValue)
            {
                name.LineHeight = 15;
                name.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                name.TextWrapping = height >= 55 ? TextWrapping.Wrap : TextWrapping.NoWrap;
                if (height >= 55) name.MaxHeight = 30;
                name.TextTrimming = TextTrimming.CharacterEllipsis;
            }
            body.Children.Add(name);
            if (height >= 39)
            {
                var caption = slot.Kind == "Pending" ? "Applied" : decision == null ? Planner.Local(slot.Start).ToString("h:mm tt") :
                    decision.Status == "Eligible" ? "Can apply" : decision.Status;
                var time = Text(caption, 11, Colors[category]);
                time.TextWrapping = TextWrapping.NoWrap;
                time.TextTrimming = TextTrimming.CharacterEllipsis;
                body.Children.Add(time);
            }
            var full = ShiftDetails(slot, decision);
            var button = new Button { Content = body, MinHeight = 0, Background = Fills[category], BorderBrush = Colors[category], BorderThickness = new Thickness(2, 0, 0, 0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top, Padding = new Thickness(5, 3, 4, 3),
                ToolTip = new ToolTip { Content = new TextBlock { Text = full, TextWrapping = TextWrapping.Wrap, MaxWidth = 375 },
                    Padding = new Thickness(9) } };
            ToolTipService.SetInitialShowDelay(button, 220);
            ToolTipService.SetShowDuration(button, 30000);
            AutomationProperties.SetName(button, full.Replace("\n", ", "));
            button.Click += delegate { select(slot); };
            return button;
        }

        private static string ShiftDetails(Slot slot, Decision decision)
        {
            var time = Planner.Local(slot.Start).ToString("dddd, MMM d, yyyy") + " · " + TimeRange(slot);
            if (!IsAllDay(slot)) time += " ET";
            var lines = new List<string> { slot.Name, time };
            if (!String.IsNullOrWhiteSpace(slot.PositionName)) lines.Add("Role · " + slot.PositionName);
            if (!String.IsNullOrWhiteSpace(slot.LocationName)) lines.Add("Location · " + slot.LocationName);
            lines.Add(slot.Kind == "Pending" || decision == null ? KindLabel(slot) : decision.Status == "Eligible" ? "Can apply" : decision.Status);
            if (decision != null) lines.Add(decision.Reason);
            if (!String.IsNullOrWhiteSpace(slot.Notes) && slot.Notes != slot.Name) lines.Add("Sling note · " + slot.Notes);
            return String.Join("\n", lines);
        }

        private static Decision DecisionFor(RunReport report, Slot slot)
        {
            if (slot.Kind == "Booked") return null;
            return report.Results.FirstOrDefault(row => (slot.Kind != "Pending" || row.IsApplied) &&
                (ReferenceEquals(row.Shift, slot) || Planner.SameOffer(row.Shift, slot)));
        }

        private static IEnumerable<Slot> Day(List<Slot> entries, DateTime date)
        {
            var first = Planner.At(date, TimeSpan.Zero);
            var last = Planner.At(date.AddDays(1), TimeSpan.Zero);
            return entries.Where(slot => slot.Start < last && slot.End > first);
        }

        private static bool IsAllDay(Slot slot) { return slot.Kind == "All day" || slot.Kind == "Sling closure"; }

        private static int Category(Slot slot) { return slot.Kind == "Booked" ? 1 : slot.Kind == "Available" ? 2 : slot.Kind == "Pending" ? 3 : 0; }

        private static string KindLabel(Slot slot) { return slot.Kind == "Sling closure" ? "Sling closure" : new[] { "Calendar event", "Booked shift", "Available shift", "Applied" }[Category(slot)]; }

        private static TextBlock Text(string value, double size, Brush color, bool heading = false)
        {
            return new TextBlock { Text = value, FontSize = size, Foreground = color, FontWeight = heading ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
        }

        private static void Cell(Grid grid, int row, int column, UIElement child, Brush background, Thickness padding)
        {
            var border = new Border { Background = background, BorderBrush = GridLine, BorderThickness = new Thickness(0, 0, 1, 1), Padding = padding, Child = child };
            Grid.SetRow(border, row);
            Grid.SetColumn(border, column);
            grid.Children.Add(border);
        }
    }
}

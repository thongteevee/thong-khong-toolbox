using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Toolbox.Tests
{
    internal static class TimesheetChecks
    {
        [STAThread]
        private static int Main()
        {
            try
            {
                ReviewChecks.Run().GetAwaiter().GetResult();
                var today = Planner.Local(DateTimeOffset.UtcNow).Date;
                var booked = Shift("Scheduled", "Booked", today, 9, 13);
                var originalStart = booked.Start;
                var originalEnd = booked.End;
                var projections = new[] {
                    Projection("approved", booked.Start.AddMinutes(-5), booked.End.AddMinutes(5), "12"),
                    Projection("pending", booked.Start, booked.End.AddMinutes(-15), "99"),
                    Projection("pending", booked.Start.AddDays(1), booked.End.AddDays(1), "12"),
                };
                var document = new Dictionary<string, object> { { "id", booked.Id }, { "timesheetProjections", projections } };
                booked.WorkPeriods = SlingClient.ParseWorkPeriods(document, booked, "12");
                Require(booked.WorkPeriods.Count == 2, "Another employee's time entered the schedule.");
                Require(booked.WorkPeriods[0].Approved && !booked.WorkPeriods[1].Approved, "Timesheet status did not remain per work period.");
                var actual = booked.ForWorkPeriod(booked.WorkPeriods[0]);
                Require(actual.Start == originalStart.AddMinutes(-5) && actual.End == originalEnd.AddMinutes(5), "Longer approved time was truncated to the scheduled shift.");
                Require(actual.WorkStatus == "Time approved" && actual.ScheduledShift == booked, "Approved time lost its scheduled reference.");
                Require(booked.Start == originalStart && booked.End == originalEnd, "Displaying approved hours changed planning times.");

                projections[0] = Projection("approved", booked.Start.AddHours(1), booked.End.AddMinutes(-20), "12");
                projections[1] = Projection("pending", booked.Start, null, "12");
                document["timesheetProjections"] = projections;
                var shorter = SlingClient.ParseWorkPeriods(document, booked, "12");
                Require(shorter.Count == 2 && shorter[0].Start == originalStart.AddHours(1) && shorter[0].End == originalEnd.AddMinutes(-20), "Shorter approved hours were replaced by scheduled hours.");
                Require(shorter.All(period => period.End > period.Start), "Incomplete clock-out was marked Worked.");
                document["id"] = "another-shift";
                try { SlingClient.ParseWorkPeriods(document, booked, "12"); throw new Exception("Mismatched timesheet accepted."); }
                catch (InvalidOperationException) { }

                CheckTimesheetLoading(today).GetAwaiter().GetResult();

                var report = new RunReport { Now = DateTimeOffset.UtcNow };
                var assigned = Shift("Future assigned", "Booked", today.AddDays(1), 9, 13);
                var applied = Shift("Future application", "Pending", today.AddDays(1), 14, 18);
                var worked = Shift("Recorded work", "Booked", today.AddDays(-1), 9, 13);
                worked.WorkPeriods = new List<WorkPeriod> { new WorkPeriod { Start = worked.Start, End = worked.End.AddMinutes(10) } };
                var paid = Shift("Approved hours", "Booked", today.AddDays(-1), 14, 18);
                paid.WorkPeriods = new List<WorkPeriod> { new WorkPeriod { Start = paid.Start.AddMinutes(5), End = paid.End.AddMinutes(-10), Approved = true } };
                var unworked = Shift("Unworked assignment", "Booked", today.AddDays(-1), 9, 13);
                unworked.WorkPeriods = new List<WorkPeriod>();
                var unreadable = Shift("Unknown attendance", "Booked", today.AddDays(-1), 9, 13);
                unreadable.TimesheetUnavailable = true;
                report.Calendar.AddRange(new[] { assigned, applied, worked, paid, Shift("Past class", "Calendar", today.AddDays(-1), 8, 9),
                    Shift("Old application", "Pending", today.AddDays(-1), 19, 20), unworked, unreadable,
                    Shift("Unloaded attendance", "Booked", today.AddDays(-1), 9, 13) });
                var expected = new Dictionary<string, string> { { assigned.Name, "#FF24539B" }, { applied.Name, "#FF845B0D" },
                    { worked.Name, "#FF535D6B" }, { paid.Name, "#FF16654B" } };
                foreach (var theme in new[] { false, true })
                foreach (var resultView in new[] { false, true })
                foreach (var agenda in new[] { false, true })
                {
                    WeekTimeline.SetMikuTheme(theme);
                    var host = new ContentControl();
                    new WeekTimeline(host, new WrapPanel(), resultView).Render(today.AddDays(-2), report, agenda);
                    var text = Descendants(host).OfType<TextBlock>().ToList();
                    foreach (var pair in expected)
                    {
                        var label = text.Single(item => item.Text == pair.Key);
                        if (agenda)
                        {
                            var details = ((StackPanel)label.Parent).Children.OfType<TextBlock>().Skip(1).First();
                            Require(details.Foreground.ToString() == pair.Value, pair.Key + " has the wrong agenda color.");
                        }
                        else Require(label.Foreground.ToString() == pair.Value, pair.Key + " has the wrong calendar color.");
                    }
                    Require(!text.Any(item => item.Text == "Past class" || item.Text == "Old application"), "Past-day filtering regressed.");
                    Require(!text.Any(item => item.Text == "Unworked assignment" || item.Text == "Unknown attendance" || item.Text == "Unloaded attendance"),
                        "A past assignment without recorded work appeared in worked history.");
                    Require(text.Any(item => item.Text.Contains("Scheduled")) == agenda, "Agenda did not distinguish scheduled and approved hours.");
                }
                var result = new ResultItem(new Decision(applied, "Submitted", "Fixture"), false, () => {});
                Require(result.Status == "Applied" && result.StatusForeground.ToString() == "#FF845B0D", "Applied result color is wrong.");
                var approved = new ResultItem(new Decision(assigned, "Assigned", "Fixture"), false, () => {});
                Require(approved.Status == "Approved" && approved.StatusForeground.ToString() == "#FF24539B", "Assigned result did not use Approved blue.");
                Console.WriteLine("Passed timesheet parsing, adjusted hours, employee isolation, colors, themes, worked-only history, submission independence, bounded concurrency, and timeout handling.");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }

        private static async Task CheckTimesheetLoading(DateTime today)
        {
            var past = Enumerable.Range(1, 10).Select(index => (object)new Dictionary<string, object> {
                { "id", index.ToString() }, { "type", "shift" }, { "userId", "12" },
                { "dtstart", Planner.At(today.AddDays(-1), TimeSpan.FromHours(9)).ToString("o") },
                { "dtend", Planner.At(today.AddDays(-1), TimeSpan.FromHours(13)).ToString("o") }
            }).ToArray();
            var requests = 0;
            var active = 0;
            var peak = 0;
            var client = new SlingClient(new SlingConnection { Org = "1", User = "12" }, async (path, label, token) => {
                if (path == "groups") return new object[0];
                if (path.StartsWith("calendar/")) return past;
                Interlocked.Increment(ref requests);
                var count = Interlocked.Increment(ref active);
                int observed;
                do { observed = peak; } while (count > observed && Interlocked.CompareExchange(ref peak, count, observed) != observed);
                try { await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false); }
                finally { Interlocked.Decrement(ref active); }
                return null;
            });
            var scheduleTask = client.Schedule(Planner.At(today.AddDays(-1), TimeSpan.Zero), Planner.At(today.AddDays(2), TimeSpan.Zero));
            Require(await Task.WhenAny(scheduleTask, Task.Delay(2000)).ConfigureAwait(false) == scheduleTask,
                "Historical timesheets delayed the schedule needed for applications.");
            var schedule = await scheduleTask.ConfigureAwait(false);
            Require(requests == 0 && schedule.Count == 10, "Schedule requested display-only timesheets.");
            var offer = Shift("Future offer", "Available", today.AddDays(1), 14, 18);
            offer.ApplyId = "100";
            var report = new RunReport { Now = DateTimeOffset.UtcNow, Shifts = new List<Slot> { offer } };
            report.Calendar.AddRange(schedule);
            var submitted = false;
            await ShiftRunner.Process(report, new Settings(), true, client.Key, new List<Entry>(), shift => {
                Require(requests == 0, "Historical lookups started before submitting the future offer.");
                submitted = true;
                return Task.FromResult(201);
            }, () => {}, message => {}, new[] { offer }).ConfigureAwait(false);
            Require(submitted && report.ApplicationsSent == 1, "Future offer did not submit independently of slow timesheets.");
            var timer = Stopwatch.StartNew();
            await client.EnrichTimesheets(schedule, TimeSpan.FromMilliseconds(150)).ConfigureAwait(false);
            Require(timer.Elapsed < TimeSpan.FromSeconds(2), "Display enrichment exceeded its total timeout.");
            Require(peak == 3 && active == 0 && requests == 3, "Timesheet concurrency or queued cancellation was incorrect.");
            Require(client.TimesheetsUnavailable && schedule.All(shift => shift.TimesheetUnavailable && shift.WorkPeriods == null),
                "Timed-out history was mistaken for a successfully empty timesheet.");

            var empty = Shift("Empty", "Booked", today.AddDays(-1), 9, 13);
            var failed = Shift("Unavailable", "Booked", today.AddDays(-1), 14, 18);
            var emptyClient = new SlingClient(new SlingConnection { Org = "1", User = "12" }, (path, label, token) => {
                if (path.Contains("Unavailable")) throw new InvalidOperationException("Fixture unavailable");
                return Task.FromResult<object>(new Dictionary<string, object> { { "id", empty.Id }, { "timesheetProjections", new object[0] } });
            });
            await emptyClient.EnrichTimesheets(new[] { empty, failed }).ConfigureAwait(false);
            Require(empty.WorkPeriods != null && empty.WorkPeriods.Count == 0 && !empty.TimesheetUnavailable,
                "Successfully empty timesheets were treated as unavailable.");
            Require(failed.WorkPeriods == null && failed.TimesheetUnavailable && emptyClient.TimesheetsUnavailable,
                "Unavailable timesheets lost their distinct state.");
        }

        private static object Projection(string status, DateTimeOffset start, DateTimeOffset? end, string user)
        {
            return new Dictionary<string, object> { { "clockIn", start.ToString("o") }, { "clockOut", end.HasValue ? end.Value.ToString("o") : null },
                { "status", status }, { "user", new Dictionary<string, object> { { "id", user } } } };
        }

        private static Slot Shift(string name, string kind, DateTime date, int first, int last)
        {
            return new Slot { Id = name, Name = name, Kind = kind, Start = Planner.At(date, TimeSpan.FromHours(first)), End = Planner.At(date, TimeSpan.FromHours(last)) };
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            yield return parent;
            foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
                foreach (var item in Descendants(child)) yield return item;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

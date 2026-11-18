using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Toolbox
{
    internal static class ShiftRunner
    {
        internal static async Task<RunReport> ReadWeek(Settings settings, HubAccounts accounts, DateTime monday, List<Entry> demoJournal)
        {
            if (settings.Demo)
            {
                var sample = Sample();
                AddAppliedShifts(sample, demoJournal.Where(entry => entry.State == "Submitted").Select(entry => entry.Shift));
                return sample;
            }
            var account = accounts.Sling;
            var google = accounts.Google;
            var from = Planner.At(monday, TimeSpan.Zero);
            var until = Planner.At(monday.AddDays(7), TimeSpan.Zero);
            var report = new RunReport { Now = DateTimeOffset.UtcNow };
            var events = google != null && settings.Calendars.Length > 0
                ? new GoogleClient(google).Events(settings.Calendars, from, until)
                : Task.FromResult(new List<Slot>());
            if (account == null)
            {
                report.Calendar.AddRange(await events);
                return report;
            }

            var sling = new SlingClient(account);
            await sling.VerifyAccount();
            var schedule = sling.Schedule(from, until);
            var shifts = sling.Available(from, until);
            await Task.WhenAll(events, schedule, shifts);
            report.Calendar.AddRange(await events);
            report.Calendar.AddRange(await schedule);
            report.Shifts.AddRange(await shifts);
            var journal = LocalData.Load("applications", new List<Entry>());
            var previous = journal.Where(entry => entry.Account == sling.Key && entry.State == "Submitted" &&
                entry.Shift.Start < until && entry.Shift.End > from).ToList();
            await RecheckPriorApplications(report, sling.Key, previous, sling.CheckApplication);
            AddAppliedShifts(report, previous.Where(entry => entry.State == "Submitted").Select(entry => entry.Shift));
            return report;
        }

        internal static async Task<RunReport> Run(Settings settings, HubAccounts accounts, bool submit, List<Entry> demoJournal,
            Action<string> progress, IList<Slot> selected = null, DateTime? selectedMonday = null, IList<Decision> confirmed = null)
        {
            Planner.Validate(settings);
            if (submit && (selected == null || selected.Count == 0))
                throw new InvalidOperationException("Preview matches and check the shifts you want to apply for.");
            if (settings.Demo)
            {
                var sample = Sample();
                SetPreviewWeek(sample, selectedMonday);
                var from = Planner.At(sample.Monday, TimeSpan.Zero);
                sample.Shifts.RemoveAll(shift => shift.Start < from || shift.Start >= sample.Through);
                if (submit) await Task.Delay(200);
                await Process(sample, settings, submit, "demo", demoJournal,
                    _ => Task.FromResult(201), () => {}, progress, selected, confirmed);
                return sample;
            }

            var google = accounts.Google;
            var account = accounts.Sling;
            if (google == null || account == null) throw new InvalidOperationException("Connect both Google Calendar and Sling in Settings first.");
            if (settings.Calendars.Length == 0) throw new InvalidOperationException("Select at least one calendar to check in Settings.");
            var sling = new SlingClient(account);
            var googleClient = new GoogleClient(google);
            var now = DateTimeOffset.UtcNow;
            var report = new RunReport { Now = now };
            SetPreviewWeek(report, selectedMonday);
            var until = report.Through;
            var scheduleFrom = Planner.At(report.Monday.AddDays(-1), TimeSpan.Zero);
            progress("Reading Google Calendar and Sling…");
            await sling.VerifyAccount();
            var weekStart = Planner.At(report.Monday, TimeSpan.Zero);
            var firstOffer = now > weekStart ? now : weekStart;
            var available = firstOffer < until ? sling.Available(firstOffer, until) : Task.FromResult(new List<Slot>());
            var events = googleClient.Events(settings.Calendars, weekStart, until);
            var assigned = sling.Schedule(scheduleFrom, until);
            await Task.WhenAll(available, events, assigned);
            report.Shifts = await available;
            var scheduleUntil = report.Shifts.Select(shift => shift.End).Concat(new[] { until }).Max();
            if (scheduleUntil > until)
            {
                events = googleClient.Events(settings.Calendars, weekStart, scheduleUntil);
                assigned = sling.Schedule(scheduleFrom, scheduleUntil);
                await Task.WhenAll(events, assigned);
            }
            report.Calendar.AddRange(await events);
            report.Calendar.AddRange(await assigned);
            var journal = LocalData.Load("applications", new List<Entry>());
            await RecheckPriorApplications(report, sling.Key, journal, sling.CheckApplication);
            await Process(report, settings, submit, sling.Key, journal, sling.Apply, () => LocalData.Save("applications", journal), progress, selected, confirmed);
            if (report.Results.Any(row => row.Status == "Submitted"))
            {
                progress("Sling accepted " + report.ApplicationsSent + " applications. Checking assignment status…");
                try
                {
                    var fresh = await sling.Schedule(scheduleFrom, scheduleUntil);
                    foreach (var row in report.Results.Where(row => row.Status == "Submitted"))
                    {
                        if (fresh.Any(slot => slot.Kind == "Booked" && Planner.SameShift(slot, row.Shift)))
                        {
                            row.Status = "Assigned";
                            row.Reason = "Confirmed on your Sling schedule.";
                        }
                        else if (fresh.Any(slot => slot.Kind == "Pending" && Planner.SameOffer(slot, row.Shift)))
                        {
                            row.Status = "Pending approval";
                            row.Reason = "Sling shows your application on the available shift.";
                        }
                    }
                    report.Calendar.RemoveAll(item => item.Kind != "Calendar" && item.Kind != "All day" &&
                        (item.Kind != "Pending" || fresh.Any(slot => Planner.SameShift(slot, item) || Planner.SameOffer(slot, item))));
                    report.Calendar.AddRange(fresh);
                    AddAppliedShifts(report);
                }
                catch (Exception)
                {
                    report.Note = "Submissions were recorded. Sling could not refresh assignment status.";
                }
            }
            return report;
        }

        private static void SetPreviewWeek(RunReport report, DateTime? selectedMonday)
        {
            report.Monday = Planner.Monday(selectedMonday ?? Planner.Local(report.Now).Date);
            report.Through = Planner.At(report.Monday.AddDays(7), TimeSpan.Zero);
        }

        internal static bool SameSelection(Slot selected, Slot current)
        {
            return !String.IsNullOrEmpty(selected.ApplyId) && selected.ApplyId == current.ApplyId &&
                selected.Start == current.Start && selected.End == current.End;
        }

        internal static async Task RecheckPriorApplications(RunReport report, string account, List<Entry> journal,
            Func<Slot, Task<bool?>> check)
        {
            foreach (var offer in report.Shifts.Where(shift => shift.Kind == "Available" && shift.ApplicationListKnown))
            {
                var recorded = journal.Where(entry => entry.Account == account && entry.State == "Submitted" &&
                    SameSelection(entry.Shift, offer)).ToList();
                if (recorded.Count == 0) continue;
                if (report.Calendar.Any(slot => (slot.Kind == "Pending" && Planner.SameOffer(slot, offer)) ||
                    (slot.Kind == "Booked" && Planner.SameShift(slot, offer)))) continue;

                var active = await check(offer);
                if (active == true) offer.Kind = "Pending";
                if (active == false)
                    foreach (var entry in recorded) entry.State = "No longer listed";
            }
        }

        internal static async Task Process(RunReport report, Settings settings, bool submit, string account,
            List<Entry> journal, Func<Slot, Task<int>> apply, Action save, Action<string> progress, IList<Slot> selected = null,
            IList<Decision> confirmed = null)
        {
            var previous = journal.Where(entry => entry.Account == account && entry.Shift.End > report.Now &&
                entry.State != "No longer listed").ToList();
            var ready = new List<Decision>();
            foreach (var shift in report.Shifts.OrderBy(item => item.Start).ThenByDescending(item => item.End))
            {
                var existing = previous.FirstOrDefault(item => Planner.SameOffer(item.Shift, shift));
                if (report.Calendar.Any(item => item.Kind == "Booked" && Planner.SameShift(item, shift)))
                {
                    report.Results.Add(new Decision(shift, "Assigned", "Already on your Sling schedule."));
                    continue;
                }
                if (shift.Kind == "Pending" || report.Calendar.Any(item => item.Kind == "Pending" && Planner.SameOffer(item, shift)))
                {
                    report.Results.Add(new Decision(shift, "Pending approval", "Sling shows an existing application."));
                    continue;
                }
                if (existing != null)
                {
                    report.Results.Add(existing.State == "Uncertain"
                        ? new Decision(shift, "Check Sling", "A prior request had no definitive result. Check the shift in Sling.")
                        : new Decision(shift, "Already submitted", "A previous run submitted this shift."));
                    continue;
                }

                var row = Planner.Evaluate(shift, report.Calendar, settings, settings.Demo ? report.Now : DateTimeOffset.UtcNow);
                if ((row.Status == "Eligible" || row.Status == "Needs confirmation") && String.IsNullOrEmpty(shift.ApplyId))
                    row = new Decision(shift, "Skipped", "Sling did not provide a numeric application ID for this shift.");
                if (submit && row.Status == "Needs confirmation" && confirmed != null &&
                    confirmed.Any(approval => SameSelection(approval.Shift, shift) && approval.Reason == row.Reason))
                    row = new Decision(shift, "Eligible", "You confirmed the class exception for this shift.");
                if (row.Status == "Eligible" && submit && (selected == null || !selected.Any(item => SameSelection(item, shift))))
                {
                    report.Results.Add(new Decision(shift, "Eligible", "This shift was left unselected."));
                    continue;
                }
                if (row.Status == "Eligible" && journal.Any(record => record.Account == account &&
                    record.State == "No longer listed" && SameSelection(record.Shift, shift)))
                    row.Reason += " Sling no longer lists your earlier application.";
                report.Results.Add(row);
                if (row.Status != "Eligible" || !submit) continue;

                if (ready.Any(item => item.Shift.ApplyId == shift.ApplyId))
                {
                    row.Status = "Skipped";
                    row.Reason = "Sling returned the same application ID for another selected shift.";
                }
                else ready.Add(row);
            }

            report.ApplicationsSent = await SubmitApplications(ready, settings, account, journal, previous, apply, save, progress);
            if (submit && selected != null)
                foreach (var item in selected.Where(item => !report.Shifts.Any(shift => SameSelection(item, shift))))
                    report.Results.Add(new Decision(item, "No longer available", "The shift disappeared or its time changed after preview."));

            AddAppliedShifts(report, previous.Where(entry => entry.State == "Submitted").Select(entry => entry.Shift));
        }

        private static void AddAppliedShifts(RunReport report, IEnumerable<Slot> submitted = null)
        {
            var pending = report.Calendar.Concat(report.Shifts).Where(item => item.Kind == "Pending")
                .Concat(submitted ?? Enumerable.Empty<Slot>()).ToList();
            report.Calendar.RemoveAll(item => item.Kind == "Pending");
            foreach (var slot in pending)
            {
                if (report.Calendar.Any(item => (item.Kind == "Pending" && (ReferenceEquals(item, slot) || Planner.SameOffer(item, slot))) ||
                    (item.Kind == "Booked" && (Planner.SameShift(item, slot) || Planner.SameOffer(item, slot))))) continue;

                var current = report.Shifts.FirstOrDefault(shift => Planner.SameOffer(shift, slot));
                var applied = current ?? slot;
                report.Calendar.Add(applied.Kind == "Pending" ? applied : Pending(applied));
            }
        }

        private static async Task<int> SubmitApplications(List<Decision> ready, Settings settings, string account,
            List<Entry> journal, List<Entry> previous, Func<Slot, Task<int>> apply, Action save, Action<string> progress)
        {
            const int parallelism = 3;
            var stop = false;
            var sent = 0;
            for (var offset = 0; offset < ready.Count; offset += parallelism)
            {
                if (stop)
                {
                    foreach (var unsent in ready.Skip(offset))
                    {
                        unsent.Status = "Not sent";
                        unsent.Reason = "The run stopped after a Sling connection, rate-limit, or server error.";
                    }
                    break;
                }

                var batch = ready.Skip(offset).Take(parallelism).ToList();
                var entries = batch.Select(row => new Entry { Account = account, State = "Uncertain", Shift = row.Shift }).ToList();
                journal.AddRange(entries);
                save();

                progress("Submitting " + (offset + 1) + "–" + (offset + batch.Count) + " of " + ready.Count + " selected shifts…");
                var codes = await Task.WhenAll(batch.Select(row => SendApplication(row.Shift, apply)));
                for (var index = 0; index < batch.Count; index++)
                {
                    var row = batch[index];
                    var entry = entries[index];
                    var code = codes[index];
                    if (code == 201)
                    {
                        sent++;
                        entry.State = "Submitted";
                        row.Status = "Submitted";
                        row.Reason = settings.Demo ? "Simulated application. " + row.Reason : "Sling accepted your application. " + row.Reason;
                        previous.Add(entry);
                    }
                    else if (code == 0 || code >= 500 || code >= 200 && code < 300)
                    {
                        row.Status = "Check Sling";
                        row.Reason = "The submission outcome could not be verified. Check Sling before trying again.";
                        previous.Add(entry);
                        stop = true;
                    }
                    else
                    {
                        journal.Remove(entry);
                        row.Status = "Declined";
                        switch (code)
                        {
                            case 400: row.Reason = "Sling reported that this shift was cancelled (HTTP 400)."; break;
                            case 404: row.Reason = "Sling could no longer find this shift (HTTP 404)."; break;
                            case 405: row.Reason = "Sling did not permit this employee to apply for the shift (HTTP 405)."; break;
                            case 409: row.Reason = "Sling would not accept this application in the shift's current state (HTTP 409)."; break;
                            case 417: row.Reason = "Sling rejected the HTTP request (HTTP 417). Preview again before retrying."; break;
                            case 429: row.Reason = "Sling rate-limited the request (HTTP 429). Preview again before retrying."; break;
                            default: row.Reason = "Sling declined the request (HTTP " + code + ")."; break;
                        }
                        stop |= code == 401 || code == 403 || code == 429 || code >= 300 && code < 400;
                    }
                }
                save();
            }
            return sent;
        }

        private static async Task<int> SendApplication(Slot shift, Func<Slot, Task<int>> apply)
        {
            try { return await apply(shift).ConfigureAwait(false); }
            catch (HttpRequestException) { return 0; }
            catch (OperationCanceledException) { return 0; }
        }

        private static Slot Pending(Slot shift)
        {
            return new Slot { Id = shift.Id, ApplyId = shift.ApplyId, Name = shift.Name, Start = shift.Start, End = shift.End, Kind = "Pending",
                LocationId = shift.LocationId, PositionId = shift.PositionId, LocationName = shift.LocationName,
                PositionName = shift.PositionName, Notes = shift.Notes, OpenEnd = shift.OpenEnd };
        }

        internal static RunReport Sample()
        {
            var monday = new DateTime(2026, 11, 16);
            var sample = new RunReport { Now = Planner.At(monday, TimeSpan.FromHours(7)) };
            sample.Calendar.AddRange(new[] {
                Demo(monday, 0, "08:30", "09:50", "PHIL 334", "Calendar"),
                Demo(monday, 0, "12:30", "16:00", "MTSS Delivery", "Booked"),
                Demo(monday, 1, "08:30", "11:20", "CS 332", "Calendar"),
                Demo(monday, 1, "11:30", "12:50", "MATH 326", "Calendar"),
                Demo(monday, 1, "16:30", "21:00", "GITC Delivery", "Booked"),
                Demo(monday, 2, "08:30", "09:50", "PHIL 334", "Calendar"),
                Demo(monday, 2, "18:00", "20:50", "ECE 321", "Calendar"),
                Demo(monday, 3, "11:30", "12:50", "MATH 326", "Calendar"),
                Demo(monday, 4, "09:00", "12:30", "Fenster Delivery", "Booked"),
            });
            sample.Shifts.AddRange(new[] {
                Demo(monday, 0, "09:00", "12:30", "Fenster Delivery", "Available", "1"),
                Demo(monday, 1, "12:30", "16:00", "MTSS Delivery", "Available", "2"),
                Demo(monday, 2, "16:30", "21:00", "GITC Delivery", "Available", "3"),
                Demo(monday, 3, "12:30", "16:00", "PC Mall Delivery", "Available", "4"),
                Demo(monday, 4, "13:00", "16:30", "Fenster Delivery", "Available", "5"),
            });
            return sample;
        }

        private static Slot Demo(DateTime monday, int day, string start, string end, string name, string kind, string id = null)
        {
            return new Slot { Start = Planner.At(monday.AddDays(day), Planner.Time(start)), End = Planner.At(monday.AddDays(day), Planner.Time(end)),
                Name = name, Kind = kind, Id = id ?? name, ApplyId = id };
        }
    }
}

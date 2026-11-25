using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Toolbox.Tests
{
    internal static class ReviewChecks
    {
        internal static async Task Run()
        {
            var day = Planner.Local(DateTimeOffset.UtcNow).Date.AddDays(3);
            var offer = new Slot { Id = "10", ApplyId = "10", Kind = "Available", Name = "Fixture", ApplicationListKnown = true,
                Start = Planner.At(day, TimeSpan.FromHours(12)), End = Planner.At(day, TimeSpan.FromHours(16)) };
            var report = new RunReport { Now = DateTimeOffset.UtcNow, Shifts = new List<Slot> { offer } };
            var entry = new Entry { Account = "1:2", State = "Uncertain", Shift = offer };
            var journal = new List<Entry> { entry };
            var checks = 0;
            var changed = await ShiftRunner.RecheckPriorApplications(report, "1:2", journal, shift => { checks++; return Task.FromResult<bool?>(null); });
            Require(!changed && checks == 1 && entry.State == "Uncertain", "Uncertain requests were not checked, or an unknown outcome was discarded.");
            changed = await ShiftRunner.RecheckPriorApplications(report, "1:2", journal, shift => Task.FromResult<bool?>(true));
            Require(changed && entry.State == "Submitted" && offer.Kind == "Pending", "A confirmed application stayed uncertain or was not marked for saving.");
            entry.State = "Uncertain"; offer.Kind = "Available";
            await ShiftRunner.RecheckPriorApplications(report, "1:2", journal, shift => Task.FromResult<bool?>(false));
            Require(entry.State == "No longer listed", "A definitively absent request could not be retried.");
            entry.State = "Uncertain"; offer.Kind = "Available"; offer.ApplicationListKnown = false;
            await ShiftRunner.RecheckPriorApplications(report, "1:2", journal, shift => Task.FromResult<bool?>(true));
            Require(entry.State == "Submitted", "Missing offer-list metadata prevented detail reconciliation.");
            entry.State = "Uncertain"; offer.Kind = "Available";
            await ShiftRunner.RecheckPriorApplications(report, "another-account", journal, shift => { throw new Exception("Cross-account check"); });
            Require(entry.State == "Uncertain", "Another account's history was changed.");

            entry.State = "Uncertain";
            var sent = 0;
            await ShiftRunner.Process(new RunReport { Now = DateTimeOffset.UtcNow, Shifts = new List<Slot> { offer } }, new Settings(), true,
                "1:2", journal, shift => { sent++; return Task.FromResult(201); }, () => {}, message => {}, new[] { offer });
            Require(sent == 0, "An unresolved request was submitted twice.");
            await ShiftRunner.RecheckPriorApplications(report, "1:2", journal, shift => Task.FromResult<bool?>(false));
            await ShiftRunner.Process(new RunReport { Now = DateTimeOffset.UtcNow, Shifts = new List<Slot> { offer } }, new Settings(), true,
                "1:2", journal, shift => { sent++; return Task.FromResult(201); }, () => {}, message => {}, new[] { offer });
            Require(sent == 1, "A definitively absent request could not be selected and submitted again.");
            await CheckSubmissionPacing(offer.Start);
            await CheckUnreadableOffers();
            await CheckErrorDetails(offer);

            var folder = Path.GetFullPath(Path.Combine("dist", "SlingTests", "journal-review-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "applications.json");
            Require(ShiftRunner.ReadJournal(path).Count == 0, "A fresh account required an existing journal.");
            foreach (var invalid in new[] { "null", "[null]", "[{}]", "not json" })
            {
                File.WriteAllText(path, invalid);
                try { ShiftRunner.ReadJournal(path); throw new Exception("Damaged history accepted"); }
                catch (InvalidOperationException exception) { Require(exception.Message.Contains("not duplicated"), "Damaged history failed without recovery guidance."); }
                Require(File.ReadAllText(path) == invalid, "Damaged history was silently erased.");
            }
            File.WriteAllText(path, LocalData.Json().Serialize(journal));
            Require(ShiftRunner.ReadJournal(path).Count == journal.Count, "Valid history could not be restored.");

            foreach (var monday in new[] { new DateTime(2027, 3, 8), new DateTime(2027, 11, 8) })
            {
                var sample = await ShiftRunner.ReadWeek(new Settings { Demo = true }, null, monday, new List<Entry>());
                var until = Planner.At(monday.AddDays(7), TimeSpan.Zero);
                Require(sample.Calendar.Count > 0 && sample.Shifts.Count > 0 && sample.Calendar.Concat(sample.Shifts)
                    .All(slot => slot.Start >= Planner.At(monday, TimeSpan.Zero) && slot.End <= until), "Demo navigation loaded a different week's data.");
                var preview = await ShiftRunner.Run(new Settings { Demo = true }, null, false, new List<Entry>(), message => {}, selectedMonday: monday);
                Require(preview.Shifts.Count > 0 && preview.Results.Count > 0, "Demo preview lost the selected week's offers.");
            }
            Console.WriteLine("Passed uncertain application reconciliation, account isolation, and demo selected-week checks.");
        }

        private static async Task CheckSubmissionPacing(DateTimeOffset start)
        {
            var offers = Enumerable.Range(1, 4).Select(id => new Slot { Id = id.ToString(), ApplyId = id.ToString(), Kind = "Available",
                Name = "Fixture " + id, Start = start.AddHours(id), End = start.AddHours(id + 2) }).ToList();
            var clock = Stopwatch.StartNew();
            var journal = new List<Entry>();
            var sentAt = new List<TimeSpan>();
            var report = new RunReport { Now = DateTimeOffset.UtcNow, Shifts = offers };
            var saves = 0;
            await ShiftRunner.Process(report, new Settings(), true, "fixture", journal, shift =>
            {
                sentAt.Add(clock.Elapsed);
                var expectedSaves = sentAt.Count <= 2 ? 1 : 3;
                Require(saves == expectedSaves && journal.Count(item => item.State == "Uncertain") == 1 &&
                    ReferenceEquals(journal.Last().Shift, shift), "A request started without its own saved journal entry.");
                if (sentAt.Count == 3) throw new TaskCanceledException();
                return Task.FromResult(sentAt.Count == 1 ? 417 : 201);
            }, () => saves++, message => {}, offers);
            Require(sentAt.Count == 3 && report.ApplicationsSent == 1 && sentAt.Zip(sentAt.Skip(1), (first, next) => next - first)
                .All(delay => delay >= TimeSpan.FromMilliseconds(105)), "Requests overlapped or failed to back off after a 417.");
            Require(report.Results.Select(row => row.Status).SequenceEqual(new[] { "Submitted", "Check Sling", "Not sent", "Not sent" }) &&
                report.Results[1].Reason.Contains("timed out") && journal.Select(item => item.State).SequenceEqual(new[] { "Submitted", "Uncertain" }),
                "A timeout was treated as a rejection or caused an unsafe retry.");

            journal.Clear();
            report = new RunReport { Now = DateTimeOffset.UtcNow, Shifts = offers };
            var calls = 0;
            await ShiftRunner.Process(report, new Settings { Demo = true }, true, "fixture", journal,
                shift => { calls++; return Task.FromResult(417); }, () => {}, message => {}, offers);
            Require(calls == 10 && report.Results[0].Status == "Declined" && report.Results.Skip(1).All(row => row.Status == "Not sent" && row.Reason.Contains("417")) && journal.Count == 0,
                "Repeated 417 rejections did not pause the remaining queue.");
        }

        private static async Task CheckUnreadableOffers()
        {
            var monday = Planner.Monday(Planner.Local(DateTimeOffset.UtcNow).Date).AddDays(7);
            Func<int, int, string> at = (day, hour) => Planner.At(monday.AddDays(day), TimeSpan.FromHours(hour)).ToString("o");
            Func<string, string, string, Dictionary<string, object>> offer = (id, start, end) =>
            {
                var item = new Dictionary<string, object> { { "id", id }, { "dtstart", start } };
                if (end != null) item["dtend"] = end;
                return item;
            };
            var openPending = offer("1", at(1, 9), null);
            openPending["openEnd"] = true;
            openPending["applied"] = true;
            var laterOpenPending = offer("2", at(15, 9), null);
            laterOpenPending["openEnd"] = true;
            laterOpenPending["applied"] = true;
            var shifts = new object[] {
                openPending, laterOpenPending,
                offer("3", at(20, 9), "not a time"),
                offer("4", at(2, 9), "not a time"),
                offer("5", "not a time", at(2, 12)),
                offer("6", at(3, 9), at(3, 13)),
            };
            var client = new SlingClient(new SlingConnection { Org = "1", User = "2" }, (path, label, token) =>
            {
                if (path == "groups") return Task.FromResult<object>(new object[0]);
                if (path.StartsWith("shifts/available")) return Task.FromResult<object>(new Dictionary<string, object> { { "shifts", shifts } });
                throw new InvalidOperationException("Unexpected fixture request " + path);
            });
            var week = await client.Available(Planner.At(monday, TimeSpan.Zero), Planner.At(monday.AddDays(7), TimeSpan.Zero));
            Require(week.Count == 2 && week.Any(slot => slot.Id == "6" && slot.Kind == "Available") &&
                week.Any(slot => slot.Id == "1" && slot.Kind == "Pending" && slot.OpenEnd && slot.End > slot.Start),
                "An open-ended pending offer or a malformed offer stopped the week's preview.");
            Require(client.UnreadableOffers == 2, "Unreadable offers in the selected week were not counted, or other weeks were counted.");

            var pendingEvent = new Dictionary<string, object> { { "id", "7" }, { "dtstart", at(1, 9) }, { "openEnd", true }, { "available", true },
                { "user", new Dictionary<string, object> { { "id", "9" } } },
                { "applications", new object[] { new Dictionary<string, object> { { "userId", "2" }, { "status", "pending" } } } } };
            var schedule = SlingClient.ParseSchedule(new object[] { pendingEvent }, "2");
            Require(schedule.Count == 1 && schedule[0].Kind == "Pending", "An open-ended pending shift stopped the schedule.");
            try
            {
                SlingClient.ParseSchedule(new object[] { offer("8", at(1, 9), "not a time") }, "2");
                throw new Exception("An unreadable booked shift was accepted");
            }
            catch (InvalidOperationException) { /* Booked shifts block conflicts, so unreadable ones must still stop the run. */ }
        }

        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

        private static async Task CheckErrorDetails(Slot offer)
        {
            using (var response = new HttpResponseMessage(HttpStatusCode.ExpectationFailed))
            {
                response.Headers.Add("X-Request-ID", "fixture-request");
                response.Content = new StringContent("{\"developerMessage\":\"Cannot apply (fixture-secret)\",\"requestId\":\"fixture-json-id\",\"token\":\"do-not-show\"}", Encoding.UTF8, "application/json");
                offer.ApplicationDiagnostics = await SlingApplicationDiagnostics.Read(response, "fixture-secret", CancellationToken.None);
                var row = new ResultItem(new Decision(offer, "Declined", "HTTP 417"), false, () => {});
                Require(row.HasDiagnostics && row.Diagnostics.Contains("fixture-request") && row.Diagnostics.Contains("fixture-json-id") &&
                    row.Diagnostics.Contains("Cannot apply ([redacted])") && !row.Diagnostics.Contains("fixture-secret") && !row.Diagnostics.Contains("do-not-show"),
                    "Failed application did not surface useful redacted diagnostics.");
                Require(!LocalData.Json().Serialize(offer).Contains("fixture-request"), "Diagnostics leaked into the application journal.");
            }
            using (var response = new HttpResponseMessage(HttpStatusCode.ExpectationFailed))
            {
                response.Content = new StringContent("<html>Fixture gateway page</html>", Encoding.UTF8, "text/html");
                var details = await SlingApplicationDiagnostics.Read(response, null, CancellationToken.None);
                Require(details.Contains("HTML") && !details.Contains("Fixture gateway page"), "Gateway markup was copied into the diagnostic.");
            }
        }
    }
}

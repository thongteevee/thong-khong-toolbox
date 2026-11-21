using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}

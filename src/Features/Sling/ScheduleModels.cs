using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace Toolbox
{
    public sealed class Rule
    {
        public bool Enabled { get; set; }
        public string Title { get; set; }
        public string Days { get; set; }
        public string Ready { get; set; }
        public string Mode { get; set; }
        public string Dates { get; set; }
    }

    public sealed class Settings
    {
        internal const int CurrentVersion = 2;
        public int Version { get; set; }
        public bool Demo { get; set; }
        public string[] Calendars { get; set; }
        public List<Rule> Rules { get; set; }

        public Settings()
        {
            Calendars = new string[0];
            Rules = new List<Rule> { new Rule { Enabled = true, Title = "MATH 326", Days = "Tue, Thu", Ready = "12:30", Mode = "Confirmed dates", Dates = "" } };
        }
    }

    public sealed class Slot
    {
        public string Id { get; set; }
        public string ApplyId { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; }
        public string LocationId { get; set; }
        public string PositionId { get; set; }
        public string PositionName { get; set; }
        public string LocationName { get; set; }
        public string Notes { get; set; }
        public bool OpenEnd { get; set; }
        internal bool ApplicationListKnown { get; set; }
        internal List<WorkPeriod> WorkPeriods { get; set; }
        internal bool TimesheetUnavailable { get; set; }
        internal Slot ScheduledShift { get; private set; }
        internal string WorkStatus { get; private set; }
        internal double? PaidMinutes { get; private set; }
        [ScriptIgnore] public DateTimeOffset Start { get; set; }
        [ScriptIgnore] public DateTimeOffset End { get; set; }
        public string StartIso { get { return Start.ToString("o"); } set { Start = DateTimeOffset.Parse(value, CultureInfo.InvariantCulture); } }
        public string EndIso { get { return End.ToString("o"); } set { End = DateTimeOffset.Parse(value, CultureInfo.InvariantCulture); } }

        internal Slot ForWorkPeriod(WorkPeriod period)
        {
            var display = (Slot)MemberwiseClone();
            display.ScheduledShift = this;
            display.Start = period.Start;
            display.End = period.End;
            display.WorkStatus = period.Approved ? "Time approved" : "Worked";
            display.PaidMinutes = period.PaidMinutes;
            return display;
        }
    }

    internal sealed class WorkPeriod
    {
        internal DateTimeOffset Start;
        internal DateTimeOffset End;
        internal bool Approved;
        internal double? PaidMinutes;
    }

    public sealed class Entry
    {
        public string Account { get; set; }
        public string State { get; set; }
        public Slot Shift { get; set; }
    }

    public sealed class Decision
    {
        public Slot Shift;
        public string Status;
        public string Reason;
        internal bool IsApplied { get { return Status == "Submitted" || Status == "Already submitted" || Status == "Pending approval"; } }
        public Decision(Slot shift, string status, string reason)
        {
            Shift = shift;
            Status = status;
            Reason = reason;
        }
    }

    internal sealed class RunReport
    {
        internal DateTimeOffset Now;
        internal List<Slot> Calendar = new List<Slot>();
        internal List<Slot> Shifts = new List<Slot>();
        internal List<Decision> Results = new List<Decision>();
        internal string Note;
        internal int ApplicationsSent;
        internal DateTime Monday;
        internal DateTimeOffset Through;
    }
}

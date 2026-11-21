using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Toolbox
{
    public static class Planner
    {
        public static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        public static DateTimeOffset Local(DateTimeOffset date) { return TimeZoneInfo.ConvertTime(date, Eastern); }
        public static DateTimeOffset At(DateTime date, TimeSpan time)
        {
            var wall = DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Unspecified);
            return new DateTimeOffset(wall, Eastern.GetUtcOffset(wall));
        }

        public static DateTime Monday(DateTime date) { return date.Date.AddDays(-((int)date.DayOfWeek + 6) % 7); }
        public static TimeSpan Time(string value)
        {
            TimeSpan time;
            if (!TimeSpan.TryParseExact(value, "hh\\:mm", CultureInfo.InvariantCulture, out time))
                throw new InvalidOperationException("Enter times in 24-hour HH:mm format, such as 12:30.");
            return time;
        }

        public static string[] Tokens(string text)
        {
            return (text ?? "").Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim()).Where(item => item.Length > 0).ToArray();
        }

        public static void Validate(Settings settings)
        {
            if (settings == null || settings.Rules == null || settings.Rules.Any(rule => rule == null) ||
                settings.Calendars == null || settings.Calendars.Any(String.IsNullOrWhiteSpace))
                throw new InvalidOperationException("Saved calendars or rules are incomplete. Review Sling settings.");
            foreach (var rule in settings.Rules.Where(rule => rule.Enabled))
            {
                if (String.IsNullOrWhiteSpace(rule.Title)) throw new InvalidOperationException("Give each enabled rule an event title to match.");
                Time(rule.Ready);
                if (Tokens(rule.Days).Any(day => !new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }.Contains(day, StringComparer.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Use weekday abbreviations such as Tue, Thu in special rules.");
                if (rule.Mode != "Recurring" && rule.Mode != "Confirmed dates")
                    throw new InvalidOperationException("Choose Recurring or Confirmed dates for each enabled rule.");
                foreach (var date in Tokens(rule.Dates))
                {
                    DateTime parsed;
                    if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                        throw new InvalidOperationException("Enter confirmed dates as YYYY-MM-DD, separated by commas.");
                }
            }
        }

        public static bool SameShift(Slot left, Slot right)
        {
            if (left.Start != right.Start || left.End != right.End) return false;
            if (!String.IsNullOrEmpty(left.Id) && left.Id == right.Id) return true;
            return !String.IsNullOrEmpty(left.LocationId) && !String.IsNullOrEmpty(left.PositionId)
                && left.LocationId == right.LocationId && left.PositionId == right.PositionId;
        }

        public static bool SameOffer(Slot left, Slot right)
        {
            return left.Start == right.Start && ((!String.IsNullOrEmpty(left.Id) && left.Id == right.Id) ||
                (!String.IsNullOrEmpty(left.ApplyId) && left.ApplyId == right.ApplyId));
        }

        public static Decision Evaluate(Slot shift, IList<Slot> busy, Settings settings, DateTimeOffset now)
        {
            if (shift.Start <= now) return new Decision(shift, "Skipped", "The shift has already started.");
            if (shift.OpenEnd) return new Decision(shift, "Skipped", "Sling lists an open-ended shift.");
            string allowedBy = null;
            var needsConfirmation = new List<string>();
            foreach (var block in busy)
            {
                if (block.Kind == "Pending" || block.Kind == "Sling closure" || shift.Start >= block.End || shift.End <= block.Start) continue;
                var blockStart = Local(block.Start);
                var blockEnd = Local(block.End);
                var weekday = blockStart.ToString("ddd", CultureInfo.InvariantCulture);
                var date = blockStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var matching = settings.Rules.Where(rule => rule.Enabled && block.Kind == "Calendar"
                    && blockStart.Date == blockEnd.Date
                    && block.Name.IndexOf(rule.Title, StringComparison.OrdinalIgnoreCase) >= 0
                    && (Tokens(rule.Days).Length == 0 || Tokens(rule.Days).Contains(weekday, StringComparer.OrdinalIgnoreCase))
                    && Time(rule.Ready) > blockStart.TimeOfDay && Time(rule.Ready) <= blockEnd.TimeOfDay
                    && shift.Start >= At(blockStart.Date, Time(rule.Ready))).ToList();
                if (matching.Count == 0)
                    return new Decision(shift, "Skipped", "Conflicts with " + block.Name + ".");
                if (matching.Any(rule => rule.Mode == "Recurring" || Tokens(rule.Dates).Contains(date)))
                    allowedBy = block.Name;
                else
                    needsConfirmation.Add("Confirm " + block.Name + " for " + date + ".");
            }
            if (needsConfirmation.Count > 0)
                return new Decision(shift, "Needs confirmation", String.Join("\n", needsConfirmation.Distinct().OrderBy(item => item, StringComparer.Ordinal)));

            return new Decision(shift, "Eligible", allowedBy == null ? "Fits your schedule." : "Allowed by your " + allowedBy + " exception.");
        }
    }
}

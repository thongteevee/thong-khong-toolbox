using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Toolbox
{
    public sealed class SlingConnection
    {
        public string Token { get; set; }
        public string Org { get; set; }
        public string User { get; set; }
        public string Display { get; set; }
    }

    internal sealed class SlingClient
    {
        private const string SlingRoot = "https://api.getsling.com/v1/";
        private readonly SlingConnection account;
        private readonly Lazy<Task<Dictionary<string, string>>> groupNames;
        private readonly Func<string, string, CancellationToken, Task<object>> read;
        internal bool TimesheetsUnavailable { get; private set; }

        internal SlingClient(SlingConnection connection, Func<string, string, CancellationToken, Task<object>> read = null)
        {
            account = connection;
            this.read = read;
            groupNames = new Lazy<Task<Dictionary<string, string>>>(LoadGroupNames);
        }

        private async Task<Dictionary<string, string>> LoadGroupNames()
        {
            var names = new Dictionary<string, string>();
            try
            {
                var data = await Get("groups", "Sling positions and locations").ConfigureAwait(false);
                foreach (var group in Api.List(data, "Sling positions and locations", "groups", "items", "data", "results"))
                {
                    var id = Api.PositiveId(Api.Text(group, "id"));
                    var name = Api.Text(group, "name").Trim();
                    if (id != null && name.Length > 0) names[id] = name;
                }
            }
            catch (InvalidOperationException) { /* Some employee accounts cannot list organization groups. */ }
            catch (HttpRequestException) { /* Shift data is still usable with the names it includes. */ }
            catch (TaskCanceledException) { /* A slow group lookup does not invalidate the schedule. */ }
            return names;
        }
        internal string Key { get { return account.Org + ":" + account.User; } }

        internal static async Task<SlingConnection> Connect(string email, string password, string token, string orgId)
        {
            if (String.IsNullOrWhiteSpace(token))
            {
                if (String.IsNullOrWhiteSpace(email) || String.IsNullOrEmpty(password)) throw new InvalidOperationException("Enter a Sling email and password or a Sling authorization token.");
                var json = LocalData.Json().Serialize(new { email = email.Trim(), password = password });
                using (var reply = await Api.Send(SlingRoot + "account/login", HttpMethod.Post, null, new StringContent(json, Encoding.UTF8, "application/json")).ConfigureAwait(false))
                {
                    if (!reply.IsSuccessStatusCode) throw Api.Error("Sling sign-in", reply);
                    IEnumerable<string> values;
                    if (!reply.Headers.TryGetValues("Authorization", out values))
                        throw new InvalidOperationException("Sling did not provide a session token. For Toast single sign-on, use a token from your signed-in Sling session.");
                    token = values.FirstOrDefault();
                }
            }
            if (String.IsNullOrWhiteSpace(token) || token.Contains("\r") || token.Contains("\n")) throw new InvalidOperationException("Enter a valid Sling authorization token.");
            token = token.Trim();
            if (!String.IsNullOrWhiteSpace(orgId))
            {
                if (Api.PositiveId(orgId.Trim()) == null) throw new InvalidOperationException("Sling organization ID must be a positive number.");
                var json = LocalData.Json().Serialize(new { orgId = Int64.Parse(orgId.Trim(), CultureInfo.InvariantCulture) });
                using (var reply = await Api.Send(SlingRoot + "account/session", HttpMethod.Put, token, new StringContent(json, Encoding.UTF8, "application/json")).ConfigureAwait(false))
                    if (!reply.IsSuccessStatusCode) throw Api.Error("Sling organization selection", reply);
            }
            var client = new SlingClient(new SlingConnection { Token = token });
            var session = await client.Get("account/session", "Sling account").ConfigureAwait(false);
            return ParseSession(session, token);
        }

        internal static SlingConnection ParseSession(object session, string token)
        {
            session = Api.Pick(session, "session") ?? session;
            var user = Api.Pick(session, "user");
            var org = Api.Pick(session, "org");
            var orgId = Api.PositiveId(Api.Text(org, "id")) ?? Api.PositiveId(Api.Text(session, "orgId"));
            var userId = Api.PositiveId(Api.Text(user, "id")) ?? Api.PositiveId(Api.Text(session, "userId"));
            if (orgId == null || userId == null) throw new InvalidOperationException("Sling did not identify an organization and employee. Pick an organization in Sling or enter its ID in Settings.");
            var label = Api.Text(org, "name");
            return new SlingConnection { Token = token, Org = orgId, User = userId,
                Display = (label.Length == 0 ? "Organization " + orgId : label) + " / employee " + userId };
        }

        private async Task<object> Get(string path, string operation, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (read != null) return await read(path, operation, cancellationToken).ConfigureAwait(false);
            using (var response = await Api.Send(SlingRoot + path, HttpMethod.Get, account.Token, cancellationToken: cancellationToken).ConfigureAwait(false))
                return await Api.Json(response, operation).ConfigureAwait(false);
        }

        internal async Task VerifyAccount()
        {
            var current = ParseSession(await Get("account/session", "Sling account").ConfigureAwait(false), account.Token);
            if (current.Org != account.Org || current.User != account.User)
                throw new InvalidOperationException("The Sling session switched organization or employee. Reconnect the intended account in Settings.");
        }

        internal async Task<List<Slot>> Available(DateTimeOffset from, DateTimeOffset to)
        {
            var result = await Get("shifts/available?pagesize=500", "Sling available shifts").ConfigureAwait(false);
            var names = await groupNames.Value.ConfigureAwait(false);
            var slots = Api.List(result, "Sling available shifts", "shifts", "events", "items", "data", "results", "availableShifts")
                .Where(item => !Inactive(item) && !Inactive(Api.Pick(item, "shift")))
                .Select(item =>
                {
                    var application = ApplicationStatus(item, account.User);
                    var slot = ParseShift(item, application == true ? "Pending" : "Available", names);
                    slot.ApplicationListKnown = application.HasValue;
                    return slot;
                })
                .Where(slot => slot.Start >= from && slot.Start < to).ToList();
            var known = slots.Where(slot => !String.IsNullOrEmpty(slot.Id) || !String.IsNullOrEmpty(slot.ApplyId))
                .GroupBy(slot => (slot.Id ?? "") + "|" + (slot.ApplyId ?? "") + "|" + slot.Start.ToUniversalTime().ToString("o"))
                .Select(group => group.FirstOrDefault(slot => slot.Kind == "Pending") ?? group.First());
            return known.Concat(slots.Where(slot => String.IsNullOrEmpty(slot.Id) && String.IsNullOrEmpty(slot.ApplyId))).ToList();
        }

        internal async Task<List<Slot>> Schedule(DateTimeOffset from, DateTimeOffset to)
        {
            var url = "calendar/" + account.Org + "/users/" + account.User + "?" + Api.Query("dates", from.ToString("o") + "/" + to.ToString("o"), "skipUnscheduled", "true", "showPlanningEvents", "false");
            var result = await Get(url, "Sling personal schedule").ConfigureAwait(false);
            return ParseSchedule(result, account.User, await groupNames.Value.ConfigureAwait(false));
        }

        // Display-only data is fetched after eligibility checks and any shift submissions.
        internal async Task EnrichTimesheets(IEnumerable<Slot> schedule, TimeSpan? timeout = null)
        {
            TimesheetsUnavailable = false;
            var historical = schedule.Where(slot => slot.Kind == "Booked" && slot.Start <= DateTimeOffset.UtcNow).ToList();
            using (var cancellation = new CancellationTokenSource())
            using (var gate = new SemaphoreSlim(3))
            {
                cancellation.CancelAfter(timeout ?? TimeSpan.FromSeconds(8));
                await Task.WhenAll(historical.Select(async shift =>
                {
                    var entered = false;
                    shift.WorkPeriods = null;
                    shift.TimesheetUnavailable = false;
                    try
                    {
                        await gate.WaitAsync(cancellation.Token).ConfigureAwait(false);
                        entered = true;
                        var details = await Get("timesheets/event/" + Uri.EscapeDataString(shift.Id) + "/" + account.User,
                            "Sling timesheet", cancellation.Token).ConfigureAwait(false);
                        shift.WorkPeriods = ParseWorkPeriods(details, shift, account.User);
                    }
                    catch (InvalidOperationException) { shift.TimesheetUnavailable = true; }
                    catch (HttpRequestException) { shift.TimesheetUnavailable = true; }
                    catch (OperationCanceledException) { shift.TimesheetUnavailable = true; }
                    finally { if (entered) gate.Release(); }
                })).ConfigureAwait(false);
            }
            TimesheetsUnavailable = historical.Any(shift => shift.TimesheetUnavailable);
        }

        internal static List<WorkPeriod> ParseWorkPeriods(object document, Slot shift, string employee)
        {
            var data = Api.Pick(document, "event") ?? document;
            var identifier = Api.Text(data, "id");
            if (identifier != shift.Id) throw new InvalidOperationException("Sling returned a timesheet for a different shift.");
            var projections = Api.Pick(data, "timesheetProjections") as object[];
            if (projections == null) throw new InvalidOperationException("Sling did not return timesheet status.");
            var periods = new List<WorkPeriod>();
            foreach (var item in projections)
            {
                if (Api.Flag(item, "deleted")) continue;
                var user = Api.PositiveId(Api.Text(Api.Pick(item, "user"), "id")) ?? Api.PositiveId(Api.Text(item, "userId"));
                if (user != employee) continue;
                var eventId = Api.Text(Api.Pick(item, "event"), "id");
                if (eventId.Length > 0 && eventId != shift.Id) continue;
                var status = Api.Text(item, "status");
                if (new[] { "cancelled", "canceled", "deleted", "rejected", "denied", "void" }.Contains(status, StringComparer.OrdinalIgnoreCase)) continue;
                DateTimeOffset start, end;
                if (String.IsNullOrWhiteSpace(Api.Text(item, "clockOut"))) continue;
                if (!DateTimeOffset.TryParse(Api.Text(item, "clockIn"), CultureInfo.InvariantCulture, DateTimeStyles.None, out start) ||
                    !DateTimeOffset.TryParse(Api.Text(item, "clockOut"), CultureInfo.InvariantCulture, DateTimeStyles.None, out end) || end <= start)
                    throw new InvalidOperationException("Sling returned unreadable timesheet times.");
                var rawMinutes = Api.Pick(item, "paidMinutes");
                double paid;
                var paidMinutes = rawMinutes != null && Double.TryParse(Convert.ToString(rawMinutes, CultureInfo.InvariantCulture),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out paid) && paid >= 0 && !Double.IsInfinity(paid) && !Double.IsNaN(paid) ? (double?)paid : null;
                periods.Add(new WorkPeriod { Start = start, End = end, Approved = String.Equals(status, "approved", StringComparison.OrdinalIgnoreCase), PaidMinutes = paidMinutes });
            }
            return periods.GroupBy(period => new { period.Start, period.End }).Select(group => group.OrderByDescending(period => period.Approved).First())
                .OrderBy(period => period.Start).ToList();
        }

        internal static List<Slot> ParseSchedule(object data, string employee, IDictionary<string, string> names = null)
        {
            return Api.List(data, "Sling personal schedule", "events", "shifts", "items", "data", "results", "calendar")
                .Where(item => !Inactive(item)).Select(item => Api.Pick(item, "shift") ?? item)
                .Where(item => !Inactive(item)).Select(item =>
                {
                    var owner = Api.Text(Api.Pick(item, "user"), "id");
                    if (owner.Length == 0) owner = Api.Text(item, "userId");
                    if (Api.Flag(item, "available") && owner != employee)
                        return HasApplication(item, employee) ? ParseShift(item, "Pending", names) : null;
                    if (owner.Length > 0 && owner != employee) return null;
                    var type = Api.Text(item, "type");
                    if (type == "note" || type == "dayMark" || type == "day_mark") return null;
                    if (type == "business_closed") return ParseShift(item, "Sling closure", names);
                    var kind = type == "shift" || owner == employee && (GroupId(item, "position").Length > 0 || GroupId(item, "location").Length > 0 ||
                        Api.Pick(item, "position") != null || Api.Pick(item, "location") != null)
                        ? "Booked" : "Sling busy";
                    return ParseShift(item, kind, names);
                }).Where(item => item != null).ToList();
        }

        internal static bool HasApplication(object item, string employee) { return ApplicationStatus(item, employee) == true; }

        internal static bool? ApplicationStatus(object item, string employee)
        {
            var shift = Api.Pick(item, "shift") ?? item;
            if (Api.Flag(shift, "applied")) return true;
            var applications = Api.Pick(shift, "applications") as object[];
            if (applications == null) return null;

            var complete = true;
            foreach (var application in applications)
            {
                var userId = Api.PositiveId(Api.Text(Api.Pick(application, "user"), "id"))
                    ?? Api.PositiveId(Api.Text(application, "userId")) ?? Api.PositiveId(Api.Text(application, "user"));
                if (userId == null) complete = false;
                if (userId == employee && !new[] { "denied", "rejected", "retracted", "withdrawn", "cancelled", "canceled" }
                    .Contains(Api.Text(application, "status"), StringComparer.OrdinalIgnoreCase)) return true;
            }
            return complete ? (bool?)false : null;
        }

        internal async Task<bool?> CheckApplication(Slot offer)
        {
            try
            {
                var data = await Get("shifts/" + offer.ApplyId, "Sling shift details").ConfigureAwait(false);
                var shift = Api.Pick(data, "shift") ?? data;
                if (Inactive(shift) || !ShiftRunner.SameSelection(offer, ParseShift(shift, "Available"))) return null;
                return ApplicationStatus(shift, account.User);
            }
            catch (InvalidOperationException) { return null; }
            catch (HttpRequestException) { return null; }
            catch (TaskCanceledException) { return null; }
        }

        private static bool Inactive(object item)
        {
            return Api.Flag(item, "deleted") || new[] { "cancelled", "canceled", "deleted", "planning", "unpublished" }
                .Contains(Api.Text(item, "status"), StringComparer.OrdinalIgnoreCase);
        }

        internal static Slot ParseShift(object item, string kind, IDictionary<string, string> names = null)
        {
            var shift = Api.Pick(item, "shift") ?? item;
            DateTimeOffset start, end;
            var open = Api.Flag(shift, "openEnd");
            if (!DateTimeOffset.TryParse(Api.Text(shift, "dtstart"), CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
                throw new InvalidOperationException("Sling returned an event with an unreadable start time.");
            if (!DateTimeOffset.TryParse(Api.Text(shift, "dtend"), CultureInfo.InvariantCulture, DateTimeStyles.None, out end) || end <= start)
            {
                if (!open || kind != "Available") throw new InvalidOperationException("Sling returned an event with an unreadable end time.");
                end = Planner.At(Planner.Local(start).Date.AddDays(1), TimeSpan.Zero);
            }
            var id = Api.Text(shift, "id");
            var applyId = Api.PositiveId(Api.Text(shift, "shiftId")) ?? Api.PositiveId(Api.Text(shift, "eventId")) ?? Api.PositiveId(id);
            var position = Api.Pick(shift, "position");
            var location = Api.Pick(shift, "location");
            var positionId = GroupId(shift, "position");
            var locationId = GroupId(shift, "location");
            var positionName = GroupName(position, positionId, names);
            var locationName = GroupName(location, locationId, names);
            var summary = Api.Text(shift, "summary").Trim();
            var name = new[] { Api.Text(shift, "name"), Api.Text(shift, "title") }
                .FirstOrDefault(text => !String.IsNullOrWhiteSpace(text));
            if (name == null)
                name = String.Join(" / ", new[] { positionName, locationName }.Where(text => text.Length > 0).Distinct());
            if (name.Length == 0)
                name = kind == "Sling closure" ? (summary.Length > 0 ? summary : "Business closed") :
                    kind == "Sling busy" ? (summary.Length > 0 ? summary : "Sling calendar event")
                    : (id.Length > 0 ? "Sling shift " + id : "Sling shift");
            return new Slot { Id = id, ApplyId = applyId, Name = name.Trim(), Start = start, End = end, Kind = kind,
                LocationId = locationId, PositionId = positionId, LocationName = locationName, PositionName = positionName,
                Notes = kind != "Sling busy" ? summary : null, OpenEnd = open };
        }

        private static string GroupId(object shift, string field)
        {
            return Api.PositiveId(Api.Text(Api.Pick(shift, field), "id"))
                ?? Api.PositiveId(Api.Text(shift, field + "Id"))
                ?? Api.PositiveId(Api.Text(shift, field)) ?? "";
        }

        private static string GroupName(object group, string id, IDictionary<string, string> names)
        {
            var label = Api.Text(group, "name").Trim();
            string resolved;
            return label.Length == 0 && names != null && names.TryGetValue(id, out resolved) ? resolved : label;
        }

        internal async Task<int> Apply(Slot shift)
        {
            shift.ApplicationDiagnostics = null;
            // Sling accepts this application endpoint as an empty POST without a 100-continue handshake.
            var empty = new ByteArrayContent(new byte[0]);
            empty.Headers.ContentLength = 0;
            using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12)))
            using (var response = await Api.Send(SlingRoot + "shifts/" + shift.ApplyId + "/applications?ignoreConflicts=false",
                HttpMethod.Post, account.Token, empty, expectContinue: false, cancellationToken: deadline.Token,
                completion: HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                var code = (int)response.StatusCode;
                if (code == 417)
                    shift.ApplicationDiagnostics = await SlingApplicationDiagnostics.Read(response, account.Token, deadline.Token).ConfigureAwait(false);
                return code;
            }
        }
    }
}

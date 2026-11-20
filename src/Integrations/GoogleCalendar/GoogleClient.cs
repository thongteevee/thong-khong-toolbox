using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Toolbox
{
    public sealed class GoogleCredential
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string RefreshToken { get; set; }
    }

    public sealed class GoogleCalendar
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool Primary { get; set; }
        public override string ToString() { return Name; }
    }

    internal sealed class GoogleClient
    {
        private const string CalendarRoot = "https://www.googleapis.com/calendar/v3/";
        private const string Scopes = "https://www.googleapis.com/auth/calendar.events.readonly https://www.googleapis.com/auth/calendar.calendarlist.readonly";
        private readonly GoogleCredential credential;
        private string accessToken;
        internal GoogleClient(GoogleCredential credential) { this.credential = credential; }

        private static string RandomString()
        {
            var bytes = new byte[32];
            using (var generator = RandomNumberGenerator.Create()) generator.GetBytes(bytes);
            return Base64Url(bytes);
        }

        private static string Base64Url(byte[] value) { return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }

        internal static GoogleCredential PackagedClient()
        {
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("GoogleOAuthClient"))
            {
                if (resource != null)
                {
                    using (var reader = new StreamReader(resource))
                    {
                        var packaged = LocalData.Json().Deserialize<GoogleCredential>(reader.ReadToEnd());
                        if (IsClient(packaged)) return packaged;
                    }
                }
            }
            return null;
        }

        internal static GoogleCredential DesktopClient(string clientId, string clientSecret)
        {
            var credential = new GoogleCredential { ClientId = (clientId ?? "").Trim(), ClientSecret = (clientSecret ?? "").Trim() };
            if (!IsClient(credential)) throw new InvalidOperationException("Enter the client ID and client secret for a Google Cloud Desktop app OAuth client.");
            return credential;
        }

        internal static bool IsClient(GoogleCredential credential)
        {
            const string suffix = ".apps.googleusercontent.com";
            return credential != null && !String.IsNullOrEmpty(credential.ClientId) && credential.ClientId.Length > suffix.Length &&
                credential.ClientId.EndsWith(suffix, StringComparison.Ordinal) && !credential.ClientId.Any(Char.IsWhiteSpace) &&
                !String.IsNullOrWhiteSpace(credential.ClientSecret) && !credential.ClientSecret.Any(Char.IsWhiteSpace);
        }

        internal static async Task<GoogleCredential> Connect(GoogleCredential appClient)
        {
            if (!IsClient(appClient)) throw new InvalidOperationException("Configure Google sign-in in Settings first.");
            var credential = new GoogleCredential { ClientId = appClient.ClientId, ClientSecret = appClient.ClientSecret };

            var verifier = RandomString();
            var state = RandomString();
            string challenge;
            using (var hash = SHA256.Create()) challenge = Base64Url(hash.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var redirect = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/";
                var authorization = "https://accounts.google.com/o/oauth2/v2/auth?" + Api.Query(
                    "client_id", credential.ClientId, "redirect_uri", redirect, "response_type", "code", "scope", Scopes,
                    "code_challenge", challenge, "code_challenge_method", "S256", "access_type", "offline", "prompt", "consent", "state", state);
                Process.Start(new ProcessStartInfo(authorization) { UseShellExecute = true });
                var deadline = Task.Delay(TimeSpan.FromMinutes(3));
                string code = null;
                while (code == null)
                {
                    var connection = listener.AcceptTcpClientAsync();
                    if (await Task.WhenAny(connection, deadline).ConfigureAwait(false) == deadline)
                        throw new InvalidOperationException("Google sign-in timed out. Connect again when ready.");
                    using (var client = await connection.ConfigureAwait(false))
                    {
                        // Browsers may open spare or idle sockets to the redirect address. Ignore any
                        // connection that does not deliver a readable request and keep waiting.
                        var received = await ReceiveRedirect(client, redirect, state).ConfigureAwait(false);
                        if (received == null) continue;
                        if (received.Item1 && received.Item2 == null)
                            throw new InvalidOperationException("Google sign-in was declined or returned no authorization code.");
                        code = received.Item2;
                    }
                }
                var google = new GoogleClient(credential);
                var result = await google.Token(new Dictionary<string, string> { { "grant_type", "authorization_code" },
                    { "redirect_uri", redirect }, { "code", code }, { "code_verifier", verifier } }).ConfigureAwait(false);
                var granted = Api.Text(result, "scope");
                if (!String.IsNullOrWhiteSpace(granted) && Scopes.Split(' ').Except(granted.Split(' ')).Any())
                    throw new InvalidOperationException("Allow both read-only Calendar permissions in Google, then connect again.");
                credential.RefreshToken = Api.Text(result, "refresh_token");
                if (String.IsNullOrEmpty(credential.RefreshToken)) throw new InvalidOperationException("Google did not return a refresh token. Connect again and allow access.");
                return credential;
            }
            finally { listener.Stop(); }
        }

        // Returns null for a connection to ignore; otherwise (state matched, authorization code or null).
        internal static async Task<Tuple<bool, string>> ReceiveRedirect(TcpClient client, string redirect, string state)
        {
            client.ReceiveTimeout = 4000;
            client.SendTimeout = 4000;
            NetworkStream stream;
            string target;
            try
            {
                stream = client.GetStream();
                target = await Task.Run(() => ReadRequestTarget(stream)).ConfigureAwait(false);
            }
            catch (IOException) { return null; }
            catch (ObjectDisposedException) { return null; }
            catch (InvalidOperationException) { return null; }
            if (target == null) return null;

            System.Collections.Specialized.NameValueCollection query;
            try { query = HttpUtility.ParseQueryString(new Uri(redirect + target.TrimStart('/')).Query); }
            catch (UriFormatException) { query = new System.Collections.Specialized.NameValueCollection(); }
            var valid = String.Equals(query["state"], state, StringComparison.Ordinal);
            var code = valid ? query["code"] : null;
            var text = code != null ? "Google authorization received. Return to Thong Khong Toolbox."
                : "Authorization was not completed. Return to Thong Khong Toolbox.";
            var response = Encoding.UTF8.GetBytes(text);
            var header = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/plain; charset=utf-8\r\nCache-Control: no-store\r\nConnection: close\r\nContent-Length: " + response.Length + "\r\n\r\n");
            try
            {
                await stream.WriteAsync(header, 0, header.Length).ConfigureAwait(false);
                await stream.WriteAsync(response, 0, response.Length).ConfigureAwait(false);
            }
            catch (IOException) { /* The browser closed the tab; the authorization code is still valid. */ }
            catch (ObjectDisposedException) { }
            return Tuple.Create(valid, code);
        }

        private static string ReadRequestTarget(Stream stream)
        {
            var header = new StringBuilder();
            while (header.Length < 16384)
            {
                var next = stream.ReadByte();
                if (next < 0) return null;
                header.Append((char)next);
                var length = header.Length;
                if (length >= 4 && header[length - 4] == '\r' && header[length - 3] == '\n' && header[length - 2] == '\r' && header[length - 1] == '\n')
                {
                    var parts = header.ToString().Split(new[] { "\r\n" }, 2, StringSplitOptions.None)[0].Split(' ');
                    return parts.Length == 3 && parts[0] == "GET" && parts[1].StartsWith("/", StringComparison.Ordinal) ? parts[1] : null;
                }
            }
            return null;
        }

        private async Task<object> Token(Dictionary<string, string> form)
        {
            form.Add("client_id", credential.ClientId);
            form.Add("client_secret", credential.ClientSecret);
            using (var response = await Api.Send("https://oauth2.googleapis.com/token", HttpMethod.Post, null, new FormUrlEncodedContent(form)).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    var error = LocalData.Json().DeserializeObject(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                    if (Api.Text(error, "error") == "invalid_client")
                        throw new InvalidOperationException("Google rejected the Toolbox OAuth client. Check the Desktop app client ID and secret in Settings.");
                    throw new InvalidOperationException("Google rejected the authorization. Reconnect Google Calendar in Settings.");
                }
                return await Api.Json(response, "Google authorization").ConfigureAwait(false);
            }
        }

        private async Task<object> Get(string path)
        {
            if (String.IsNullOrEmpty(accessToken))
            {
                var result = await Token(new Dictionary<string, string> { { "grant_type", "refresh_token" }, { "refresh_token", credential.RefreshToken } }).ConfigureAwait(false);
                accessToken = Api.Text(result, "access_token");
                if (String.IsNullOrEmpty(accessToken)) throw new InvalidOperationException("Google did not return an access token.");
            }
            using (var response = await Api.Send(CalendarRoot + path, HttpMethod.Get, "Bearer " + accessToken).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new InvalidOperationException("Google Calendar access expired. Reconnect Google in Settings.");
                if (response.StatusCode == HttpStatusCode.Forbidden)
                    throw new InvalidOperationException("Google Calendar denied access. Enable the Calendar API for the OAuth project and allow both read-only Calendar permissions when connecting.");
                return await Api.Json(response, "Google Calendar").ConfigureAwait(false);
            }
        }

        internal async Task<List<GoogleCalendar>> Calendars()
        {
            var result = new List<GoogleCalendar>();
            var page = "";
            do
            {
                var document = await Get("users/me/calendarList?" + Api.Query("maxResults", "250", "pageToken", page)).ConfigureAwait(false);
                var data = Api.Pick(document, "items");
                if (Api.Text(document, "kind") != "calendar#calendarList" || data != null && !(data is object[]))
                    throw new InvalidOperationException("Google Calendar returned an unrecognized calendar list.");
                var items = data as object[] ?? new object[0];
                foreach (var calendar in items)
                {
                    if (Api.Text(calendar, "accessRole") == "freeBusyReader") continue;
                    var id = Api.Text(calendar, "id");
                    var name = Api.Text(calendar, "summaryOverride");
                    if (String.IsNullOrEmpty(name)) name = Api.Text(calendar, "summary");
                    if (String.IsNullOrEmpty(name)) name = id;
                    if (!String.IsNullOrEmpty(id)) result.Add(new GoogleCalendar { Id = id, Name = name, Primary = Api.Flag(calendar, "primary") });
                }
                page = Api.Text(document, "nextPageToken");
            } while (!String.IsNullOrEmpty(page));
            return result;
        }

        internal async Task<List<Slot>> Events(string[] calendars, DateTimeOffset from, DateTimeOffset to)
        {
            var events = new List<Slot>();
            foreach (var calendar in calendars.Distinct())
            {
                var page = "";
                do
                {
                    var path = "calendars/" + Uri.EscapeDataString(calendar) + "/events?" + Api.Query("timeMin", from.ToString("o"),
                        "timeMax", to.ToString("o"), "singleEvents", "true", "maxResults", "250", "timeZone", "America/New_York",
                        "fields", "kind,nextPageToken,items(id,summary,status,transparency,start,end,attendees(self,responseStatus))", "pageToken", page);
                    var data = await Get(path).ConfigureAwait(false);
                    var items = Api.Pick(data, "items");
                    if (Api.Text(data, "kind") != "calendar#events" || items != null && !(items is object[]))
                        throw new InvalidOperationException("Google Calendar returned an unrecognized event list.");
                    foreach (var item in items as object[] ?? new object[0])
                    {
                        var slot = ParseEvent(item);
                        if (slot != null) events.Add(slot);
                    }
                    page = Api.Text(data, "nextPageToken");
                } while (!String.IsNullOrEmpty(page));
            }
            return events;
        }

        internal static Slot ParseEvent(object item)
        {
            if (Api.Text(item, "status") == "cancelled" || Api.Text(item, "transparency") == "transparent") return null;
            var attendees = Api.Pick(item, "attendees") as object[] ?? new object[0];
            if (attendees.Any(attendee => Api.Flag(attendee, "self") && Api.Text(attendee, "responseStatus") == "declined")) return null;
            var start = Api.Pick(item, "start");
            var end = Api.Pick(item, "end");
            var allDay = !String.IsNullOrEmpty(Api.Text(start, "date"));
            DateTimeOffset from, until;
            if (allDay)
            {
                DateTime date, last;
                if (!DateTime.TryParseExact(Api.Text(start, "date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ||
                    !DateTime.TryParseExact(Api.Text(end, "date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out last))
                    throw new InvalidOperationException("Google Calendar returned an unreadable all-day event.");
                from = Planner.At(date, TimeSpan.Zero);
                until = Planner.At(last, TimeSpan.Zero);
            }
            else if (!DateTimeOffset.TryParse(Api.Text(start, "dateTime"), CultureInfo.InvariantCulture, DateTimeStyles.None, out from) ||
                     !DateTimeOffset.TryParse(Api.Text(end, "dateTime"), CultureInfo.InvariantCulture, DateTimeStyles.None, out until))
                throw new InvalidOperationException("Google Calendar returned an unreadable event time.");
            if (until <= from) return null;
            var title = Api.Text(item, "summary");
            return new Slot { Id = Api.Text(item, "id"), Name = String.IsNullOrWhiteSpace(title) ? "Busy calendar event" : title,
                Kind = allDay ? "All day" : "Calendar", Start = from, End = until };
        }
    }

}

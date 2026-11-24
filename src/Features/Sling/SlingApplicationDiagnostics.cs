using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Toolbox
{
    internal static class SlingApplicationDiagnostics
    {
        private const int MaximumBodyBytes = 4096;

        internal static async Task<string> Read(HttpResponseMessage response, string session, CancellationToken deadline)
        {
            var lines = new List<string> { "HTTP " + (int)response.StatusCode,
                "Time (UTC): " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) };
            foreach (var name in new[] { "X-Request-ID", "Request-ID", "X-Correlation-ID" })
            {
                IEnumerable<string> values;
                if (response.Headers.TryGetValues(name, out values)) Add(lines, name, values.FirstOrDefault(), session);
            }

            if (response.Content == null || response.Content.Headers.ContentLength == 0)
            {
                lines.Add("Response body was empty.");
                return String.Join(Environment.NewLine, lines);
            }

            var mediaType = response.Content.Headers.ContentType == null ? null : response.Content.Headers.ContentType.MediaType;
            if (mediaType != null && mediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase))
            {
                lines.Add("Response format: HTML.");
                return String.Join(Environment.NewLine, lines);
            }

            try
            {
                using (var budget = CancellationTokenSource.CreateLinkedTokenSource(deadline))
                {
                    budget.CancelAfter(TimeSpan.FromSeconds(2));
                    var body = await ReadBody(response.Content, budget.Token).ConfigureAwait(false);
                    if (String.IsNullOrWhiteSpace(body)) lines.Add("Response body was empty.");
                    else if (body.TrimStart().StartsWith("<", StringComparison.Ordinal)) lines.Add("Response format: markup.");
                    else if (body.TrimStart().StartsWith("{", StringComparison.Ordinal))
                    {
                        var data = LocalData.Json().DeserializeObject(body);
                        var before = lines.Count;
                        foreach (var field in new[] { "status", "code", "message", "developerMessage", "requestId", "error" })
                            Add(lines, field, Api.Text(data, field), session);
                        if (lines.Count == before) lines.Add("JSON response had no recognized error fields.");
                    }
                    else Add(lines, "Response", body, session);
                }
            }
            catch (Exception error)
            {
                if (!(error is IOException || error is HttpRequestException || error is OperationCanceledException ||
                    error is ArgumentException || error is InvalidOperationException)) throw;
                lines.Add("Response body could not be read as a short error message.");
            }
            return String.Join(Environment.NewLine, lines);
        }

        private static async Task<string> ReadBody(HttpContent content, CancellationToken token)
        {
            // Closing the stream also interrupts transports whose reads ignore cancellation.
            using (token.Register(content.Dispose))
            using (var stream = await content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                var bytes = new byte[MaximumBodyBytes];
                var length = 0;
                while (length < bytes.Length)
                {
                    var read = await stream.ReadAsync(bytes, length, bytes.Length - length, token).ConfigureAwait(false);
                    if (read == 0) break;
                    length += read;
                }
                return Encoding.UTF8.GetString(bytes, 0, length);
            }
        }

        private static void Add(List<string> lines, string name, string value, string session)
        {
            if (String.IsNullOrWhiteSpace(value)) return;
            if (!String.IsNullOrEmpty(session)) value = value.Replace(session, "[redacted]");
            value = Regex.Replace(value, @"(?i)\b(?:authorization|cookie|set-cookie)\s*[:=][^\r\n]*", "[redacted header]");
            value = Regex.Replace(value, @"(?i)\bBearer\s+\S+", "Bearer [redacted]");
            value = Regex.Replace(value, @"\s+", " ").Trim();
            if (value.Length > 500) value = value.Substring(0, 500) + "…";
            lines.Add(name + ": " + value);
        }
    }
}

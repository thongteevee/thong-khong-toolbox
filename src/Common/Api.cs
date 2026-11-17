using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Toolbox
{
    internal static class Api
    {
        private static readonly HttpClient Http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, MaxConnectionsPerServer = 4 })
        { Timeout = TimeSpan.FromSeconds(25) };

        internal static async Task<HttpResponseMessage> Send(string url, HttpMethod method, string token, HttpContent body = null,
            bool? expectContinue = null)
        {
            using (var request = new HttpRequestMessage(method, url))
            {
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                if (!String.IsNullOrEmpty(token)) request.Headers.TryAddWithoutValidation("Authorization", token);
                request.Content = body;
                if (expectContinue.HasValue) request.Headers.ExpectContinue = expectContinue;
                return await Http.SendAsync(request).ConfigureAwait(false);
            }
        }

        internal static Exception Error(string operation, HttpResponseMessage response)
        {
            var code = (int)response.StatusCode;
            var hint = code == 401 || code == 403 ? " Check your account connection or permissions." :
                code == 429 ? " Wait before running again." : "";
            return new InvalidOperationException(operation + " returned HTTP " + code + "." + hint);
        }

        internal static async Task<object> Json(HttpResponseMessage response, string label)
        {
            if (!response.IsSuccessStatusCode) throw Error(label, response);
            var raw = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            try { return LocalData.Json().DeserializeObject(raw); }
            catch (ArgumentException) { throw new InvalidOperationException(label + " did not return readable JSON."); }
            catch (InvalidOperationException) { throw new InvalidOperationException(label + " did not return readable JSON."); }
        }

        internal static object Pick(object value, string key)
        {
            var dictionary = value as IDictionary<string, object>;
            object result;
            return dictionary != null && dictionary.TryGetValue(key, out result) ? result : null;
        }

        internal static string Text(object value, string key)
        {
            var item = Pick(value, key);
            return item is string || item is int || item is long ? Convert.ToString(item, CultureInfo.InvariantCulture) : "";
        }

        internal static bool Flag(object value, string key) { return Object.Equals(Pick(value, key), true); }

        internal static object[] List(object value, string label, params string[] keys)
        {
            var array = value as object[];
            if (array != null) return array;
            foreach (var key in keys)
            {
                var child = Pick(value, key);
                array = child as object[];
                if (array != null) return array;
                if (key == "data" && child is IDictionary<string, object>)
                    return List(child, label, keys.Where(candidate => candidate != "data").ToArray());
            }
            throw new InvalidOperationException(label + " returned an unrecognized list. Run stopped before using this data.");
        }

        internal static string Query(params string[] pairs)
        {
            var result = new List<string>();
            for (var i = 0; i < pairs.Length; i += 2)
                result.Add(Uri.EscapeDataString(pairs[i]) + "=" + Uri.EscapeDataString(pairs[i + 1]));
            return String.Join("&", result);
        }

        internal static string PositiveId(string value)
        {
            long id;
            return Int64.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0 ? value : null;
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Toolbox.Tests
{
    // Startup recovery, Google sign-in redirect handling, and updater cleanup.
    internal static class RobustnessChecks
    {
        internal static void Run()
        {
            var root = Path.Combine(Path.GetTempPath(), "ToolboxRobustness-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                UnreadableAccounts(root);
                PreferenceRecovery(root);
                IncompleteAccounts(root);
                RedirectHandling();
                DownloadCleanup(root);
                PortableBackups(root);
            }
            finally { Directory.Delete(root, true); }
            Console.WriteLine("Passed damaged/null preference recovery, incomplete accounts, sign-in redirects, scoped update cleanup, and portable backup checks.");
        }

        private static void UnreadableAccounts(string root)
        {
            var folder = Path.Combine(root, "accounts");
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "accounts.dpapi");
            File.WriteAllBytes(file, Encoding.ASCII.GetBytes("not protected data"));
            var accounts = new HubAccounts(folder);
            Require(accounts.Notice != null && accounts.Google == null && accounts.Sling == null, "Unreadable connections stopped startup or kept stale data.");
            Require(!File.Exists(file) && Directory.EnumerateFiles(folder, "accounts.dpapi.unreadable-*").Count() == 1,
                "Unreadable connection file was not set aside.");
            accounts.SetSling(new SlingConnection { Token = "t", Org = "1", User = "2", Display = "x" });
            Require(new HubAccounts(folder).Sling != null, "Connections could not be saved again after recovery.");
        }

        private static void RedirectHandling()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                var redirect = "http://127.0.0.1:" + port + "/";
                using (var idle = new TcpClient())
                {
                    idle.Connect(IPAddress.Loopback, port);
                    using (var accepted = listener.AcceptTcpClient())
                        Require(GoogleClient.ReceiveRedirect(accepted, redirect, "s").Result == null,
                            "An idle browser connection ended Google sign-in.");
                }
                Require(Send(listener, port, redirect, "GET /favicon.ico HTTP/1.1\r\nHost: x\r\n\r\n").Item1 == false,
                    "A stray request was treated as the redirect.");
                var answer = Send(listener, port, redirect, "GET /?state=s&code=abc HTTP/1.1\r\nHost: x\r\n\r\n");
                Require(answer.Item1 && answer.Item2 == "abc", "The real redirect was not accepted.");
                var declined = Send(listener, port, redirect, "GET /?state=s&error=access_denied HTTP/1.1\r\nHost: x\r\n\r\n");
                Require(declined.Item1 && declined.Item2 == null, "A declined sign-in was not reported.");
            }
            finally { listener.Stop(); }
        }

        private static void PreferenceRecovery(string root)
        {
            var cases = new[] { "null", "[]", "{\"Rules\":null}", "{\"Calendars\":null}", "{\"Rules\":[null]}", "{\"Calendars\":[null]}" };
            foreach (var json in cases)
            {
                var file = Path.Combine(root, "preferences.json");
                File.WriteAllText(file, json);
                var recovered = false;
                var settings = LocalData.LoadPreferences(file, new Settings(), Planner.Validate, () => recovered = true);
                Require(recovered && settings.Calendars.Length == 0 && settings.Rules != null && !File.Exists(file), "Incomplete settings caused startup failure or silently retained calendars.");
            }
            var appearance = Path.Combine(root, "appearance.json");
            File.WriteAllText(appearance, "null");
            var notice = false;
            var theme = LocalData.LoadPreferences(appearance, new AppearanceSettings(), null, () => notice = true);
            Require(notice && theme.MikuPink, "Null appearance preferences were not recovered.");
            File.WriteAllText(appearance, "{\"MikuPink\":false,\"Artwork\":false}");
            theme = LocalData.LoadPreferences(appearance, new AppearanceSettings(), null, () => { throw new Exception("Valid preferences recovered"); });
            Require(!theme.MikuPink && !theme.Artwork, "Valid appearance preferences changed.");
            Require(Directory.GetFiles(root, "preferences.json.unreadable-*").Length == cases.Length, "Damaged preference backups collided or were lost.");
        }

        private static void IncompleteAccounts(string root)
        {
            var folder = Path.Combine(root, "partial-accounts");
            var file = Path.Combine(folder, "accounts.dpapi");
            var validSling = new SlingConnection { Token = "fixture", Org = "1", User = "2", Display = "Fixture" };
            LocalData.SaveSecret(file, new HubAccounts.Connections { GoogleRefreshToken = "fixture", Sling = validSling });
            var original = File.ReadAllBytes(file);
            var accounts = new HubAccounts(folder);
            Require(accounts.Google == null && accounts.Sling != null && accounts.Notice != null, "Incomplete Google credentials crashed startup or removed a working Sling connection.");
            Require(File.ReadAllBytes(file).SequenceEqual(original), "Validation overwrote the credential vault.");
            LocalData.SaveSecret(file, new HubAccounts.Connections { Sling = new SlingConnection { Token = "fixture", Org = "bad", User = "2" } });
            accounts = new HubAccounts(folder);
            Require(accounts.Sling == null && accounts.Notice != null, "Incomplete Sling credentials appeared connected.");
        }

        private static Tuple<bool, string> Send(TcpListener listener, int port, string redirect, string request)
        {
            using (var browser = new TcpClient())
            {
                browser.Connect(IPAddress.Loopback, port);
                var bytes = Encoding.ASCII.GetBytes(request);
                browser.GetStream().Write(bytes, 0, bytes.Length);
                using (var accepted = listener.AcceptTcpClient())
                {
                    var result = GoogleClient.ReceiveRedirect(accepted, redirect, "s").Result;
                    Require(result != null, "A complete request was ignored.");
                    return result;
                }
            }
        }

        private static void DownloadCleanup(string root)
        {
            var downloads = Path.Combine(root, "downloads");
            var old = Folder(downloads, Guid.NewGuid().ToString("N"), true);
            var current = Folder(downloads, Guid.NewGuid().ToString("N"), false);
            var other = Folder(downloads, "keep-me", true);
            var mixed = Folder(downloads, Guid.NewGuid().ToString("N"), true);
            File.WriteAllText(Path.Combine(mixed, "keep.txt"), "keep");
            var nested = Folder(downloads, Guid.NewGuid().ToString("N"), true);
            Directory.CreateDirectory(Path.Combine(nested, "keep"));
            ToolboxUpdater.RemoveOldDownloads(downloads, DateTime.UtcNow);
            Require(!Directory.Exists(old), "An old update download was not removed.");
            Require(Directory.Exists(current) && Directory.Exists(other), "Cleanup removed a recent or unrelated folder.");
            Require(File.Exists(Path.Combine(mixed, "ThongKhongToolbox.exe")) && File.Exists(Path.Combine(mixed, "keep.txt")) &&
                File.Exists(Path.Combine(nested, "ThongKhongToolbox.exe")), "Update cleanup changed a folder containing unrelated files or subfolders.");
        }

        private static string Folder(string parent, string name, bool old)
        {
            var path = Path.Combine(parent, name);
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "ThongKhongToolbox.exe"), "x");
            if (old) Directory.SetCreationTimeUtc(path, DateTime.UtcNow.AddDays(-2));
            return path;
        }

        private static void PortableBackups(string root)
        {
            var folder = Path.Combine(root, "portable");
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, "ThongKhongToolbox.exe");
            var legacy = Path.Combine(folder, "ThongKhongToolbox.exe." + Guid.NewGuid().ToString("N") + ".update.backup");
            var unrelated = Path.Combine(folder, "notes.update.backup");
            File.WriteAllText(target, "v1");
            File.WriteAllText(legacy, "v0");
            File.WriteAllText(unrelated, "keep");
            var replace = typeof(ToolboxUpdater).GetMethod("ReplacePortable", BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var version in new[] { "v2", "v3" })
            {
                var source = Path.Combine(root, version + ".exe");
                File.WriteAllText(source, version);
                replace.Invoke(null, new object[] { source, target });
            }
            Require(File.ReadAllText(target) == "v3" && File.ReadAllText(target + ".previous") == "v2", "Portable update did not keep one previous copy.");
            Require(!File.Exists(legacy) && File.Exists(unrelated) && Directory.GetFiles(folder).Length == 3,
                "Portable update left extra backups or removed an unrelated file.");
        }

        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}

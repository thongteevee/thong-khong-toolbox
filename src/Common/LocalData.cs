using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Toolbox
{
    public static class LocalData
    {
        internal static readonly string ToolboxFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThongKhongToolbox");
        private static readonly string Folder = Path.Combine(ToolboxFolder, "Sling");
        public static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = 24 * 1024 * 1024 }; }

        private static void Write(string path, byte[] content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, content);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { File.Delete(temporary); }
        }

        public static void Save<T>(string name, T value)
        {
            Write(Path.Combine(Folder, name + ".json"), Encoding.UTF8.GetBytes(Json().Serialize(value)));
        }

        public static T Load<T>(string name, T fallback)
        {
            var path = Path.Combine(Folder, name + ".json");
            return File.Exists(path) ? Json().Deserialize<T>(File.ReadAllText(path)) : fallback;
        }

        // True for files that exist but cannot be decrypted or parsed (damaged, edited,
        // or protected for another Windows user or PC).
        internal static bool Unreadable(Exception exception)
        {
            return exception is CryptographicException || exception is ArgumentException ||
                exception is InvalidOperationException || exception is FormatException;
        }

        // Keep an unreadable file for inspection instead of deleting it, so startup can continue.
        internal static void SetAside(string path)
        {
            var aside = path + ".unreadable-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            try { File.Move(path, aside); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        internal static string SettingsPath(string name) { return Path.Combine(Folder, name + ".json"); }

        internal static void SaveSecret<T>(string path, T value)
        {
            var bytes = Encoding.UTF8.GetBytes(Json().Serialize(value));
            Write(path, ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
        }

        internal static T LoadSecret<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            var bytes = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
            return Json().Deserialize<T>(Encoding.UTF8.GetString(bytes));
        }
    }
}

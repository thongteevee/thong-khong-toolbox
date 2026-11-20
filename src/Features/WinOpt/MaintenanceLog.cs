using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace Toolbox.WinOpt
{
    internal sealed class MaintenanceLog
    {
        internal string Folder { get; private set; }
        internal MaintenanceLog(string folder) { Folder = folder; }
        internal string Write(string operation, int completed, int skipped, int failed, long bytes, bool cancelled, Dictionary<string, int> reasons)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var path = Path.Combine(Folder, "maintenance.jsonl");
                if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024)
                {
                    var previous = Path.Combine(Folder, "maintenance.previous.jsonl");
                    if (File.Exists(previous)) File.Delete(previous);
                    File.Move(path, previous);
                }
                var entry = new { timestampUtc = DateTime.UtcNow.ToString("o"), action = operation, completed = completed,
                    skipped = skipped, failed = failed, logicalBytes = bytes, cancelled = cancelled, reasons = reasons };
                File.AppendAllText(path, new JavaScriptSerializer().Serialize(entry) + Environment.NewLine, new UTF8Encoding(false));
                return null;
            }
            catch (Exception e) { if (!PathPolicy.Recoverable(e)) throw; return "Activity log unavailable (" + PathPolicy.Failure(e) + ")."; }
        }
        internal string Recent()
        {
            try
            {
                var path = Path.Combine(Folder, "maintenance.jsonl");
                if (!File.Exists(path)) return "No recorded maintenance yet.";
                if (new FileInfo(path).Length > 2 * 1024 * 1024) return "Recent activity log is too large to display.";
                var serializer = new JavaScriptSerializer();
                return string.Join(Environment.NewLine, File.ReadLines(path).Reverse().Take(5).Select(line =>
                {
                    // One damaged record (e.g. a line truncated by a crash) must not hide the others.
                    const string unreadable = "An activity record could not be read.";
                    try
                    {
                        var entry = serializer.Deserialize<Dictionary<string, object>>(line);
                        if (entry == null || new[] { "timestampUtc", "action", "completed", "logicalBytes" }.Any(key => !entry.ContainsKey(key)))
                            return unreadable;
                        return entry["timestampUtc"] + "  " + entry["action"] + "  • " + entry["completed"] + " files • " + StorageFormat.Bytes(Convert.ToInt64(entry["logicalBytes"]));
                    }
                    catch (Exception e)
                    {
                        if (!(e is ArgumentException) && !(e is InvalidOperationException) && !(e is FormatException) && !(e is OverflowException) && !(e is InvalidCastException)) throw;
                        return unreadable;
                    }
                }));
            }
            catch (Exception e) { if (!PathPolicy.Recoverable(e) && !(e is InvalidOperationException) && !(e is KeyNotFoundException) && !(e is FormatException) && !(e is OverflowException)) throw; return "Recent activity unavailable."; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Toolbox.WinOpt
{
    internal enum CleanupMode { Safe, Standard, Aggressive }

    internal sealed class TempRoot
    {
        internal string Path { get; private set; }
        internal string Category { get; private set; }
        internal TempRoot(string path, string category) { Path = PathPolicy.Canonical(path); Category = category; }
    }

    internal sealed class ScanPolicy
    {
        internal CleanupMode Mode { get; private set; }
        internal DateTime CreatedUtc { get; private set; }
        internal ReadOnlyCollection<TempRoot> Roots { get; private set; }
        internal ReadOnlyCollection<string> Exclusions { get; private set; }

        internal ScanPolicy(CleanupMode mode, IEnumerable<TempRoot> roots, IEnumerable<string> exclusions, DateTime now)
        {
            if (!Enum.IsDefined(typeof(CleanupMode), mode)) throw new ArgumentException("Unknown cleanup mode.");
            Mode = mode; CreatedUtc = now.ToUniversalTime();
            Roots = Array.AsReadOnly(roots.GroupBy(r => r.Path, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToArray());
            Exclusions = Array.AsReadOnly(exclusions.Where(s => !string.IsNullOrWhiteSpace(s)).Select(PathPolicy.Canonical).ToArray());
            foreach (var root in Roots) PathPolicy.ValidateRoot(root.Path);
        }

        internal bool OldEnough(DateTime modifiedUtc)
        {
            if (modifiedUtc > CreatedUtc) return false;
            var age = Mode == CleanupMode.Safe ? TimeSpan.FromDays(7) : Mode == CleanupMode.Standard ? TimeSpan.FromDays(1) : TimeSpan.Zero;
            return Mode == CleanupMode.Aggressive ? modifiedUtc <= CreatedUtc : modifiedUtc < CreatedUtc - age;
        }
    }

    internal sealed class Candidate
    {
        public string Path { get; private set; }
        public string Category { get; private set; }
        public long Bytes { get { return Stamp.Length; } }
        public string Size { get { return StorageFormat.Bytes(Bytes); } }
        public DateTime Modified { get { return Stamp.ModifiedUtc.ToLocalTime(); } }
        internal TempRoot Root { get; private set; }
        internal FileStamp Stamp { get; private set; }
        internal Candidate(string path, TempRoot root, FileStamp stamp) { Path = path; Root = root; Category = root.Category; Stamp = stamp; }
    }

    internal sealed class ScanResult
    {
        internal ScanPolicy Policy { get; private set; }
        internal ReadOnlyCollection<Candidate> Files { get; private set; }
        internal Dictionary<string, int> Reasons { get; private set; }
        internal bool Cancelled { get; private set; }
        internal bool Limited { get; private set; }
        internal bool Consumed { get; set; }
        internal long Bytes { get { return Files.Sum(f => f.Bytes); } }
        internal ScanResult(ScanPolicy policy, List<Candidate> files, Dictionary<string, int> reasons, bool cancelled, bool limited)
        { Policy = policy; Files = files.AsReadOnly(); Reasons = reasons; Cancelled = cancelled; Limited = limited; }
    }

    internal sealed class CleanupResult
    {
        internal bool DryRun;
        internal bool Cancelled;
        internal int Completed;
        internal int Skipped;
        internal int Failed;
        internal long Bytes;
        internal readonly Dictionary<string, int> Reasons = new Dictionary<string, int>();
        internal readonly List<SpaceObservation> Volumes = new List<SpaceObservation>();
    }

    internal sealed class SpaceObservation
    {
        internal string Volume;
        internal long? Before;
        internal long? After;
        internal long? Delta { get { return Before.HasValue && After.HasValue ? After - Before : null; } }
    }

    internal static class StorageFormat
    {
        internal static string Bytes(long bytes)
        {
            double value = Math.Abs((double)bytes);
            var units = new[] { "B", "KiB", "MiB", "GiB", "TiB" }; int unit = 0;
            while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
            return (bytes < 0 ? "−" : "") + value.ToString(unit == 0 ? "0" : "0.##") + " " + units[unit];
        }
        internal static void Count(Dictionary<string, int> reasons, string reason)
        { int count; reasons.TryGetValue(reason, out count); reasons[reason] = count + 1; }
        internal static string Reasons(Dictionary<string, int> reasons)
        { return string.Join(" · ", reasons.OrderBy(p => p.Key).Select(p => p.Key + ": " + p.Value)); }
    }
}

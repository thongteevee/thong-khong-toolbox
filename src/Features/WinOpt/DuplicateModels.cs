using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Toolbox.WinOpt
{
    internal sealed class DuplicateOptions
    {
        internal ReadOnlyCollection<string> Roots { get; private set; }
        internal ReadOnlyCollection<string> Exclusions { get; private set; }
        internal bool Recursive { get; private set; }
        internal int EntryLimit { get; private set; }
        internal DuplicateOptions(IEnumerable<string> roots, IEnumerable<string> exclusions, bool recursive, int entryLimit = 100000)
        {
            var canonical = roots.Select(PathPolicy.Canonical).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (canonical.Length == 0) throw new ArgumentException("Choose a folder first.");
            foreach (var root in canonical)
            {
                if (!Directory.Exists(root)) throw new ArgumentException("A selected folder is unavailable.");
                if (new DriveInfo(Path.GetPathRoot(root)).DriveType != DriveType.Fixed)
                    throw new ArgumentException("Choose a local fixed-drive folder. Network and removable locations are not scanned.");
            }
            // Nested roots are redundant only when recursion is enabled.
            Roots = Array.AsReadOnly(canonical.Where(r => !recursive || !canonical.Any(other =>
                !string.Equals(r, other, StringComparison.OrdinalIgnoreCase) && PathPolicy.Within(r, other, false))).ToArray());
            Exclusions = Array.AsReadOnly(exclusions.Where(p => !string.IsNullOrWhiteSpace(p)).Select(PathPolicy.Canonical).ToArray());
            Recursive = recursive;
            if (entryLimit < 1 || entryLimit > 1000000) throw new ArgumentOutOfRangeException("entryLimit");
            EntryLimit = entryLimit;
        }
        internal bool Excluded(string path) { return Exclusions.Any(e => PathPolicy.Within(path, e, true)); }
    }

    internal sealed class DuplicateFile
    {
        public string Path { get; private set; }
        public string Name { get { return System.IO.Path.GetFileName(Path); } }
        public long Bytes { get { return Stamp.Length; } }
        public string Size { get { return StorageFormat.Bytes(Bytes); } }
        public DateTime Modified { get { return Stamp.ModifiedUtc.ToLocalTime(); } }
        internal FileStamp Stamp { get; private set; }
        internal DuplicateFile(string path, FileStamp stamp) { Path = path; Stamp = stamp; }
    }

    internal sealed class DuplicateGroup
    {
        public string Name { get; private set; }
        public int Count { get { return Files.Count; } }
        public string Description { get { return Count + " matching files · " + StorageFormat.Bytes(Files[0].Bytes) + " each"; } }
        public ReadOnlyCollection<DuplicateFile> Files { get; private set; }
        internal DuplicateGroup(int number, IEnumerable<DuplicateFile> files)
        { Name = "Group " + number; Files = Array.AsReadOnly(files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToArray()); }
    }

    internal sealed class DuplicateProgress
    {
        internal string Phase;
        internal int Entries;
        internal int Hashed;
        internal long BytesRead;
    }

    internal sealed class DuplicateResult
    {
        internal ReadOnlyCollection<DuplicateGroup> Groups { get; private set; }
        internal ReadOnlyDictionary<string, int> Reasons { get; private set; }
        internal bool Cancelled { get; private set; }
        internal bool Limited { get; private set; }
        internal int Entries { get; private set; }
        internal int Hashed { get; private set; }
        internal DuplicateResult(IEnumerable<DuplicateGroup> groups, Dictionary<string, int> reasons, bool cancelled, bool limited, int entries, int hashed)
        {
            Groups = Array.AsReadOnly(groups.ToArray()); Reasons = new ReadOnlyDictionary<string, int>(reasons);
            Cancelled = cancelled; Limited = limited; Entries = entries; Hashed = hashed;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace Toolbox.WinOpt
{
    // This component has no mutation API. All file handles request read access only.
    internal sealed class DuplicateScanner
    {
        private sealed class ChangedFileException : IOException { }
        private const FileAttributes SkippedAttributes = FileAttributes.ReparsePoint | FileAttributes.Offline | FileAttributes.System;
        private readonly Action<string> beforeRead;
        internal DuplicateScanner() { }
        // Test seam for changes between discovery and reading; not exposed by the UI.
        internal DuplicateScanner(Action<string> inspect) { beforeRead = inspect; }

        internal DuplicateResult Scan(DuplicateOptions options, CancellationToken token, IProgress<DuplicateProgress> progress)
        {
            var reasons = new Dictionary<string, int>();
            var candidates = new List<DuplicateFile>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            var confirmed = new List<List<DuplicateFile>>();
            var firstBuffer = new byte[65536]; var secondBuffer = new byte[65536];
            int entries = 0, hashed = 0; long bytesRead = 0; bool limited = false, cancelled = false;
            var reportTimer = Stopwatch.StartNew();
            Action<string, bool> report = (phase, force) =>
            {
                if (progress != null && (force || reportTimer.ElapsedMilliseconds >= 150))
                { progress.Report(new DuplicateProgress { Phase = phase, Entries = entries, Hashed = hashed, BytesRead = bytesRead }); reportTimer.Restart(); }
            };
            try
            {
                foreach (var root in options.Roots)
                {
                    var pending = new Stack<string>(); pending.Push(root);
                    while (pending.Count > 0 && !limited)
                    {
                        token.ThrowIfCancellationRequested();
                        var directory = pending.Pop();
                        if (options.Excluded(directory)) { Count(reasons, "Excluded path"); continue; }
                        try
                        {
                            if (Unavailable(File.GetAttributes(directory))) { Count(reasons, "Link, system file or cloud placeholder"); continue; }
                            using (NativeFiles.PinDirectory(directory))
                            {
                                if (Unavailable(File.GetAttributes(directory))) { Count(reasons, "Link, system file or cloud placeholder"); continue; }
                                foreach (var path in Directory.EnumerateFileSystemEntries(directory))
                                {
                                    token.ThrowIfCancellationRequested();
                                    if (entries >= options.EntryLimit) { limited = true; break; }
                                    entries++; report("Discovering files", false);
                                    try
                                    {
                                        if (options.Excluded(path)) { Count(reasons, "Excluded path"); continue; }
                                        var attributes = File.GetAttributes(path);
                                        if (Unavailable(attributes)) { Count(reasons, "Link, system file or cloud placeholder"); continue; }
                                        if ((attributes & FileAttributes.Directory) != 0)
                                        { if (options.Recursive) pending.Push(path); continue; }
                                        using (var handle = NativeFiles.OpenFile(path, false))
                                        {
                                            NativeFiles.RequireFinalPath(handle, path);
                                            var stamp = NativeFiles.Read(handle);
                                            if (stamp.Links != 1) { Count(reasons, "Hard-linked file"); continue; }
                                            if (Unavailable((FileAttributes)stamp.Attributes)) { Count(reasons, "Link, system file or cloud placeholder"); continue; }
                                            var id = Path.GetPathRoot(path).ToUpperInvariant() + ":" + stamp.Volume + ":" + stamp.IndexHigh + ":" + stamp.IndexLow;
                                            if (identities.Add(id)) candidates.Add(new DuplicateFile(path, stamp));
                                        }
                                    }
                                    catch (Exception e) { if (!PathPolicy.Recoverable(e)) throw; Count(reasons, PathPolicy.Failure(e)); }
                                }
                            }
                        }
                        catch (Exception e) { if (!PathPolicy.Recoverable(e)) throw; Count(reasons, PathPolicy.Failure(e)); }
                    }
                    if (limited) break;
                }

                foreach (var sizeGroup in candidates.GroupBy(f => f.Bytes).Where(g => g.Count() > 1))
                {
                    var hashes = new Dictionary<string, List<DuplicateFile>>(StringComparer.Ordinal);
                    foreach (var file in sizeGroup)
                    {
                        token.ThrowIfCancellationRequested();
                        try
                        {
                            if (beforeRead != null) beforeRead(file.Path);
                            var hash = Hash(file, token, firstBuffer, value => { bytesRead += value; report("Comparing possible matches", false); }); hashed++;
                            List<DuplicateFile> matching;
                            if (!hashes.TryGetValue(hash, out matching)) hashes[hash] = matching = new List<DuplicateFile>();
                            matching.Add(file);
                            report("Comparing possible matches", false);
                        }
                        catch (Exception e) { if (!PathPolicy.Recoverable(e)) throw; Count(reasons, Failure(e)); }
                    }
                    foreach (var possible in hashes.Values.Where(g => g.Count > 1))
                    {
                        var exactGroups = new List<List<DuplicateFile>>();
                        foreach (var file in possible)
                        {
                            token.ThrowIfCancellationRequested();
                            try
                            {
                                var group = exactGroups.FirstOrDefault(g => Equal(g[0], file, token, firstBuffer, secondBuffer));
                                if (group == null) exactGroups.Add(new List<DuplicateFile> { file }); else group.Add(file);
                            }
                            catch (Exception e) { if (!PathPolicy.Recoverable(e)) throw; Count(reasons, Failure(e)); }
                        }
                        foreach (var group in exactGroups.Where(g => g.Count > 1))
                        {
                            // A representative may have changed since its earlier comparison.
                            var stillCurrent = new List<DuplicateFile>();
                            foreach (var file in group)
                            {
                                token.ThrowIfCancellationRequested();
                                try { Inspect(file); stillCurrent.Add(file); }
                                catch (Exception e) { if (!PathPolicy.Recoverable(e)) throw; Count(reasons, Failure(e)); }
                            }
                            if (stillCurrent.Count > 1) confirmed.Add(stillCurrent);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { cancelled = true; }
            var ordered = confirmed.OrderByDescending(g => g[0].Bytes).ThenBy(g => g[0].Path, StringComparer.OrdinalIgnoreCase);
            return new DuplicateResult(ordered.Select((g, i) => new DuplicateGroup(i + 1, g)), reasons, cancelled, limited, entries, hashed);
        }

        private static void Count(Dictionary<string, int> reasons, string reason) { StorageFormat.Count(reasons, reason); }
        private static string Failure(Exception e) { return e is ChangedFileException ? "Changed during scan" : PathPolicy.Failure(e); }
        private static bool Unavailable(FileAttributes attributes)
        { return (attributes & SkippedAttributes) != 0 || ((uint)attributes & (0x40000u | 0x400000u)) != 0; }
        private static void Validate(DuplicateFile expected, Microsoft.Win32.SafeHandles.SafeFileHandle handle)
        {
            NativeFiles.RequireFinalPath(handle, expected.Path);
            var current = NativeFiles.Read(handle);
            if (current.Links != 1 || Unavailable((FileAttributes)current.Attributes) || !current.Same(expected.Stamp))
                throw new ChangedFileException();
        }
        private static void Inspect(DuplicateFile file)
        {
            using (NativeFiles.PinDirectory(Path.GetDirectoryName(file.Path)))
            using (var handle = NativeFiles.OpenFile(file.Path, false)) Validate(file, handle);
        }
        private static string Hash(DuplicateFile file, CancellationToken token, byte[] buffer, Action<int> read)
        {
            using (NativeFiles.PinDirectory(Path.GetDirectoryName(file.Path)))
            using (var handle = NativeFiles.OpenFile(file.Path, false))
            {
                Validate(file, handle);
                using (var stream = new FileStream(handle, FileAccess.Read, 65536, false))
                using (var sha = SHA256.Create())
                {
                    int length;
                    while ((length = stream.Read(buffer, 0, buffer.Length)) > 0)
                    { token.ThrowIfCancellationRequested(); sha.TransformBlock(buffer, 0, length, buffer, 0); read(length); }
                    token.ThrowIfCancellationRequested(); sha.TransformFinalBlock(new byte[0], 0, 0);
                    Validate(file, handle);
                    return Convert.ToBase64String(sha.Hash);
                }
            }
        }
        private static bool Equal(DuplicateFile left, DuplicateFile right, CancellationToken token, byte[] one, byte[] two)
        {
            using (NativeFiles.PinDirectory(Path.GetDirectoryName(left.Path)))
            using (NativeFiles.PinDirectory(Path.GetDirectoryName(right.Path)))
            using (var a = NativeFiles.OpenFile(left.Path, false))
            using (var b = NativeFiles.OpenFile(right.Path, false))
            {
                Validate(left, a); Validate(right, b);
                using (var first = new FileStream(a, FileAccess.Read, 65536, false))
                using (var second = new FileStream(b, FileAccess.Read, 65536, false))
                {
                    int count;
                    while ((count = first.Read(one, 0, one.Length)) > 0)
                    {
                        token.ThrowIfCancellationRequested(); int offset = 0;
                        while (offset < count) { int n = second.Read(two, offset, count - offset); if (n == 0) return false; offset += n; }
                        for (int i = 0; i < count; i++) if (one[i] != two[i]) return false;
                    }
                    if (second.ReadByte() != -1) return false;
                    Validate(left, a); Validate(right, b); return true;
                }
            }
        }
    }
}

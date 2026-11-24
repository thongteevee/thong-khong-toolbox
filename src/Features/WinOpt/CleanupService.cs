using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Toolbox.WinOpt
{
    internal sealed class CleanupService
    {
        internal static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(30);
        private readonly Func<string, long?> freeSpace;
        private readonly Func<DateTime> clock;
        internal CleanupService() : this(ReadFreeSpace, () => DateTime.UtcNow) { }
        internal CleanupService(Func<string, long?> meter, Func<DateTime> now) { freeSpace = meter; clock = now; }

        internal CleanupResult Run(ScanResult preview, IEnumerable<Candidate> selection, bool dryRun, CancellationToken cancellation, IProgress<int> progress)
        {
            throw new NotSupportedException("Cleanup is disabled. WinOpt only supports read-only file discovery.");
        }

        // Historical implementation retained for reference, unreachable from the application.
        private CleanupResult LegacyRun(ScanResult preview, IEnumerable<Candidate> selection, bool dryRun, CancellationToken cancellation, IProgress<int> progress)
        {
            if (preview == null) throw new InvalidOperationException("Cleanup is disabled.");
            // A preview can never be replayed or changed by a cleanup caller.
            lock (preview)
            {
                if (preview.Consumed) throw new InvalidOperationException("This preview has been used. Scan again.");
                var age = clock() - preview.Policy.CreatedUtc;
                if (age < TimeSpan.Zero || age > PreviewLifetime) throw new InvalidOperationException("Preview expired. Scan again.");
                var selected = selection.Distinct().ToArray();
                var approved = new HashSet<Candidate>(preview.Files);
                if (selected.Length == 0 || selected.Any(f => !approved.Contains(f))) throw new InvalidOperationException("Select files from this preview only.");
                if (!dryRun) preview.Consumed = true;
                return Execute(preview, selected, dryRun, cancellation, progress);
            }
        }

        private CleanupResult Execute(ScanResult preview, Candidate[] selected, bool dryRun, CancellationToken cancellation, IProgress<int> progress)
        {
            var result = new CleanupResult { DryRun = dryRun };
            foreach (var volume in selected.Select(f => Path.GetPathRoot(f.Path)).Distinct(StringComparer.OrdinalIgnoreCase))
                result.Volumes.Add(new SpaceObservation { Volume = volume, Before = freeSpace(volume) });
            int visited = 0;
            // Pin each parent directory (with its ancestors) once for all of its selected files.
            foreach (var group in selected.GroupBy(f => Path.GetDirectoryName(f.Path), StringComparer.OrdinalIgnoreCase))
            {
                if (cancellation.IsCancellationRequested) { result.Cancelled = true; break; }
                var files = group.ToArray();
                NativeFiles.Pins pins;
                string groupRejection;
                try
                {
                    pins = NativeFiles.PinDirectory(group.Key);
                    try { groupRejection = InsideProject(group.Key, files[0].Root) ? "Project folder" : null; }
                    catch { pins.Dispose(); throw; }
                }
                catch (Exception e)
                {
                    if (!PathPolicy.Recoverable(e)) throw;
                    foreach (var file in files)
                    {
                        if (cancellation.IsCancellationRequested) { result.Cancelled = true; break; }
                        result.Failed++; StorageFormat.Count(result.Reasons, PathPolicy.Failure(e));
                        Tick(ref visited, progress);
                    }
                    continue;
                }
                using (pins)
                {
                    foreach (var file in files)
                    {
                        if (cancellation.IsCancellationRequested) { result.Cancelled = true; break; }
                        try
                        {
                            var rejection = PathPolicy.Rejection(file.Path, file.Root, preview.Policy, false) ?? groupRejection;
                            if (rejection != null) { Skip(result, rejection); continue; }
                            using (var handle = NativeFiles.OpenFile(file.Path, true))
                            {
                                NativeFiles.RequireFinalPath(handle, file.Path);
                                var current = NativeFiles.Read(handle);
                                rejection = current.Rejection(preview.Policy);
                                if (rejection != null) { Skip(result, rejection); continue; }
                                if (!current.Same(file.Stamp)) { Skip(result, "Changed since preview"); continue; }
                                if (cancellation.IsCancellationRequested) { result.Cancelled = true; break; }
                                if (!dryRun) NativeFiles.Delete(handle);
                            }
                            result.Completed++; result.Bytes += file.Bytes;
                        }
                        catch (Exception e)
                        {
                            if (!PathPolicy.Recoverable(e)) throw;
                            result.Failed++; StorageFormat.Count(result.Reasons, PathPolicy.Failure(e));
                        }
                        finally { Tick(ref visited, progress); }
                    }
                }
                if (result.Cancelled) break;
            }
            foreach (var volume in result.Volumes) volume.After = freeSpace(volume.Volume);
            return result;
        }
        // Refuse directories that became projects since the scan.
        private static bool InsideProject(string directory, TempRoot root)
        {
            for (var parent = directory; PathPolicy.Within(parent, root.Path, true); parent = Path.GetDirectoryName(parent))
            {
                if (PathPolicy.IsProjectFolder(parent)) return true;
                if (string.Equals(PathPolicy.Canonical(parent), root.Path, StringComparison.OrdinalIgnoreCase)) return false;
            }
            return false;
        }
        private static void Tick(ref int visited, IProgress<int> progress) { if (++visited % 25 == 0 && progress != null) progress.Report(visited); }
        private static void Skip(CleanupResult result, string reason) { result.Skipped++; StorageFormat.Count(result.Reasons, reason); }
        private static long? ReadFreeSpace(string volume)
        {
            try { return new DriveInfo(volume).TotalFreeSpace; }
            catch (Exception e) { if (!PathPolicy.Recoverable(e)) throw; return null; }
        }
    }
}

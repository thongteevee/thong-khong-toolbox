using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Toolbox.WinOpt
{
    internal sealed class TempScanner
    {
        internal const int CandidateLimit = 100000;
        internal ScanResult Scan(ScanPolicy policy, CancellationToken cancellation, IProgress<int> progress)
        {
            var files = new List<Candidate>();
            var reasons = new Dictionary<string, int>();
            bool limited = false;
            var visited = 0;
            foreach (var root in policy.Roots)
            {
                var pending = new Stack<string>(); pending.Push(root.Path);
                while (pending.Count > 0 && !cancellation.IsCancellationRequested && !limited)
                {
                    var directory = pending.Pop();
                    try
                    {
                        // Subdirectories were checked before being queued; only the root needs checking here.
                        var rejection = directory == root.Path ? PathPolicy.Rejection(directory, root, policy, true) : null;
                        if (rejection != null) { StorageFormat.Count(reasons, rejection); continue; }
                        using (NativeFiles.PinDirectory(directory))
                        {
                            if (PathPolicy.IsProjectFolder(directory))
                            { StorageFormat.Count(reasons, "Project folder"); continue; }
                            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
                            {
                                if (cancellation.IsCancellationRequested) break;
                                if (++visited % 200 == 0 && progress != null) progress.Report(visited);
                                if (visited >= 1000000 || files.Count >= CandidateLimit) { limited = true; break; }
                                try
                                {
                                    var attrs = File.GetAttributes(path);
                                    if ((attrs & (FileAttributes.ReparsePoint | FileAttributes.System | FileAttributes.Hidden | FileAttributes.Offline)) != 0)
                                    { StorageFormat.Count(reasons, "Protected attributes or link"); continue; }
                                    bool isDirectory = (attrs & FileAttributes.Directory) != 0;
                                    rejection = PathPolicy.Rejection(path, root, policy, isDirectory);
                                    if (rejection != null) { StorageFormat.Count(reasons, rejection); continue; }
                                    if (isDirectory)
                                    {
                                        if (path.Length - root.Path.Length > 2048) { StorageFormat.Count(reasons, "Depth limit"); continue; }
                                        pending.Push(path); continue;
                                    }
                                    using (var handle = NativeFiles.OpenFile(path, false))
                                    {
                                        NativeFiles.RequireFinalPath(handle, path);
                                        var stamp = NativeFiles.Read(handle);
                                        rejection = stamp.Rejection(policy);
                                        if (rejection != null) { StorageFormat.Count(reasons, rejection); continue; }
                                        files.Add(new Candidate(path, root, stamp));
                                    }
                                }
                                catch (Exception e) { if (!PathPolicy.Recoverable(e)) throw; StorageFormat.Count(reasons, PathPolicy.Failure(e)); }
                            }
                        }
                    }
                    catch (Exception e) { if (!PathPolicy.Recoverable(e)) throw; StorageFormat.Count(reasons, PathPolicy.Failure(e)); }
                }
                if (limited || cancellation.IsCancellationRequested) break;
            }
            return new ScanResult(policy, files, reasons, cancellation.IsCancellationRequested, limited);
        }
    }
}

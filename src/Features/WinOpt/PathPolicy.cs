using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Toolbox.WinOpt
{
    internal static class PathPolicy
    {
        internal static string Canonical(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length < 3 || !char.IsLetter(path[0]) || path[1] != ':' || path[2] != '\\')
                throw new ArgumentException("Use a full local path such as C:\\folder. Network and device paths are excluded.");
            if (path.IndexOf(':', 2) >= 0 || path.IndexOfAny(new[] { '*', '?', '"', '<', '>', '|' }) >= 0)
                throw new ArgumentException("Alternate streams and wildcard paths are excluded.");
            var segments = path.Substring(3).Replace('/', '\\').Split('\\');
            if (segments.Any(p => p == "." || p == ".." || (p.Length > 0 && (p.EndsWith(".") || p.EndsWith(" ")))))
                throw new ArgumentException("Ambiguous and traversing paths are excluded.");
            var canonical = System.IO.Path.GetFullPath(path).TrimEnd('\\');
            return canonical.Length == 2 ? canonical + "\\" : canonical;
        }

        internal static bool Within(string path, string root, bool allowEqual)
        {
            path = Canonical(path); root = Canonical(root);
            return (allowEqual && string.Equals(path, root, StringComparison.OrdinalIgnoreCase)) ||
                path.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
        }

        internal static void ValidateRoot(string root)
        {
            root = Canonical(root);
            if (root.Length <= 3) throw new ArgumentException("A volume root is not a temporary folder.");
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var windowsTemp = System.IO.Path.Combine(windows, "Temp");
            if (Within(root, windows, true) && !string.Equals(root, windowsTemp, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Only Windows Temp may be scanned inside Windows.");
            foreach (var special in new[] { Environment.SpecialFolder.UserProfile, Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolder.ApplicationData, Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86,
                Environment.SpecialFolder.CommonApplicationData, Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.DesktopDirectory })
            {
                var path = Environment.GetFolderPath(special);
                if (!string.IsNullOrEmpty(path) && (Within(path, root, true) ||
                    ((special == Environment.SpecialFolder.MyDocuments || special == Environment.SpecialFolder.DesktopDirectory ||
                      special == Environment.SpecialFolder.ProgramFiles || special == Environment.SpecialFolder.ProgramFilesX86 ||
                      special == Environment.SpecialFolder.CommonApplicationData || special == Environment.SpecialFolder.ApplicationData) && Within(root, path, true))))
                    throw new ArgumentException("The temporary folder overlaps a protected location.");
            }
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (var child in new[] { "Downloads", "Documents", "Pictures", "Music", "Videos", "OneDrive" })
                if (Within(root, System.IO.Path.Combine(profile, child), true)) throw new ArgumentException("Personal folders cannot be temporary cleanup roots.");
            foreach (var name in new[] { "OneDrive", "OneDriveConsumer", "OneDriveCommercial" })
            {
                var cloud = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrEmpty(cloud) && Within(root, cloud, true)) throw new ArgumentException("Cloud folders cannot be cleanup roots.");
            }
        }

        internal static TempRoot[] ProductionRoots(bool includeWindows)
        {
            var temporary = Canonical(System.IO.Path.GetTempPath());
            ValidateProductionTemp(temporary);
            var roots = new List<TempRoot> { new TempRoot(temporary, "User temporary files") };
            if (includeWindows) roots.Add(new TempRoot(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"), "Windows temporary files"));
            return roots.ToArray();
        }

        internal static void ValidateProductionTemp(string temporary)
        {
            ValidateRoot(temporary);
            var localTemp = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp");
            var name = System.IO.Path.GetFileName(temporary);
            if (!Within(temporary, localTemp, true) && !string.Equals(name, "Temp", StringComparison.OrdinalIgnoreCase) && !string.Equals(name, "Tmp", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The configured user temporary folder is not a dedicated Temp/Tmp location. Use Windows cleanup or correct TEMP before scanning.");
            for (var parent = temporary; !string.IsNullOrEmpty(parent); parent = System.IO.Path.GetDirectoryName(parent))
                if (IsProjectFolder(parent))
                    throw new ArgumentException("The configured temporary folder is inside a project. WinOpt will not clean it.");
        }

        internal static bool IsProjectFolder(string directory)
        {
            return Directory.Exists(System.IO.Path.Combine(directory, ".git")) || File.Exists(System.IO.Path.Combine(directory, ".git")) ||
                Directory.Exists(System.IO.Path.Combine(directory, ".svn"));
        }

        internal static string Rejection(string path, TempRoot root, ScanPolicy policy, bool directory)
        {
            if (!Within(path, root.Path, directory)) return "Outside approved root";
            if (policy.Exclusions.Any(p => Within(path, p, true))) return "User exclusion";
            var relative = path.Substring(root.Path.Length).TrimStart('\\');
            foreach (var segment in relative.Split('\\'))
            {
                var lower = segment.ToLowerInvariant();
                if (lower == ".git" || lower == ".svn" || lower == "node_modules" || lower == "documents" || lower == "downloads" ||
                    lower.Contains("install") || lower.Contains("setup") || lower.StartsWith("msi") || lower.StartsWith("{cosmos") ||
                    lower == "onedrive" || lower == "dropbox") return "Protected folder or payload";
            }
            if (!directory)
            {
                var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
                if (new[] { ".exe", ".dll", ".msi", ".msp", ".msix", ".msixbundle", ".appx", ".appxbundle", ".cab", ".sys", ".vhd", ".vhdx", ".iso" }.Contains(extension))
                    return "Protected folder or payload";
            }
            return null;
        }

        internal static bool Recoverable(Exception e)
        { return e is IOException || e is UnauthorizedAccessException || e is System.ComponentModel.Win32Exception || e is System.Security.SecurityException || e is ArgumentException || e is NotSupportedException; }
        internal static string Failure(Exception e)
        {
            var win32 = e as System.ComponentModel.Win32Exception;
            if (win32 != null) return "Windows error " + win32.NativeErrorCode;
            if (e is UnauthorizedAccessException || e is System.Security.SecurityException) return "Access denied";
            if (e is FileNotFoundException || e is DirectoryNotFoundException) return "No longer available";
            return e is ArgumentException ? "Unsafe path" : "Unavailable or in use";
        }
    }
}

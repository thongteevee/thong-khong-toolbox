using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Toolbox.WinOpt
{
    internal sealed class FileStamp
    {
        internal uint Volume, IndexHigh, IndexLow, Links, Attributes;
        internal long Length;
        internal DateTime CreatedUtc, ModifiedUtc;
        internal bool Same(FileStamp other)
        {
            return Volume == other.Volume && IndexHigh == other.IndexHigh && IndexLow == other.IndexLow &&
                Links == other.Links && Length == other.Length && Attributes == other.Attributes &&
                CreatedUtc == other.CreatedUtc && ModifiedUtc == other.ModifiedUtc;
        }
        internal string Rejection(ScanPolicy policy)
        {
            const uint forbidden = 0x1 | 0x2 | 0x4 | 0x10 | 0x400 | 0x1000 | 0x40000 | 0x400000;
            if ((Attributes & forbidden) != 0) return "Protected attributes or link";
            if (Links != 1) return "Hard link";
            if (!policy.OldEnough(ModifiedUtc) || !policy.OldEnough(CreatedUtc)) return "Too recent";
            return null;
        }
    }

    internal static class NativeFiles
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Info
        {
            internal uint Attributes;
            internal FILETIME Created, Accessed, Modified;
            internal uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out Info info);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint count, uint flags);

        internal sealed class Pins : IDisposable
        {
            internal readonly List<SafeFileHandle> Handles = new List<SafeFileHandle>();
            public void Dispose() { for (int i = Handles.Count - 1; i >= 0; i--) Handles[i].Dispose(); }
        }

        // Deny delete sharing on EVERY ancestor, so a checked directory cannot be
        // renamed/replaced while traversal, file validation or deletion is in flight.
        internal static Pins PinDirectory(string directory)
        {
            directory = PathPolicy.Canonical(directory);
            var pins = new Pins();
            try
            {
                var current = Path.GetPathRoot(directory);
                PinOne(current, pins);
                foreach (var part in directory.Substring(current.Length).Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries))
                { current = Path.Combine(current, part); PinOne(current, pins); }
                return pins;
            }
            catch { pins.Dispose(); throw; }
        }

        private static void PinOne(string path, Pins pins)
        {
            var handle = Open(path, 0x1 | 0x80, 0x1 | 0x2, 0x02000000 | 0x00200000);
            pins.Handles.Add(handle);
            var stamp = Read(handle);
            if ((stamp.Attributes & 0x400) != 0 || (stamp.Attributes & 0x10) == 0) throw new IOException("Linked or invalid directory.");
            RequireFinalPath(handle, path);
        }

        internal static SafeFileHandle OpenFile(string path, bool deletionAccess)
        {
            if (deletionAccess) throw new NotSupportedException("Write and deletion access are disabled for WinOpt discovery.");
            // OPEN_REPARSE_POINT inspects the link itself; share=0 refuses active users.
            return Open(path, 0x80000000u, 0, 0x00200000);
        }

        private static SafeFileHandle Open(string path, uint access, uint share, uint flags)
        {
            var handle = CreateFile(path, access, share, IntPtr.Zero, 3, flags, IntPtr.Zero);
            if (handle.IsInvalid) { var error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error); }
            return handle;
        }

        internal static FileStamp Read(SafeFileHandle handle)
        {
            Info info;
            if (!GetFileInformationByHandle(handle, out info)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return new FileStamp { Attributes = info.Attributes, Volume = info.Volume, IndexHigh = info.IndexHigh,
                IndexLow = info.IndexLow, Links = info.Links, Length = ((long)info.SizeHigh << 32) | info.SizeLow,
                CreatedUtc = Time(info.Created), ModifiedUtc = Time(info.Modified) };
        }
        private static DateTime Time(FILETIME value)
        { return DateTime.FromFileTimeUtc(((long)value.dwHighDateTime << 32) | (uint)value.dwLowDateTime); }

        internal static void RequireFinalPath(SafeFileHandle handle, string expected)
        {
            // Start small; when the buffer is short the API returns the required size including the terminator.
            var path = new StringBuilder(512);
            var length = GetFinalPathNameByHandle(handle, path, (uint)path.Capacity, 0);
            if (length >= path.Capacity && length <= 32768)
            {
                path = new StringBuilder((int)length);
                length = GetFinalPathNameByHandle(handle, path, (uint)path.Capacity, 0);
            }
            if (length == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (length >= path.Capacity) throw new IOException("Resolved path too long.");
            var resolved = path.ToString();
            if (!resolved.StartsWith(@"\\?\", StringComparison.Ordinal)) throw new IOException("Unrecognized final path.");
            resolved = resolved.Substring(4);
            if (!string.Equals(PathPolicy.Canonical(resolved), PathPolicy.Canonical(expected), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Resolved file is outside the inspected path.");
        }

        internal static void Delete(SafeFileHandle handle)
        {
            throw new NotSupportedException("File deletion is disabled. WinOpt only supports read-only file discovery.");
        }
    }
}

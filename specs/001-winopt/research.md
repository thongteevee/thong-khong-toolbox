# Research and decisions

Reviewed 2026-11-19 against primary sources:

- [Spec Kit v1.4.3](https://github.com/github/spec-kit/tree/v1.4.3): vendored official
  templates and PowerShell prerequisite helper; no global CLI install needed here.
- [Optimize-Volume](https://learn.microsoft.com/en-us/powershell/module/storage/optimize-volume):
  Windows selects operations by storage type, including virtual/thin/tiered storage
  and SSDs without TRIM. Prefer native Optimize Drives over guessed forced modes.
- [GetFinalPathNameByHandle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfinalpathnamebyhandlew):
  inspect the resolved object path rather than lexical path checks alone.
- [SetFileInformationByHandle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle):
  file disposition requires DELETE access; deletion is tied to the inspected handle.
- [File access rights](https://learn.microsoft.com/en-us/windows/win32/fileio/file-security-and-access-rights):
  respect ACL denial; do not alter ownership, attributes or ACLs to make deletion work.

Media query uses the Windows Storage CIM provider (Get-Partition → Get-Disk;
Get-PhysicalDisk matching DeviceId only for ordinary nonvirtual disks). Unknown,
multiple, virtual, unavailable or inconsistent mappings stay Unknown. The query is
read-only and bounded by timeout. Last-optimization status stays in Windows' native UI.

Tradeoff: permanent deletion has no undo. Explicit warning and conservative default
are required; no claim that age proves a file is disposable. Protected installer
filtering is a conservative heuristic combined with locks and explicit review.

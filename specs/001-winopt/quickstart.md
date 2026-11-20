# Quickstart

On Windows 11 with .NET Framework 4.8:

```powershell
.\Build.ps1
.\Test-WinOpt.ps1 -ReadOnlyDriveProbe
.\.specify\scripts\powershell\check-prerequisites.ps1 -Json -RequireSpec -RequireTasks -IncludeTasks
.\dist\ThongKhongToolbox.exe
```

Choose WinOpt from Home or the sidebar. Use Scan only and review the preview.
Dry run selected validates without deletion. Clean selected requires permanent-deletion
confirmation. Test actual cleanup in a disposable VM with disposable temp files.

Read [README](../../README.md#winopt-storage-maintenance) and [test plan](test-plan.md)
before release validation. Development fixture tests are separate from real temp roots.

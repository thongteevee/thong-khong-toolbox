# Test plan

## Automated (isolated fixtures only)

`Test-WinOpt.ps1` compiles the core and a console harness using the Windows .NET
Framework compiler. Fixture trees are generated inside `dist/WinOptTests/fixtures`.
Tests must never pass the machine's personal/system temp folders to cleanup.

The suite also constructs disconnected WPF components to check that rounded tab
borders fit their allocated bounds at 400, 590 and 960 pixels, tabs wrap, cleanup
starts disabled, Grid/List layouts switch, and tile navigation commands bind.
These component checks do not show windows or send input to the desktop.

Cover strict age boundaries for all modes; case-insensitive canonical path
boundaries; traversal/ADS/UNC/device rejection; exclusions; hidden/read-only/system
files; installer and project paths; symlink/junction and hard-link rejection;
locked and replaced/changed files; dry-run nonmutation; selected-only deletion;
cancellation; preview expiry/single-use; unavailable metrics; aggregate log privacy.
All fixture recursive teardown targets must resolve inside the fixture base.

## Read-only integration

Build the complete hub and execute only read-only fixed-drive/media discovery.
Open WinOpt, verify layout, keyboard access, initial disabled Clean action, native
link command targets and operation-state handling. Preview scans, if performed,
must never be followed by cleaning the development machine's real temporary files.

## Manual release verification (requires a disposable Windows 11 VM)

- Scan user Temp and optionally Windows Temp as a standard user; see denied-access
  counts. Cancel a large scan; change mode/exclusions and confirm preview invalidates.
- Create disposable aged temp files, select a subset, cancel confirmation, run dry
  run, then confirm cleanup. Verify only approved files disappear and rescan required.
- Replace, lock, hard-link and junction candidates after preview; verify survival.
- Cancel cleanup midway; verify accurate partial counts and free-space reporting.
- Verify native Settings/Recycle Bin links. In Optimize Drives inspect SSD, HDD,
  virtual/unknown media and last-run status. Exercise UAC cancellation, Windows
  optimization progress and outcome only in the disposable VM.
- Test keyboard-only navigation, 125–200% scale, window resizing and high contrast.

## Known limitations

Optimization progress/last-run status/results live in Windows' own window. No
WinOpt optimization history is claimed. Media queries can be unavailable and show
Unknown. No in-app admin system-temp cleaner, undo, Recycle Bin deletion, CPU/RAM,
scheduled tasks, browser caches, large/duplicate finder or SMART in this MVP.
Logical preview bytes are estimates, not allocated disk bytes. Free-space deltas
are observations affected by concurrent activity. Metadata checks do not prove an
unlocked file is unwanted. Do not market the app as making Windows faster.

## Recorded verification — 2026-11-19

- `Test-WinOpt.ps1 -ReadOnlyDriveProbe`: **34 passed, 0 failed** outside the
  restrictive command sandbox, which denies hard-link fixture creation. The same
  tests compile with warnings as errors. All deletion was inside generated fixtures.
- Read-only drive discovery returned C:, M: and T: capacity/free values. Media
  was Unknown; the Storage CIM query is unavailable to this process. No elevation
  is requested inside the app to resolve this display-only information.
- Full Windows build succeeds using the existing .NET Framework compiler.
- Official Spec Kit prerequisite checker passes with spec, plan and tasks.
- Live UI smoke checks confirmed the integrated WinOpt page, Miku palette and
  Home's adjacent Grid/List control with successful List rendering.
- Final tab-layout and tile-command corrections pass disconnected WPF component
  checks. After Windows was unlocked, the final executable was reopened and live
  screenshots confirmed full rounded borders on all three tabs in both Storage
  overview and Temporary files. Clicking the Home WinOpt tile opened the embedded
  page successfully, and Dry run/Clean remained disabled without a preview/selection.
  Narrow-width wrapping remains covered by the disconnected component checks;
  the live resize attempt did not change the window size.
- No real user/system temp cleanup, Windows optimization, UAC approval, automatic
  startup changes, account changes, or installer publishing was performed.

The disposable-VM release checklist above remains manual; passing fixtures does
not claim that every Windows storage configuration or application temp format has
been validated.

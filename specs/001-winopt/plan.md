# Implementation Plan: WinOpt

**Feature**: `001-winopt` | **Date**: 2026-11-19 | **Spec**: [spec.md](spec.md)

## Review and improvements to the brief

- Integrate into the existing Toolbox as its second tool, with an embedded WPF page
  and shared sidebar navigation, per user feedback. Use rounded turquoise/charcoal
  styling and subtle pink focus accents. Keep Home's Grid/List control beside Apps.
  A separate executable and .NET SDK migration add packaging cost without MVP value.
- Delegate optimization to Windows Optimize Drives (`dfrgui.exe`). This supplies
  media-aware behavior, last-run information, elevation, progress and results without
  maintaining a privileged command bridge. This deliberately replaces in-app
  Optimize-Volume execution; the limitation is explicit in the UI and documentation.
- Add immutable previews, a 30-minute expiry, one-shot cleanup, per-file opt-in,
  changed-file detection and handle-based deletion. Never delete by a path after
  checking a different object. Hold directory handles to resist ancestor renames.
- Add hard-link refusal, preview limits, protected payload filtering, privacy-preserving
  bounded logs and per-volume measurements. Report incomplete scans and cancellation.
- Keep system temp optional and best-effort at current privilege. Users can use
  native cleanup for inaccessible files; do not elevate the entire account-connected hub.
- Keep Recycle Bin in its native review UI; no app-level empty-all action.

## Technical Context

**Language**: C# 5 compatible with the existing compiler.
**Platform**: Windows 11, .NET Framework 4.8, WPF; no external runtime packages.
**Storage**: in-memory previews; aggregate JSONL logs under local application data.
**Tests**: dependency-free console harness plus manual Windows UI verification.
**Constraints**: no cleanup or optimization on development-machine temp volumes;
100,000 candidate cap; cooperative cancellation; no background maintenance.

## Constitution Check

Preview/confirmation, scope confinement, least privilege, honest measurements,
privacy, isolated tests and component separation are required before release.
Native optimization handoff satisfies least privilege and avoids forced media modes.

## Project Structure and code flow

`src/Features/WinOpt/`:

- `Models.cs`: immutable preview and operation results.
- `PathPolicy.cs`: canonical boundaries, eligibility, approved roots and exclusions.
- `NativeFiles.cs`: directory pins, file identity snapshots and deletion through handles.
- `TempScanner.cs`: iterative traversal, limits, cancellation, read-only preview.
- `CleanupService.cs`: selection/expiry checks, revalidation, dry run and measurements.
- `MaintenanceLog.cs`: bounded aggregate local logging.
- `WindowsStorage.cs`: local drive summaries, read-only media query and native links.
- `Views/WinOptView.cs` and `WinOpt.xaml`: embedded UI orchestration and accessible presentation.

Embedded view → immutable policy → scanner → preview → selection → confirmation → cleanup.
The native I/O layer holds non-delete-sharing directory handles and exclusive file
handles, checks final paths/identity, then marks exactly that handle for deletion.
Cleanup never recursively deletes directories. WindowsStorage cannot clean files.

## Verification

Run `Test-WinOpt.ps1`, `Build.ps1`, official Spec Kit prerequisites, then read-only
drive discovery and UI smoke tests. See [test-plan.md](test-plan.md) for release checks.

## Research refinement

See [comparison with established tools](../../docs/winopt-research.md). The MVP adds
native diagnostic links for Task Manager, Startup Apps and Installed Apps. Future
performance work requires measured workload baselines; future app cleaners require
app-specific rules and running-process detection. No generic boost score or imported
community cleanup rules are introduced.

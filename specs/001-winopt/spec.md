# Feature Specification: WinOpt maintenance MVP

> Historical specification. The user's 2026-11-23 data-protection rule and
> [read-only duplicate discovery](../002-duplicate-discovery/spec.md) supersede
> age-based cleanup. Do not run the old cleanup workflow against user data.

**Feature**: `001-winopt` | **Created**: 2026-11-19 | **Status**: Accepted for implementation
**Input**: Review and build from WinOpt_Codex_Project_Brief.md, adapting to the existing Toolbox.

## User Scenarios & Testing

### US1 — Inspect temporary storage (P1)

Open WinOpt from Home or the sidebar, select Safe (7 days), Standard (24 hours),
or Aggressive (all otherwise eligible files), optionally include Windows Temp,
enter exclusions, then Scan only. Review individual files, categories, skipped
reasons, counts and estimated bytes. Candidates start unselected.

Acceptance: scanning changes no files; all modes respect exclusions and protected
files; cancellation remains responsive and produces a clearly partial preview.
Changing policy invalidates the preview. Resolve user temp through Windows APIs.
WinOpt stays inside the Toolbox window. Use a rounded turquoise/charcoal theme
with subtle pink keyboard-focus accents and wrapping, uncropped navigation tabs.

### US2 — Clean explicitly selected files (P1)

Select files, choose Clean selected, review a permanent-deletion warning with
count/bytes and confirm. Revalidate each file against the original snapshot and
policy, skip locked/changed/inaccessible files, record outcomes and show actual
before/after free-space deltas by volume. Offer Dry run selected separately.

Acceptance: no unchecked file is deleted; replacing or modifying a candidate after
scan prevents its deletion; hard links and reparse points are skipped; cancellation
reports completed work. Dry run performs the same checks without marking deletion.

### US3 — Inspect and maintain drives (P2)

Display ready fixed local drives, capacity, used/free, and media type or Unknown.
Open Windows Optimize Drives to view last-run status and explicitly start Windows'
supported optimization, with its own elevation, progress and outcome UI.

Acceptance: never guess SSD/HDD from a model name; never force Defrag. Query errors
do not hide capacity information. No optimization occurs automatically or in tests.
Storage Sense, Cleanup Recommendations and Recycle Bin open native Windows views.

## Requirements

- FR-001: Only approved local temp roots; no arbitrary cleanup folder picker.
- FR-002: Canonical boundary checks, no alternate data streams/device/UNC paths,
  no linked ancestors, and handle-based identity checks through deletion.
- FR-003: Skip hidden/system/read-only/offline, multi-link files, installers,
  executable payloads, installation folders, project markers and user exclusions.
- FR-004: A scan snapshot expires after 30 minutes and is single-use for cleanup.
- FR-005: Log UTC timestamps, operation, counts, bytes, cancellation and error codes;
  never log filenames. Keep bounded local log history. No telemetry or speed claims.
- FR-006: Windows Temp access denial is reported without elevating the hub.
- FR-007: CPU/RAM/process/startup/large-file/duplicate tools remain later features.
  No registry cleaning, service changes, automatic Recycle Bin or Downloads cleanup.

## Key Entities

Scan policy (mode, fixed time, approved roots, exclusions); immutable candidate
(path, root, file identity, size/times); scan snapshot; outcome counters/reasons;
drive summary; per-volume free-space observation.

## Success Criteria

- All destructive automated tests use generated isolated fixture roots.
- Out-of-scope, changed, locked, linked and protected fixture files survive cleanup.
- Preview and dry run preserve every fixture file.
- Build succeeds with the existing Windows .NET Framework compiler, without NuGet.
- Windows manual verification distinguishes tested flows from deferred destructive checks.

## Assumptions and edge cases

The existing WPF/.NET Framework 4.8 hub is retained to avoid a runtime migration.
Compressed/sparse files and other processes mean free-space deltas can differ from
logical bytes, including negative deltas; unavailable metrics stay unavailable.
No undo is promised for temp deletion. Windows-managed cleanup remains in Windows.
An unlocked old file can still be useful; users must review selected files.

# WinOpt: read-only duplicate discovery

Requested 2026-11-24, superseding age-based cleanup in the WinOpt interface.

## User journey

Choose one or more folders, optionally exclude paths and include subfolders, then
find duplicate contents. Review groups of matching files with full paths, size and
modified dates. Cancel a scan and see clearly partial results and skipped counts.

## Required behavior

- Never delete, move, rename, overwrite, recycle or change scanned files.
- No cleanup buttons, age thresholds, automatic keep/remove choices or filename logs.
- Group by length, hash possible matches with SHA-256, then compare bytes before
  reporting a confirmed group. Different filenames can match; identical names alone
  prove nothing. Revalidate file identity/metadata and reject changed files.
- Scope is only explicitly selected local folders. Do not follow reparse points,
  cloud placeholders or network paths. Do not count hard links as extra copies.
- Read files in bounded buffers with cancellation; bound the number of entries.
- Results describe content equality at scan time. A duplicate is not automatically
  unwanted, and matching logical bytes are not a promise of recoverable disk space.
- Disable the legacy cleanup execution entry point as defense in depth. Preserve
  historical source/documents, with obsolete workflows clearly identified.

## Verification

Only create and scan new uniquely named fixture trees. Preserve fixtures and write
a manifest of created files. Never execute the legacy destructive test suite or
scan real user folders during development. Check matching/different bytes, renames,
size filtering, exclusions, nested roots, changed/locked files, links, cancellation,
limits, unchanged fixture contents and an interface without cleanup controls.

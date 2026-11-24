# Read-only duplicate discovery

WinOpt compares file contents in folders explicitly selected in the app. It does
not select folders automatically. Choose **Duplicate files → Choose folder…**,
optionally include subfolders or exclude full paths, then **Find duplicate contents**.
Select a group to see its files and select a row to inspect the full path.

The scanner first groups files by length, reads potential matches with SHA-256,
then compares bytes to confirm equality. Filename and age are not matching rules.
Files are opened with read access only. Identity and metadata are checked before
and after reading. Links, hard-linked files, system files, cloud placeholders,
inaccessible/locked files and changed files are skipped. Network/removable locations
are excluded from this version. The scan is bounded to 100,000 inspected entries.

Cancellation and limits produce clearly partial results. Matching contents describe
what was read during the scan; files can change afterward. A matching file can
still be important in its original folder. There is no automatic keep/remove choice,
deletion action, age-based cleanup or claim of recoverable disk space. Empty files
can appear as matching contents, with a size of zero.

WinOpt does not save filename history or scan results, and uploads nothing. The
legacy cleanup service and native write/deletion entry points are disabled.
The old automatic updater-download pruning task is disabled as well; existing
updater downloads are never removed based on age.
Historical temporary-cleanup source/tests/specs are retained for reference; they
are not the current workflow and must not be used to clean real data.

## Build and verification

`Build.ps1` now defaults to a new uniquely named output directory. It refuses to
overwrite an existing executable. The build's generated version metadata stays
inside that new build directory. Existing output and user temporary folders are
never cleaned by the duplicate workflow.

```powershell
.\Build.ps1
.\Test-WinOpt.ps1
```

`Test-WinOpt.ps1` runs only the new read-only discovery tests. Each run creates a
new directory below `dist\DuplicateTests`, records fixtures in `created-fixtures.tsv`
and retains all files. It performs no deletion. Tests also check that no cleanup
controls remain and that the old destructive entry points refuse execution.
Native hard-link/symbolic-link fixture checks can be reported as skipped when the
execution sandbox or Windows permissions do not support creating those new links.

During development, scan only newly created fixtures. Real user data is read-only
and must never be used for cleanup tests, regardless of file age or location.

Verified 2026-11-24: 23 tests passed with no failures or skips, including native
link fixtures, unchanged content/attributes, preservation of old updater downloads, cancellation, large files, scope,
group selection and rounded tab layout. Full application compilation succeeded.
No real user folders were scanned or changed during verification. The full app
was not launched, avoiding unrelated startup/account/update behavior.

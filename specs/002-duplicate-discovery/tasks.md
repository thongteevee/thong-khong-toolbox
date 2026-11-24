# Tasks

- [x] Specify the read-only scope and supersede the age-based cleanup UI.
- [x] Implement size filtering, SHA-256 hashing, exact comparison, identity checks,
  exclusions, limits and cancellation.
- [x] Add folder selection, duplicate groups and full path review in the embedded view.
- [x] Disable legacy cleanup, deletion and write-access entry points.
- [x] Replace the default test runner with non-destructive duplicate tests and
  uniquely named, retained fixtures with a created-file manifest.
- [x] Verify final build and tests; document remaining limits and how to run.

2026-11-24: 23 read-only discovery/component tests passed, none failed or skipped,
including native hard links and symbolic links. Every fixture was retained in a
new test-run directory with a created-file manifest. No real user scan, cleanup,
drive optimization or interactive application launch was performed.

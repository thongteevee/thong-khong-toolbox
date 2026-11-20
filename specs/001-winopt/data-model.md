# Data model

- **TempRoot**: absolute approved local directory and category; production roots
  resolved internally, fixture roots admitted only by the test harness.
- **ScanPolicy**: mode, UTC reference instant, roots and canonical exclusions.
- **FileStamp**: volume serial, file index, link count, attributes, size, creation
  and last-write times obtained through a handle.
- **Candidate**: path, category/root and FileStamp; immutable outside construction.
- **ScanResult**: policy, completion state, read-only candidates, skipped reason counts.
- **CleanupResult**: dry-run flag, completed/skipped/failed counts, logical bytes,
  cancellation, reason counts and free-space before/after per volume.
- **DriveSummary**: root, capacity/free, media description and query availability.

No candidate filenames are persisted. A preview is single-use after real cleanup;
dry runs can be repeated until expiry. Policy edits require another scan.

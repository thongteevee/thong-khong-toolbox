# Toolbox maintenance principles

1. Preview before mutation. Scan is read-only; cleanup requires explicit selection and confirmation.
2. Least privilege. The hub stays unelevated; Windows owns drive optimization and its UAC prompt.
3. Confine cleanup to approved temporary roots; reject links, protected files, changed files and in-use files.
4. Never infer performance improvement. Report logical deleted bytes and observed volume free-space deltas separately.
5. Cancellation preserves completed results. Log aggregate outcomes without filenames.
6. Test destructive behavior only in isolated fixtures, never against this machine's personal or system temp files.
7. Keep scan, policy, native I/O, cleanup, Windows integration and presentation independently understandable.

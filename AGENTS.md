# User data protection — mandatory

The user explicitly instructed on 2026-11-23: **NEVER DELETE OLD FILES. NEVER TOUCH REAL USER DATA.**

- Treat real user data as read-only. Do not delete, move, rename, overwrite, truncate,
  recycle, clean, or otherwise modify it. File age, location, extension, or lack of
  a lock never grants permission to remove it.
- Real user data includes files in user and Windows temporary directories, Downloads,
  Documents, Desktop, project folders, cloud folders, app data, logs, backups and
  existing build/output directories. A folder named Temp or dist is not disposable.
- Never run WinOpt cleanup, other cleaners, drive optimization, or destructive
  verification against real user/system locations. Do not launch a cleanup action
  to reproduce a reported problem.
- Destructive tests may operate ONLY on disposable files created by that same test
  run inside a new, uniquely named, isolated fixture directory. Keep an explicit
  record of created fixtures; verify the exact directory and its scope before
  teardown. Never reuse or clean up pre-existing fixture/output directories.
- Ordinary source edits specifically requested by the user may change the intended
  repository source files. That does not authorize changing personal data or
  deleting existing files. Use new output paths for builds and probes.
- Review and diagnosis must not trigger real cleanup. When uncertain whether a file
  is a newly created disposable fixture, preserve it.

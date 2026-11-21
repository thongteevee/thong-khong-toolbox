# App review fixes

The review used the current source after 0.1.13 and the committed sign-in/startup/updater fixes. The native desktop inspection reached installed version 0.1.11; current-source behavior was verified with disconnected WPF controls, simulated Sling responses, local loopback sign-in fixtures, and isolated filesystem fixtures.

Fixed issues:

- Null or incomplete settings and damaged appearance preferences could still stop startup. Preferences are validated, preserved in uniquely named unreadable backups, and restored to safe defaults with a notice. Live application still requires selected calendars.
- A refresh token without its Google client could crash connection loading; malformed Sling connections appeared connected. Invalid connections are disabled in memory with a reconnect notice while other valid connections and the original vault remain intact.
- Changing weeks cleared the report without clearing its cache marker. Returning after a failed load could leave the calendar blank. Cache markers now follow the report lifecycle.
- Demo offers stayed in a fixed November 2026 week. Sample events now follow the selected week using Eastern wall-clock times, including daylight-saving transitions.
- Uncertain applications were never reconciled. Fresh Sling evidence can confirm submission or absence; ambiguous outcomes remain blocked. Changed journal states are saved atomically. Damaged history is preserved and stops submission.
- Update cleanup could partially delete a GUID-named folder containing unrelated content. Cleanup now skips linked roots, nested folders, links, and unrecognized files before deleting anything.

Regression coverage checks those cases, preserves duplicate prevention and account isolation, and retains the existing WinOpt cleanup/layout suite. Google/Sling/Workday submissions and cleanup of personal or system temporary files were not used for testing.

[Workday timesheet submission](roadmap.md) is planned for later; no Workday integration was added.

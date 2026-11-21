# Later: Workday timesheets

The user wants to explore submitting school timesheets to Workday using the supervisor-approved Sling work periods. This is deferred; the current review does not implement Workday access or submission.

- Keep scheduled shifts, recorded clock-in/out, approval state, and paid minutes distinct. Approved hours can differ from shift duration, and paid time can differ from elapsed clock time.
- Map work periods into the school's pay period, time zone, job/worktag, and required time-entry format. Account for unpaid breaks and later corrections.
- First determine which integration the school permits. Use the normal school sign-in and verification flow for any browser integration; availability of an authorized API is still unknown.
- Start with a read-only comparison or export. Show the proposed entries and existing Workday entries before implementing submission and duplicate prevention.
- Add submission only after the real entry flow, permissions, and confirmation requirements are understood. Keep draft, submitted, and supervisor-approved states distinct.

Current Sling timesheet access remains read-only. Do not infer approval or attendance from an assigned shift.

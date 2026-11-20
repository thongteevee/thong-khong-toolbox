# Sling chat handoff

Prepared November 20, 2026 for the original Sling development chat.

## Project state

Sling Shift Auto Apply is integrated into **Thong Khong Toolbox**, a C#/WPF Windows hub. The published baseline covered by this handoff is **v0.1.10**.

At handoff time, the workspace contained additional uncommitted Sling changes, including timesheet parsing and status/display changes. Inspect the current Git diff before editing so ongoing work is preserved.

[Source repository](https://github.com/thongteevee/thong-khong-toolbox)

## User requirements

- Preview the week selected in the calendar.
- There is no application-count or weekly-hours cap.
- Multiple applications may overlap each other. Supervisors decide which applications become assignments.
- Existing calendar commitments and assigned shifts block conflicting applications.
- Pending applications do not block other applications.
- The current planner also excludes started shifts and open-ended shifts.
- Special class exceptions can produce **Needs confirmation**. Selecting one confirms the displayed exception for that specific shift.

Earlier implementations treated applications like scheduled work and imposed hours limits. These behaviors caused valid offers to disappear.

## Interface and selection

Opening Sling shows the selected week's schedule. **Preview matches** opens Results with a calendar and a resizable checklist beside it. The navigation sidebar is also resizable.

The checklist ordering is as follows.

1. **Can apply** is selectable and initially checked.
2. **Needs confirmation** is selectable and initially unchecked.
3. **Applied** is visible as a read-only status.
4. Skipped or conflicting offers stay hidden from the selectable list.

**Auto apply** submits the checked shifts after rechecking their current eligibility. The calendar also shows applied shifts. Hover details include the actual shift name, time, role, location, status, and notes.

The circular selectors use independent `ToggleButton` controls with two-way binding and `INotifyPropertyChanged`. This preserves multi-selection. Earlier checklist problems repeatedly affected the user, so verify clicking, clearing, and selecting multiple rows through the actual WPF controls.

## Sling data interpretation

The implementation uses this API host.

```text
https://api.getsling.com/v1/
```

Shift titles come from position and location information. Notes belong in the detail view. A cached groups lookup resolves position/location IDs when the response omits names.

Application detection must identify the signed-in employee's application. A shift can have applications from several people.

Before live operations, `VerifyAccount()` checks the current organization and employee against the saved connection. Preserve this check.

## Application requests and speed

The implemented request shape is shown below.

```http
POST /v1/shifts/{ApplyId}/applications?ignoreConflicts=false
Authorization: <saved Sling session token>
Content-Length: 0
```

It uses an empty body and disables `Expect: 100-continue`.

Submissions run in batches of **three concurrent requests** through `Task.WhenAll`. The shared HTTP client allows four connections per server and has a 25-second timeout.

Request construction, response interpretation, and duplicate prevention all matter when diagnosing slow or failed submissions. Increasing concurrency alone did not establish successful application behavior.

A live test should confirm that the application appears in the correct employee's Sling account. The request shape above describes the implementation and still requires live outcome verification.

## Submission states and duplicate prevention

Before sending a batch, the runner records each request as **Uncertain** in the local journal.

- HTTP **201** becomes **Submitted**.
- Network failures, timeouts, server errors, and unexpected successful status codes become **Check Sling**.
- Authentication failures, rate limits, server failures, and ambiguous outcomes stop subsequent batches.
- Definite rejected requests receive a visible failure result.
- After submission, the app refreshes the Sling schedule to distinguish an application awaiting approval from a confirmed assignment.

Preserve uncertain outcomes across retries. Reconcile them with Sling before allowing another submission.

Journal entries are scoped to the organization/employee account. Prior applications are rechecked where Sling provides enough information to determine whether they still exist.

## Calendar behavior

Applied shifts appear once on both Week and Results calendars. A confirmed booking replaces the applied representation. Applied shifts remain read-only in Results.

The preview considers the selected week while fetching enough surrounding schedule information to detect boundary and overnight conflicts.

The calendar uses pixel-based scrolling. Earlier row-based scrolling moved too far per wheel action.

## Shared accounts and storage

Google and Sling connections are managed centrally in Toolbox Settings.

- Google uses browser OAuth with PKCE and a loopback callback.
- The app requests read-only calendar-list and event permissions.
- Credentials and refresh/session tokens are protected with Windows DPAPI.
- Preferences and the application journal are separate local JSON files.

The public release currently requires users to configure their own Google Desktop OAuth client.

The shared credential vault is `%LOCALAPPDATA%\ThongKhongToolbox\accounts.dpapi`. Sling preferences and application history are under `%LOCALAPPDATA%\ThongKhongToolbox\Sling`.

## Main code locations

| Area | File |
| --- | --- |
| Sling requests and parsing | [SlingClient.cs](../src/Features/Sling/SlingClient.cs) |
| Conflict and exception rules | [Planner.cs](../src/Features/Sling/Planner.cs) |
| Preview, submission, and journal reconciliation | [ShiftRunner.cs](../src/Features/Sling/ShiftRunner.cs) |
| Checklist selection state | [ResultItem.cs](../src/Features/Sling/Views/ResultItem.cs) |
| Calendar and hover details | [WeekTimeline.cs](../src/Features/Sling/Views/WeekTimeline.cs) |
| WPF actions and result filtering | [ToolboxWindow.Sling.cs](../src/Features/Sling/Views/ToolboxWindow.Sling.cs) |
| Shared connections | [HubAccounts.cs](../src/Hub/HubAccounts.cs) |
| Google Calendar integration | [GoogleClient.cs](../src/Integrations/GoogleCalendar/GoogleClient.cs) |
| Shared HTTP transport | [Api.cs](../src/Common/Api.cs) |

## Verification and release lessons

This chat verified demo submissions, applied-calendar entries, read-only result rows, booking replacement, and WPF selection/navigation behavior. These checks do not establish live Sling acceptance.

The **34 tests mentioned for v0.1.10 were WinOpt tests**. Additional Sling tests existed as uncommitted workspace work at handoff time and need their own review.

This chat previously excluded WinOpt's uncommitted integration from releases, which made it disappear from the user's downloaded app. That was corrected in v0.1.10. Future releases should include the complete intended hub and verify both modules' normal entry points.

Build through `Build-Release.ps1` using the release version. Publish both Windows assets with their existing filenames and matching checksums. The website follows GitHub's latest release, and the app's **Update now** feature supports installed and portable copies.

The user's standing instruction is to always publish releases to GitHub. A release is complete only after its source commit, matching version tag, portable app, installer, and checksums are published and verified. Version 0.1.11 adds the past-day display filter and read-only timesheet status colors. Timesheet approval remains with the supervisor; the app only reads and displays the recorded times.

The two findings in [the Sling review](sling-review-feedback.md) are addressed in 0.1.11. `Schedule` returns the scheduling data without fetching timesheets. `Run` performs eligibility checks, submissions, and assignment reconciliation before enriching work history. Timesheet enrichment uses three concurrent requests and an eight-second total cancellation deadline. Past days render only recorded work periods, never inferred attendance from an assignment. Successful empty timesheets remain distinguishable from unavailable data. Sling fixtures cover delayed lookups, submission independence, concurrency, cancellation, empty history, and unavailable history.

## Next live verification

1. Confirm the intended Sling organization and employee.
2. Select the target calendar week and compare fetched offers with Sling's available-shifts view.
3. Verify position/location titles and application ownership.
4. Check multiple offers, including overlapping applications that fit the user's existing commitments.
5. Submit the selected offers and record per-request outcomes without exposing credentials.
6. Refresh Sling and confirm the applications appear on the intended account.
7. Preview again to check duplicate prevention, applied-calendar visibility, and any newly assigned shifts.

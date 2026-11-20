# Review of the original Sling chat's changes

Reviewed November 20, 2026 against the shared workspace changes following published v0.1.10. The other chat was still active during the review, so later edits may change these findings.

## Assessment

The changes mostly follow the approach agreed in the hub chat. They preserve selected-week matching, overlapping applications, the absence of hours/application caps, per-shift confirmation, shared accounts, bounded concurrent submissions, and journal-based duplicate prevention.

The other chat also followed the user's additional requests for yellow Applied, blue Approved, gray Worked, and green Time approved states. It separates actual work periods from scheduled intervals, preserving scheduled times for conflict checks. Timesheet access is read-only.

## P1 — Timesheet enrichment delays the application path

In [SlingClient.Schedule](../src/Features/Sling/SlingClient.cs#L127), each started booked shift triggers an awaited timesheet request inside a sequential loop. Schedule returns only after these requests complete.

[ShiftRunner.Run](../src/Features/Sling/ShiftRunner.cs) awaits Schedule before evaluating and submitting offers, and calls it again when refreshing assignments. Historical display enrichment therefore delays preview and Auto apply. The shared HTTP timeout is 25 seconds. Ten sequential timeouts could add roughly 250 seconds to a single schedule fetch, even though submission batches still use three concurrent requests.

Load the scheduling data needed for conflict checks independently from historical timesheet enrichment. Enrich the historical display with bounded concurrency and appropriate cancellation or timeout handling. Add a test with delayed timesheet responses to verify that applying for future shifts can proceed without waiting for historical display data.

## P2 — Past assigned shifts without recorded work remain visible

The user asked the original chat to show only shifts actually worked after a day has passed. In [WeekTimeline.Render](../src/Features/Sling/Views/WeekTimeline.cs#L84), a booked shift with an empty work-period list falls back to its scheduled interval. The historical filter retains every Booked slot.

I reproduced this with a shift assigned yesterday and a successfully loaded empty work-period list. Agenda still displayed it with the Approved label. A scheduled assignment alone does not establish that the user worked it.

Filter known historical assignments without completed work periods from worked history. Distinguish a successful empty timesheet response from unavailable timesheet data. Add a fixture for a past assigned shift with no clocked time.

## Checks performed

- Read the recent turns in the original Sling chat, including the user's color and historical-display requests.
- Inspected the current source diff and the existing application pipeline.
- Ran Test-Sling.ps1 successfully.
- Independently reproduced the historical unworked-assignment display issue.

The new fixture suite covers parsing, employee isolation, adjusted intervals, colors, and selected historical filtering examples. It does not cover network latency or a past assigned shift with a successful empty timesheet response.

The other chat's transcript records read-only checks against real Sling timesheets. My independent validation used local fixtures. Live shift-application acceptance remains a separate verification step.

## Release coordination

The original chat acknowledged rebuilding changed code with the existing 0.1.10 version and subsequently moved to 0.1.11 after the user's instruction. Keep distinct version numbers for changed public builds and distinguish local builds from published GitHub releases.

Keep WinOpt, the compact Home layout, pink/light-teal styling, artist credits, and the self-update mechanism in subsequent complete hub releases.

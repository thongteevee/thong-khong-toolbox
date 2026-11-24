# Sling Shift Auto Apply

Open **Sling Shift Auto Apply** from Toolbox Home or the left rail. For account setup and the Windows build, see [Thong Khong Toolbox](../README.md).

## Try the sample schedule

1. Enable **Demo mode** under **Sling → Rules**.
2. The MATH 326 exception starts with **Confirmed dates**, a ready time of `12:30`, and Tuesdays and Thursdays selected. Enter `2026-11-17` in **Confirmed dates** to allow only the sample Tuesday. Choose **Recurring** to use the ready time for each matching class. Ready time includes the walk to work.
3. Choose **Preview matches**, then **Auto apply** to simulate applications to the checked shifts in **Results**.

**Week** loads the current schedule when you open Sling Shift Auto Apply. Choose **Agenda** for a list of the selected week. Hover over a shift in either calendar for its time, role, location, status, and any notes.

**Today** returns to the current Eastern-time week. The refresh icon fetches that week's schedule again, including changes made in Sling since it was loaded.

Auto apply sends one request at a time, starts with a 67 ms gap after each response, and uses a 12-second response timeout per request. After an HTTP 417 response, it adds 50 ms to the gap and retries the same shift up to 10 total attempts. A timeout shows **Check Sling** and pauses the remaining requests. Sling rate limits also pause the queue. Preview again to recheck the outcome before continuing.

## Live runs

**Preview matches** opens **Results** with the calendar and shift checklist. Both calendars show submitted shifts as **Applied** while they await assignment. Results also shows applied shifts as read-only rows. Once Sling assigns a shift, it appears as booked. Use the arrows on **Week** to select the week you want to preview. Eligible shifts start checked. Select a **Needs confirmation** row to approve its listed class exception for that shift. Calendar commitments and assigned shifts can block a match. Pending applications and other offers do not block overlapping applications. **Auto apply** rechecks the schedule and submits only checked shifts that are still eligible. Sling approval and assignment follow the employer's settings. If a submission result is ambiguous, check the shift in Sling. The module journals submissions locally to avoid a repeat request.

Calendar selections, work rules, and the application journal are stored under `%LOCALAPPDATA%\ThongKhongToolbox\Sling`. Toolbox Settings manages the account connections.

[Sling API and authentication](https://github.com/getsling/getsling-api-docs)

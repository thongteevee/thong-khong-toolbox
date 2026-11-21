# Thong Khong Toolbox

Thong Khong Toolbox is a Windows desktop hub built in C# and WPF. **Sling Shift Auto Apply** compares open Sling shifts with your Google Calendar and submits applications for the shifts you select. **WinOpt** previews temporary-file cleanup and connects you with Windows storage and performance diagnostics. Both tools run inside the same window.

**[Download Windows installer](https://github.com/thongteevee/thong-khong-toolbox/releases/latest/download/ThongKhongToolboxSetup.exe)** · **[Download portable EXE](https://github.com/thongteevee/thong-khong-toolbox/releases/latest/download/ThongKhongToolbox.exe)** · [Release notes and checksums](https://github.com/thongteevee/thong-khong-toolbox/releases/latest)

## Run it on Windows

Both downloads run on Windows with .NET Framework 4.8 or later.

- **Windows installer.** Run `ThongKhongToolboxSetup.exe`, accept the [User Agreement](installer/UserAgreement.txt), and review the [Privacy Policy](installer/PrivacyPolicy.txt). Setup installs for your Windows user and adds a Start menu shortcut. You can remove it from **Windows Settings → Apps**.
- **Portable EXE.** Run `ThongKhongToolbox.exe` directly from the folder where you saved it.

Both editions use the same local connections and preferences within your Windows profile. Uninstalling keeps that data. See the [Privacy Policy](installer/PrivacyPolicy.txt) for revocation and local deletion steps.

Open **Sling Shift Auto Apply** from Home. To try it with sample shifts, enable **Demo mode** under **Sling → Rules**, then choose **Preview matches**. Demo applications are simulated.

## Miku teal and pink theme

**Toolbox Settings → Miku theme** controls the teal and pink palette and original Miku artwork. The art is credited to KEI and Crypton Future Media under CC BY-NC 3.0, with source and license links in the app. Music, heart, and flower decorations use font glyphs. Your choices are saved for the next launch. See the [artist credits and permission request](docs/artist-permissions.md).

## Check for updates

Toolbox checks for updates at startup. A newer stable release shows an **Update now** / **Later** banner. Checks run in the background; an unavailable connection does not interrupt startup. **Update now** downloads the release, verifies its SHA-256 checksum, and restarts the app. You can also check manually in **Settings → Updates**. Installed copies upgrade through Windows Setup. Portable copies retain a backup alongside the EXE. Settings and connections are stored separately.

## WinOpt storage maintenance

Open **WinOpt** from Home or the sidebar. It uses the same window and shared pink and light teal theme. Home's **Grid / List** switch sits beside the Apps heading and changes the app catalog layout.

Search Home by app name or description. **Ctrl+F** opens search; **Ctrl+1/2/3** opens Home/Sling/WinOpt, and **Ctrl+,** opens Settings. Enter opens a single search match; Escape clears the search.

1. **Storage** shows local drives and free/used space. **Temporary files** opens Cleanup.
2. **Cleanup → Scan** previews user temporary files older than 7 days. Select files, then choose **Clean selected…** and confirm permanent deletion. Nothing starts selected. Close installers first; an old, unlocked file can still be useful.
3. The list starts with the largest files first and fits at least 10 rows. Drag its lower grip to resize it, or choose **Expand list** for a larger resizable window. Selections stay shared. Empty scans show whether no files were found or the scan was incomplete.
4. **Results** shows the latest outcome. **Copy** copies the summary, including technical details when Advanced mode is on. Completed operations leave a full progress bar. Cleanup consumes the preview; scan again to continue. Previews expire after 30 minutes.

Turn on **Advanced mode** for cleanup modes, optional Windows Temp, exclusions, full paths, category/date columns, test runs, Windows tools, and activity details. Safe requires creation and modification more than 7 days ago; Standard requires more than 24 hours; Aggressive includes otherwise eligible files. Returning to simple mode restores Safe/user-temp scope and clears the preview while preserving exclusions. Advanced results distinguish deleted logical bytes from observed drive free-space changes. Changing scan policy invalidates the preview. Unusual TEMP redirects into personal/project or general-purpose folders are refused.

WinOpt skips linked, changed, locked, protected and excluded files; it never recursively deletes directories. Its cleanup scope is temporary folders, with conservative installer/payload/project exclusions. It does not delete Downloads, Documents, browser profiles, Windows component-store files or Recycle Bin contents. Native Windows cleanup tools have their own scope and confirmation screens. No administrator prompt is requested by the hub.

Use **Task Manager**, **Startup apps**, and **Installed apps** links to investigate responsiveness separately. WinOpt does not claim that deleting temporary files makes a PC faster. Scheduling, app-aware cache cleanup, in-app process/startup management, large-file analysis and duplicate discovery remain future work.

Aggregate logs (without filenames) stay in `%LOCALAPPDATA%\ThongKhongToolbox\WinOpt\Logs`; approximately two 1 MiB files are retained. Scan choices and file previews are kept in memory for this session.

The [reviewed implementation plan](specs/001-winopt/plan.md), [research comparison](docs/winopt-research.md), and [test plan / limitations](specs/001-winopt/test-plan.md) explain the choices and component flow. Official Spec Kit templates and workflow helpers are vendored under [.specify](.specify/README.md).

## Set up live mode

### Connect Google Calendar

1. In a Google Cloud project, [enable the Google Calendar API](https://console.cloud.google.com/apis/library/calendar-json.googleapis.com).
2. Set up the Google Auth Platform consent screen. For a personal Gmail account, choose **External** and add that account as a test user if the app is in **Testing**. Add the two read-only scopes below under **Data Access**. [Google consent setup](https://developers.google.com/workspace/guides/configure-oauth-consent)

   - `https://www.googleapis.com/auth/calendar.calendarlist.readonly`
   - `https://www.googleapis.com/auth/calendar.events.readonly`

3. Create a **Desktop app** OAuth client in [Google Auth Platform → Clients](https://console.cloud.google.com/auth/clients).
4. In Toolbox, open **Settings → Google OAuth setup**, enter the client ID and secret, and save. Click **Connect Google** and authorize Calendar access in your browser. Google OAuth apps with an External audience in Testing can require reconnecting after seven days. [Google refresh token rules](https://developers.google.com/identity/protocols/oauth2#expiration)

### Connect Sling

1. Open **Toolbox Settings → Sling**. Sign in with your Sling email and password, or use a Sling browser session token.
2. For the browser option, sign in to Sling, open the browser's **Network** panel, and reload. Open a successful request to `api.getsling.com` and copy the value of `Authorization` from **Request Headers** into Toolbox's masked **Authorization token** field. Click **Connect Sling**. [Sling API authentication](https://github.com/getsling/getsling-api-docs#authentication)
3. In **Sling → Rules**, load and select the calendars that should block work. Review the class exceptions and disable or remove the example rule if it does not apply to you. Mark informational all-day events as **Free** in Google Calendar.

Toolbox uses the Sling password only to establish a session. Windows encrypts the saved Sling session and Google connection for your user account in `%LOCALAPPDATA%\ThongKhongToolbox\accounts.dpapi`.

## Preview and apply

1. Select a week in the **Week** tab, then click **Preview matches**.
2. **Results** shows the week's calendar and checklist. **Can apply** shifts start selected. **Needs confirmation** shifts start unchecked. Selecting one approves the listed class exception for that shift only.
3. Click **Auto apply** to recheck and submit the selected shifts. Toolbox reports how many applications were sent and shows Sling errors for declined requests. Supervisors decide which shifts to assign.

Submitted applications appear as **Applied** on the calendar and as read-only rows in Results. Once Sling assigns the shift, the calendar shows it as booked. Toolbox tracks applications to prevent duplicate submissions. Calendar commitments and assigned shifts can block an application. See the [Sling tool guide](auto-sling-shift-apply/README.md) for the sample schedule and run behavior.

Uncertain requests are rechecked against Sling. Confirmed applications remain blocked from duplicate submission; a definitively absent request can be selected again. If the local application history is damaged, submissions stop with recovery guidance instead of discarding the history.

The calendar uses yellow for **Applied**, blue for **Approved** shifts, gray for completed **Worked** time, and green for **Time approved**. Worked and approved time come from your Sling timesheet. Their blocks use the recorded clock-in and clock-out times, and the event details also show the scheduled times. Past days show only recorded work. If Sling cannot return timesheet status, the app reports that some work history could not be loaded. Display-only timesheets load after shift submissions, with up to three requests at once and an eight-second total timeout.

## Build from source

From the repository root, run the following in PowerShell on Windows with the .NET Framework 4.x compiler available.

```powershell
.\Build.ps1
```

The executable is written to `dist\ThongKhongToolbox.exe`. Use `-OutputDirectory` to choose a different build folder.

To build both release packages and their SHA-256 checksums, install [Inno Setup](https://jrsoftware.org/inno-setup/downloads) and run the release build. It embeds the version in the app and installer and writes to `dist\v0.1.2` with the version shown below. Publish it under the matching tag `v0.1.2` so the update checker can compare versions. If your compiler is installed elsewhere, pass its `ISCC.exe` path with `-InnoCompiler`.

```powershell
.\Build-Release.ps1 -Version 0.1.2
```

Every release must be published to GitHub with its matching version tag, portable EXE, installer, and `SHA256SUMS.txt`. A local build alone is not a completed release. Verify the published assets before reporting that a version is available.

| Folder | Contents |
|---|---|
| `src/Hub` | WPF window, navigation, settings, and shared account connections |
| `src/Features/Sling` | Schedule matching, submissions, calendar, and checklist |
| `src/Features/WinOpt` | Temp-file policy, scanner, handle-based cleanup, Windows diagnostics and embedded view |
| `src/Integrations/GoogleCalendar` | Google sign-in and Calendar requests |
| `src/Common` | HTTP transport and encrypted local storage |
| `installer` | Windows setup, User Agreement, and Privacy Policy |

For a personal build with a preconfigured Google OAuth client, set `TOOLBOX_GOOGLE_CLIENT_ID` and `TOOLBOX_GOOGLE_CLIENT_SECRET` in the build environment. GitHub release builds let each user enter their own OAuth client details in Settings.

Run WinOpt's dependency-free safety tests on Windows:

```powershell
.\Test-WinOpt.ps1
# Optional read-only drive probe:
.\Test-WinOpt.ps1 -ReadOnlyDriveProbe
```

All test deletion is confined to generated repository fixtures under `dist\WinOptTests\fixtures`. Native link tests require Windows hard-link/symlink support; restrictive execution sandboxes can block those fixture operations. No tests clean real user or Windows temporary directories or optimize drives.

Run `.\Test-Sling.ps1` to check timesheet parsing, adjusted work hours, calendar and agenda colors, and past-day filtering using local fixtures.

Run `.\Test-Updates.ps1` for startup checks, suggestions, dismissal, retry, offline handling, and banner layout using a simulated release service.
The same suite checks Home search, keyboard navigation, and Sling's Today/Refresh controls without live account requests.

## Public website

The [website and Google OAuth handoff](docs/website-handoff.md) covers the product page, download, privacy policy, terms, and Google Auth Platform settings.

The [future Workday plan](docs/roadmap.md) records the deferred timesheet-submission idea. Current Sling timesheets remain read-only.

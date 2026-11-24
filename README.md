# Thong Khong Toolbox

Thong Khong Toolbox is a Windows desktop hub built in C# and WPF. **Sling Shift Auto Apply** compares open Sling shifts with your Google Calendar and submits applications for the shifts you select. **WinOpt** finds identical file contents in folders you choose, without changing your files. Both tools run inside the same window.

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

## WinOpt duplicate discovery

Open **WinOpt → Duplicate files** from Home or the sidebar. Choose one or more folders, optionally exclude paths or include subfolders, then select **Find duplicate contents**.

WinOpt filters by length, hashes possible matches with SHA-256, then compares bytes. Different names and dates can still match; names and ages alone do not prove duplication. Select a matching group to inspect file paths, sizes and modified dates. Cancellation and scan limits produce clearly partial results.

**This is read-only. There are no cleanup, deletion, rename, move or automatic keep/remove actions.** The legacy cleanup service and native write/deletion entry points are disabled. Matching contents do not mean a file is unwanted. No filename history or scan results are written to disk or uploaded.

The automatic startup task that pruned old updater downloads is also disabled. Existing updater downloads are preserved regardless of age.

Links, cloud placeholders, system files, locked/inaccessible files, hard-linked files and files that change during scanning are skipped. This version scans explicitly selected folders on local fixed drives and stops after 100,000 inspected entries. Details are in the [duplicate-discovery guide](docs/duplicate-discovery.md) and [specification](specs/002-duplicate-discovery/spec.md).

Storage information remains available on request, alongside links to Task Manager, Startup apps and Installed apps. These are separate from duplicate discovery. WinOpt makes no claim that finding duplicates speeds up Windows or recovers storage.

Search Home by app name or description. **Ctrl+F** opens search; **Ctrl+1/2/3** opens Home/Sling/WinOpt, and **Ctrl+,** opens Settings. Enter opens a single search match; Escape clears the search.

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

The executable is written to a new uniquely named directory under `dist\builds`. Use `-OutputDirectory` to choose a new build folder. Existing executables are never overwritten.

To build both release packages and their SHA-256 checksums, install [Inno Setup](https://jrsoftware.org/inno-setup/downloads) and run the release build. It embeds the version in the app and installer and writes to `dist\v0.1.2` with the version shown below. Publish it under the matching tag `v0.1.2` so the update checker can compare versions. If your compiler is installed elsewhere, pass its `ISCC.exe` path with `-InnoCompiler`.

```powershell
.\Build-Release.ps1 -Version 0.1.2
```

Every release must be published to GitHub with its matching version tag, portable EXE, installer, and `SHA256SUMS.txt`. A local build alone is not a completed release. Verify the published assets before reporting that a version is available.

| Folder | Contents |
|---|---|
| `src/Hub` | WPF window, navigation, settings, and shared account connections |
| `src/Features/Sling` | Schedule matching, submissions, calendar, and checklist |
| `src/Features/WinOpt` | Read-only duplicate scanner, native file inspection, Windows diagnostics and embedded view |
| `src/Integrations/GoogleCalendar` | Google sign-in and Calendar requests |
| `src/Common` | HTTP transport and encrypted local storage |
| `installer` | Windows setup, User Agreement, and Privacy Policy |

For a personal build with a preconfigured Google OAuth client, set `TOOLBOX_GOOGLE_CLIENT_ID` and `TOOLBOX_GOOGLE_CLIENT_SECRET` in the build environment. GitHub release builds let each user enter their own OAuth client details in Settings.

Run WinOpt's dependency-free safety tests on Windows:

```powershell
.\Test-WinOpt.ps1
```

The current WinOpt suite performs no deletion. Each run creates a new directory under `dist\DuplicateTests`, records created fixtures in a manifest, and retains them. Native link tests may be skipped if Windows or the execution sandbox prevents creating fixture links. Do not execute the historical cleanup tests against real user data.

Run `.\Test-Sling.ps1` to check timesheet parsing, adjusted work hours, calendar and agenda colors, and past-day filtering using local fixtures.

Run `.\Test-Updates.ps1` for startup checks, suggestions, dismissal, retry, offline handling, and banner layout using a simulated release service.
The same suite checks Home search, keyboard navigation, and Sling's Today/Refresh controls without live account requests.

## Public website

The [website and Google OAuth handoff](docs/website-handoff.md) covers the product page, download, privacy policy, terms, and Google Auth Platform settings.

The [future Workday plan](docs/roadmap.md) records the deferred timesheet-submission idea. Current Sling timesheets remain read-only.

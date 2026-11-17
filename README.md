# Thong Khong Toolbox

Thong Khong Toolbox is a Windows desktop hub built in C# and WPF. Its first tool, **Sling Shift Auto Apply**, compares open Sling shifts with your Google Calendar and submits applications for the shifts you select.

**[Download ThongKhongToolbox.exe](https://github.com/thongteevee/thong-khong-toolbox/releases/latest/download/ThongKhongToolbox.exe)** · [Release notes and checksum](https://github.com/thongteevee/thong-khong-toolbox/releases/latest)

## Run it on Windows

1. Download `ThongKhongToolbox.exe` from the latest GitHub release.
2. Run the EXE on Windows with .NET Framework 4.8 or later. The download is a portable executable.
3. Open **Sling Shift Auto Apply** from Home. To try it with sample shifts, enable **Demo mode** under **Sling → Rules**, then choose **Preview matches**. Demo applications are simulated.

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

Pending applications stay out of the checklist and calendar. Toolbox tracks them to prevent duplicate submissions. Calendar commitments and assigned shifts can block an application. See the [Sling tool guide](auto-sling-shift-apply/README.md) for the sample schedule and run behavior.

## Build from source

From the repository root, run the following in PowerShell on Windows with the .NET Framework 4.x compiler available.

```powershell
.\Build.ps1
```

The executable is written to `dist\ThongKhongToolbox.exe`. Use `-OutputDirectory` to choose a different build folder.

| Folder | Contents |
|---|---|
| `src/Hub` | WPF window, navigation, settings, and shared account connections |
| `src/Features/Sling` | Schedule matching, submissions, calendar, and checklist |
| `src/Integrations/GoogleCalendar` | Google sign-in and Calendar requests |
| `src/Common` | HTTP transport and encrypted local storage |

For a personal build with a preconfigured Google OAuth client, set `TOOLBOX_GOOGLE_CLIENT_ID` and `TOOLBOX_GOOGLE_CLIENT_SECRET` in the build environment. GitHub release builds let each user enter their own OAuth client details in Settings.

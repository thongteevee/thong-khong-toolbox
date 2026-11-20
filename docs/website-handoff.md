# Thong Khong Toolbox public website handoff

Build the public product site for **Thong Khong Toolbox**, a Windows desktop hub built with C# and WPF. Its first tool, **Sling Shift Auto Apply**, checks open shifts against the user's Google Calendar and submits applications for the shifts the user selects.

## Pages to build

Use the existing `thongkhong.me` site and its deployment process. These are the proposed canonical URLs. Update the website links and Google Auth Platform together if the site uses different paths.

| Page | URL | Required content |
| --- | --- | --- |
| Product home | `https://thongkhong.me/toolbox` | Product name, purpose, Windows download, demo and live setup, support, and prominent links to Privacy Policy and Terms of Service |
| Privacy Policy | `https://thongkhong.me/toolbox/privacy` | Actual desktop and website data practices, Google permissions and use, storage and retention, service providers, disconnect and deletion steps, support contact, and effective date |
| Terms of Service | `https://thongkhong.me/toolbox/terms` | Owner-approved terms for using Toolbox and submitting Sling applications, support contact, and effective date |

Serve the pages over HTTPS and make them readable without signing in. Use ordinary navigation links so visitors and Google's reviewers can reach the policy from the product page. Google treats the terms link as optional for brand verification. If it stays in Branding, it should point to the terms page. [Google brand verification requirements](https://developers.google.com/identity/protocols/oauth2/production-readiness/brand-verification)

## Product page copy and links

**Thong Khong Toolbox**

Preview which Sling shifts fit your Google Calendar and submit applications for the shifts you choose.

- **Download Windows installer** links directly to [ThongKhongToolboxSetup.exe](https://github.com/thongteevee/thong-khong-toolbox/releases/latest/download/ThongKhongToolboxSetup.exe). Setup includes a User Agreement and Privacy Policy, installs for the current Windows user, and adds a Start menu shortcut.
- **Download portable EXE** links directly to [ThongKhongToolbox.exe](https://github.com/thongteevee/thong-khong-toolbox/releases/latest/download/ThongKhongToolbox.exe). Both downloads require Windows with .NET Framework 4.8 or later. Link to [release notes and checksums](https://github.com/thongteevee/thong-khong-toolbox/releases/latest) and the [source and setup guide](https://github.com/thongteevee/thong-khong-toolbox#readme).
- **Try it with sample shifts.** Open Sling Shift Auto Apply, enable **Demo mode** under **Rules**, and select **Preview matches**. Demo mode uses sample data and simulates applications.
- **Use your own schedule.** Connect Google Calendar and Sling in Toolbox Settings, choose the calendars to check, and select a week. **Preview matches** overlays shifts on the calendar and opens a checklist. Users can adjust the selection and explicitly confirm flagged shifts. **Auto apply** rechecks and submits selected shifts. Sling supervisors decide which shifts to assign.
- **Explain current Google setup before download.** The public EXE currently requires each user to configure a Google **Desktop app** OAuth client in Toolbox Settings. Google consent opens in the user's browser from the desktop app. Link to the [live setup instructions](https://github.com/thongteevee/thong-khong-toolbox#connect-google-calendar). A website login or one-click Google setup would require separate desktop release work.
- Use the [Toolbox SVG logo](../assets/toolbox-logo.svg) for the product page. A [PNG export](../assets/toolbox-logo.png) and [Windows ICO](../assets/toolbox.ico) are also available. The Segoe UI Bold TK mark with blue app tiles is embedded in both Windows packages. Use Demo mode for screenshots so the site shows the calendar and checklist without exposing real schedules or accounts.

## Privacy Policy content

Use the [desktop Privacy Policy](../installer/PrivacyPolicy.txt) and these facts to draft the website copy for the current Windows release. Confirm the website host's actual logging, cookies, analytics, and retention before publishing the website section. A static site with download links and a support email is sufficient. Keep sign-in, calendar data, and credentials inside the desktop app.

| Data | Access, use, and storage |
| --- | --- |
| Google Calendar list | With consent, Toolbox reads calendar names, identifiers, and access information to let users choose which calendars to check. Selected calendar identifiers are kept in local settings and can contain an email address. |
| Google Calendar events | Toolbox reads event titles, start and end times, status, busy/free flags, and the user's attendance response for selected calendars. It uses these fields to show the schedule, check overlaps, and evaluate user-defined exceptions. Fetched events are processed in memory. User-configured rules can retain event-matching text locally. Google permissions are read-only. |
| Google OAuth credentials | Google sign-in occurs in the default browser. Toolbox uses a loopback callback on `127.0.0.1` with PKCE. The desktop client registration and refresh token are saved locally under Windows DPAPI for the current Windows user. Access tokens are used in memory to call Google. |
| Sling connection and requests | Toolbox uses the Sling password to establish a session, or accepts a Sling session token. It saves the session and account details in the Windows DPAPI vault. It sends requests directly to Sling to read schedules and submit selected applications. These requests use Sling account and shift information. Google Calendar event contents stay in the desktop app. |
| Local settings and application journal | Selected calendars and rules are stored in `Sling\settings.json` and the live application journal in `Sling\applications.json`, under `%LOCALAPPDATA%\ThongKhongToolbox`. The journal records Sling shift details and submission state to track outcomes and prevent duplicate submissions. These are local JSON files. The separate credential vault is `accounts.dpapi`. |
| Update checks | At startup, and when a user selects **Check for updates**, Toolbox requests public release metadata from GitHub and identifies the app and its version. An available update is suggested; the user chooses when to install it. **Update now** downloads release files and checksums to a temporary folder, verifies them, and restarts to apply the update. Portable updates retain the previous EXE as a backup. Requests contain no account or schedule data. Users can also open downloads in their browser. |

The desktop app communicates directly with Google and Sling for calendar and shift features and with GitHub for startup and manual update checks. Explain that Toolbox uses Google user data to provide the calendar and shift-matching features described above and does not sell that data or use it for advertising. GitHub serves the executable. If users contact support, explain what the support mailbox receives and verify the owner's retention practice before stating it. [Google API Services User Data Policy](https://developers.google.com/terms/api-services-user-data-policy)

Describe how users control their data.

1. **Disconnect Google** in Toolbox Settings clears the active local refresh token. The saved desktop client configuration and preferences remain. To revoke the grant at Google, remove Toolbox access from [Google Account connections](https://myaccount.google.com/connections).
2. **Disconnect Sling** removes the saved session from Toolbox. Applications already submitted remain in Sling and are managed there.
3. To erase all Toolbox files on that Windows profile, close Toolbox and remove `%LOCALAPPDATA%\ThongKhongToolbox`. This also removes preferences, any older local credential files, and the journal used for duplicate tracking.

Implementation references for the policy include [Google Calendar access](../src/Integrations/GoogleCalendar/GoogleClient.cs), [shared accounts](../src/Hub/HubAccounts.cs), [local storage](../src/Common/LocalData.cs), [Sling requests](../src/Features/Sling/SlingClient.cs), and [the application journal](../src/Features/Sling/ShiftRunner.cs).

## Terms of Service content

Use the [desktop User Agreement](../installer/UserAgreement.txt) as source copy. Identify the operator and the software covered. Explain use with accounts the user is authorized to access, that preview checks availability and **Auto apply** submits the selection to Sling, and that users review selections and confirm submitted applications in Sling. Explain that availability can change and that assignments are decided in Sling. Identify Google and Sling as separate services with their own terms. Get the owner's approval on the operator identity, contact, effective date, and any legal clauses before publishing.

## Google Auth Platform settings

The Branding screen currently shows **Testing** and uses the domain root for the home, privacy, and terms links. Use the final page URLs once they are live.

| Console field | Value or action |
| --- | --- |
| Branding → App name | Use **Thong Khong Toolbox** on the site and consent screen for consistency. The current console value is `thong-khong-toolbox`. |
| Branding → User support email | Use the existing support mailbox from Branding if the owner confirms it as the public address. Use the same approved address on the site. |
| Branding → App logo | The console already shows a `TK.` logo. Confirm the asset and display name before submitting for review. |
| Branding → Application home page | `https://thongkhong.me/toolbox` |
| Branding → Application privacy policy link | `https://thongkhong.me/toolbox/privacy` |
| Branding → Application terms of service link | `https://thongkhong.me/toolbox/terms`, once approved and published |
| Branding → Authorized domains | `thongkhong.me`. Verify ownership in Google Search Console with an account that is an owner or editor of the OAuth project. |
| Branding → Developer contact information | Keep a monitored address in Google Cloud for project notices. |
| Clients | Use a **Desktop app** OAuth client. The WPF app handles the loopback callback. The public site only needs pages and download links. |
| Audience and Verification Center | Review the External audience and production publishing settings, then follow the Verification Center prompts for branding and any scope review required for public access. |

Use these scope explanations in **Data Access** and in the desktop verification video. Request only the two scopes in the app. Show connecting in the desktop app, the browser consent screen, calendar selection, and a week preview using safe test data. [Google Calendar scopes](https://developers.google.com/workspace/calendar/api/auth) · [Google sensitive scope verification](https://developers.google.com/identity/protocols/oauth2/production-readiness/sensitive-scope-verification)

| Scope | Why Toolbox requests it |
| --- | --- |
| `https://www.googleapis.com/auth/calendar.calendarlist.readonly` | Populate the calendar picker so the user can select the calendars that should block work. |
| `https://www.googleapis.com/auth/calendar.events.readonly` | Read events in the selected calendars so Toolbox can overlay the schedule and identify conflicts with open Sling shifts. |

Link the live Privacy Policy from Toolbox near **Connect Google** in the next desktop release so users can find the disclosure when authorizing access. Publishing the site supplies the links for review. The owner still completes Search Console and Google Auth Platform steps, and Google decides verification. [Google disclosure policy](https://developers.google.com/terms/api-services-user-data-policy)

## Build and publish checklist

- Confirm the public support email, operator information, effective date, hosting practices, and approved logo with the owner. Keep OAuth secrets, downloaded client JSON, session tokens, and private screenshots out of the site and repository.
- Build and deploy the three routes through the existing site's project. Check each canonical HTTPS URL returns its intended content when signed out and that the product page links to both policies.
- Test both downloads on the deployed product page while signed out. Confirm the expected setup and portable EXE filenames and check the release notes and setup links.
- Give the owner the three live URLs to enter under Branding and report any site or policy details still awaiting confirmation.

## Prompt for the website chat

> In my `thongkhong.me` website project, build and publish the Thong Khong Toolbox pages using this [website and Google OAuth handoff](https://github.com/thongteevee/thong-khong-toolbox/blob/main/docs/website-handoff.md). Follow the existing site's stack and styling. Use the GitHub Releases links for both Windows downloads, add the privacy and terms pages, and verify that all three URLs and both downloads work while signed out. Use the desktop policy and agreement as source copy and confirm any missing owner or hosting details before publishing.

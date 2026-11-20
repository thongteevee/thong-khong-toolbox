# WinOpt interface

Simple mode is the default. Storage, Cleanup, and Results use short labels and concise outcomes. Cleanup keeps explicit file selection and permanent-deletion confirmation.

Advanced mode reveals cleanup policy, optional Windows Temp, exclusions, full paths, category/date columns, test runs, Windows tools, and technical results. Turning it off restores Safe mode and user-temp scope, invalidates the preview, and preserves exclusions. Safety checks remain enforced in both modes.

The main list fits ten rows, has a drag grip, and opens a resizable expanded review sharing the same selections. Completed operations leave progress at 100%; cancellation and failure do not.

Files start in descending numeric size order. Empty states distinguish an unscanned list, a successful scan with no candidates, and an incomplete scan with no candidates. Copy result includes only the short summary in simple mode and adds the visible technical details in Advanced mode.

Design references reviewed November 20, 2026:

- [Microsoft PC Manager](https://pcmanager.microsoft.com/en-US) groups storage and maintenance around clear user tasks.
- [BleachBit general usage](https://docs.bleachbit.org/doc/general-usage/) uses a preview/review/clean sequence.
- [BleachBit expert mode](https://docs.bleachbit.org/doc/expert-mode/) separates advanced controls from its default interface. WinOpt uses this disclosure pattern while retaining its safety policy in both modes.

# How established tools approach storage and performance

Reviewed 2026-11-19. These are design observations from vendor documentation,
not benchmark results or endorsements. No third-party cleaner was installed or run.

| Source | Documented approach | WinOpt decision |
|---|---|---|
| [Microsoft Storage Sense](https://support.microsoft.com/en-us/windows/experience/storage-filemanagement/manage-drive-space-with-storage-sense) | Configurable temporary-file retention and distinct choices for Downloads/cloud content. | Keep narrow temp roots; leave Downloads/cloud content alone. Open native Storage Sense for OS-managed policy. |
| [BleachBit usage](https://docs.bleachbit.org/doc/general-usage/) and [preferences](https://docs.bleachbit.org/doc/preferences/index.html) | Describe cleaner options, select categories, close affected apps, preview, revise selections, then delete. Excluded paths are honored in preview and execution. | Separate Scan, Dry run and Clean; start unchecked; invalidate previews when options change; apply exclusions twice. |
| [BleachBit cleaner implementation](https://github.com/bleachbit/bleachbit/blob/master/bleachbit/Cleaner.py) | Individual cleaners carry app-specific options, warnings and running-app detection. | Future browser cleanup needs versioned, app-aware rules and active-browser detection; broad extension matching is insufficient. |
| [WizTree FAQ](https://diskanalyzer.com/faq) and [guide](https://diskanalyzer.com/guide) | Logical length and allocated space differ; hard links affect totals. Storage analysis is a separate concern from deciding what to delete. | Label preview size an estimate; refuse hard links in cleanup; show measured volume free-space change separately. Future large-file analysis must track file identity and allocated bytes. |
| [Microsoft Optimize-Volume](https://learn.microsoft.com/en-us/powershell/module/storage/optimize-volume) | Optimization varies for SSD/TRIM, hard disks, virtual and tiered storage, and unsupported media. | Open Windows Optimize Drives for the actual operation and last-run state; never force HDD defrag based on guessed media. |
| [Microsoft performance guidance](https://support.microsoft.com/en-us/windows/experience/performance-optimization/tips-to-improve-pc-performance-in-windows) and [startup impact](https://learn.microsoft.com/en-us/windows/win32/w8cookbook/startup-apps) | Inspect resource bottlenecks and startup CPU/disk work. | Provide native diagnostic links now. Phase 2 should measure CPU time, memory pressure, disk activity and startup impact; do not invent a health/boost score. |
| [BleachBit SSD erasure limitations](https://docs.bleachbit.org/doc/shred-files-and-wipe-disks) | Shredding/free-space wiping has weaker guarantees on SSDs. | No shredding, free-space wiping, or secure-erasure claim in this maintenance tool. |

## Changes adopted in this build

- One integrated Toolbox page with rounded turquoise/charcoal styling, per user feedback.
- Preview-based cleanup with unselected defaults, exclusions, policy invalidation,
  expiration, object-identity checks and non-replayable destructive execution.
- Visible links to Task Manager, Startup Apps and Installed Apps, separate from cleanup.
- An extra guard against a redirected TEMP that points into a project or an
  unexpected general-purpose folder; such locations require Windows cleanup instead.

## Roadmap refinement

1. **Storage discovery**: user-selected read-only analysis, size vs allocated size,
   cancellation, hard-link-aware accounting and no default deletion selections.
2. **Performance diagnosis**: collect a baseline and repeated measurements for the
   same workload; account for warm caches and background activity. Display raw
   measurements before attributing improvement to any change.
3. **App-specific cleaners**: ship reviewed rules with a documented data impact,
   active-process detection, exclusions and isolated test fixtures per app/version.
   Keep credentials, cookies, sessions and history out of default scope.
4. **Optional scheduling**: only after rule/version stability; explicit scope and
   dry-run history first. Prefer Windows' existing maintenance where it already works.

Do not import community cleaner rules or optimizer scripts wholesale: a rule
database is executable maintenance policy and needs code review and tests.

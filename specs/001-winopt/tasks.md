# Tasks: WinOpt

**Input**: spec.md, plan.md, research.md, data-model.md

## Setup

- [x] T001 Review repository and upstream Windows/Spec Kit documentation.
- [x] T002 Vendor version-pinned official templates/helpers and write constitution,
  specification, implementation plan, research, data model and test plan.

## US1 — Preview

- [x] T003 Implement models, path policy and native handle inspection.
- [x] T004 Implement cancellable read-only traversal with skip counts and limits.
- [x] T005 Add policy, exclusions, categories and selectable preview UI.

## US2 — Cleanup

- [x] T006 Implement snapshot validation, dry run, handle deletion and cancellation.
- [x] T007 Add aggregate logging and before/after per-volume measurements.
- [x] T008 Add confirmation, one-shot previews and result summaries.

## US3 — Windows storage

- [x] T009 Add read-only drive/media discovery and native maintenance links.
- [x] T010 Integrate WinOpt into Toolbox Home and navigation; update build resources.

## Verification and handoff

- [x] T011 Test eligibility, paths, links, locks, changed files, cancellation,
  measurements, failures, dry run and logging using isolated fixtures.
- [x] T012 Build and perform non-destructive Windows/UI smoke checks; runtime WPF
  component checks cover final tab bounds, wrapping, Grid/List and tile command binding.
- [x] T013 Update README/privacy policy and record test results/known limitations.

Remaining release checks are explicitly listed in test-plan.md: disposable-VM
end-to-end destructive validation, native optimization/UAC, scale and accessibility checks.
The final live visual recheck of tab borders, embedded tile navigation and initial
cleanup button state is complete.

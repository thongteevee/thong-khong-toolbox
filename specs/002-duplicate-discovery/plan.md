# Implementation plan

1. Implement a read-only scanner and immutable duplicate result models. Reuse the
   native read/identity helpers; never request DELETE or write access.
2. Replace WinOpt's temp cleanup view with folder selection and duplicate groups,
   keeping the integrated window and shared rounded theme.
3. Disable the legacy cleanup service and native deletion entry point. Replace
   the default WinOpt test runner with a non-destructive discovery test runner.
4. Use unique output directories for tests/builds; retain fixture files and record
   their paths in a manifest. Avoid the existing build script's temporary cleanup
   by compiling the verification build directly into its new output directory.
5. Update README and product data-handling text. Leave prior specs as historical
   context; this specification supersedes their age-based cleanup requirements.

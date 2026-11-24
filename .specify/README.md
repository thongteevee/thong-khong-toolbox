# Spec Kit workflow

Official templates and PowerShell helpers are vendored from
[github/spec-kit v1.4.3](https://github.com/github/spec-kit/tree/v1.4.3), under
the included MIT license. Upstream files are unmodified. The CLI is not installed;
the specification → plan → tasks → implementation workflow is performed using
these templates and the official prerequisite checker, without a global Python/uv install.

Active feature: `specs/002-duplicate-discovery`. From PowerShell:

```powershell
$env:SPECIFY_FEATURE_DIRECTORY = 'specs/002-duplicate-discovery'
.\.specify\scripts\powershell\check-prerequisites.ps1 -Json -RequireSpec -RequireTasks -IncludeTasks
```

No generated agent instructions or global configuration changes are required.

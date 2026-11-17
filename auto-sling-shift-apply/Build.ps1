param([string]$OutputDirectory)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $PSScriptRoot 'dist'
}
& (Join-Path (Split-Path -Parent $PSScriptRoot) 'Build.ps1') -OutputDirectory $OutputDirectory

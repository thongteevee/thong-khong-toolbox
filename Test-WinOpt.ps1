param([switch]$ReadOnlyDriveProbe, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework compiler is required.' }
$output = if ($OutputDirectory) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory) } else { Join-Path $PSScriptRoot 'dist\WinOptTests' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src\Features\WinOpt') -Filter '*.cs' -File -Recurse | Select-Object -ExpandProperty FullName)
$sources += Join-Path $PSScriptRoot 'tests\WinOpt\Program.cs'
$sources += Join-Path $PSScriptRoot 'tests\WinOpt\LayoutChecks.cs'
$sources += Join-Path $PSScriptRoot 'src\Hub\ToolCommand.cs'
$references = @('System.dll','System.Core.dll','System.Web.Extensions.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$resources = @(
    ('/resource:' + (Join-Path $PSScriptRoot 'src\Hub\Toolbox.xaml') + ',Toolbox.xaml')
    ('/resource:' + (Join-Path $PSScriptRoot 'src\Features\WinOpt\Views\WinOpt.xaml') + ',WinOpt.xaml')
)
$exe = Join-Path $output 'WinOptTests.exe'
& $compiler /nologo /target:exe /codepage:65001 /warnaserror+ "/out:$exe" $resources $references $sources
if ($LASTEXITCODE -ne 0) { throw 'WinOpt test compilation failed.' }
& $exe
if ($LASTEXITCODE -ne 0) { throw 'WinOpt fixture tests failed.' }
if ($ReadOnlyDriveProbe) {
    & $exe --read-only-drives
    if ($LASTEXITCODE -ne 0) { throw 'Read-only drive probe failed.' }
}

param([switch]$ReadOnlyDriveProbe, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework compiler is required.' }
$base = if ($OutputDirectory) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory) } else { Join-Path $PSScriptRoot 'dist\DuplicateTests' }
$output = Join-Path $base ([Guid]::NewGuid().ToString('N'))
if (Test-Path -LiteralPath $output) { throw 'Refusing to reuse an existing test-run directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
[IO.File]::WriteAllText((Join-Path $output 'created-by-this-test-run.txt'), 'New isolated test run. All fixtures and outputs are retained.')
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File -Recurse | Select-Object -ExpandProperty FullName)
$sources += Join-Path $PSScriptRoot 'tests\WinOpt\DuplicateTests.cs'
$references = @('System.dll','System.Core.dll','System.Windows.Forms.dll','System.Web.dll','System.Web.Extensions.dll','System.Net.Http.dll','System.Security.dll','System.Xml.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','WPF\UIAutomationTypes.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$layout = Join-Path $PSScriptRoot 'src\Features\WinOpt\Views\WinOpt.xaml'
$exe = Join-Path $output 'DuplicateTests.exe'
& $compiler /nologo /target:exe /main:DuplicateTests /codepage:65001 /warnaserror+ "/out:$exe" "/resource:$layout,WinOpt.xaml" $references $sources
if ($LASTEXITCODE -ne 0) { throw 'Read-only duplicate test compilation failed.' }
& $exe $output
if ($LASTEXITCODE -ne 0) { throw 'Read-only duplicate tests failed. All fixtures are preserved.' }
if ($ReadOnlyDriveProbe) { Write-Output 'Drive probing is intentionally not performed by this fixture-only test runner.' }
Write-Output ('Test output: ' + $output)

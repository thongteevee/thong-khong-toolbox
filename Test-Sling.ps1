$ErrorActionPreference = 'Stop'
$project = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
    $compiler = Join-Path $framework 'csc.exe'
}
$output = Join-Path (Join-Path $project 'dist\SlingTests') ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$references = @('System.dll', 'System.Core.dll', 'System.Windows.Forms.dll', 'System.Web.dll', 'System.Web.Extensions.dll', 'System.Net.Http.dll',
    'System.Security.dll', 'System.Xaml.dll', 'System.Xml.dll', 'WPF\WindowsBase.dll', 'WPF\PresentationCore.dll',
    'WPF\PresentationFramework.dll', 'WPF\UIAutomationTypes.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $project 'src') -Filter '*.cs' -Recurse -File | Select-Object -ExpandProperty FullName)
$sources += @(Get-ChildItem -LiteralPath (Join-Path $project 'tests\Sling') -Filter '*.cs' -File | Select-Object -ExpandProperty FullName)
$executable = Join-Path $output 'SlingTests.exe'
& $compiler /nologo /target:exe /main:Toolbox.Tests.TimesheetChecks /codepage:65001 "/out:$executable" $references $sources
if ($LASTEXITCODE -ne 0) { throw 'Sling tests did not compile.' }
& $executable
if ($LASTEXITCODE -ne 0) { throw 'Sling tests failed.' }

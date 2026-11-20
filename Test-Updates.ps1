$ErrorActionPreference = 'Stop'
$project = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$output = Join-Path $project 'dist\UpdateTests'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$references = @('System.dll', 'System.Core.dll', 'System.Web.dll', 'System.Web.Extensions.dll', 'System.Net.Http.dll',
    'System.Security.dll', 'System.Xaml.dll', 'System.Xml.dll', 'WPF\WindowsBase.dll', 'WPF\PresentationCore.dll',
    'WPF\PresentationFramework.dll', 'WPF\UIAutomationTypes.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$resources = @(
    ('/resource:' + (Join-Path $project 'src\Hub\Toolbox.xaml') + ',Toolbox.xaml'),
    ('/resource:' + (Join-Path $project 'assets\toolbox.ico') + ',Toolbox.ico'),
    ('/resource:' + (Join-Path $project 'assets\miku\original-miku.jpg') + ',Miku.original-miku.jpg'),
    ('/resource:' + (Join-Path $project 'src\Features\WinOpt\Views\WinOpt.xaml') + ',WinOpt.xaml')
)
$sources = @(Get-ChildItem -LiteralPath (Join-Path $project 'src') -Filter '*.cs' -Recurse -File | Select-Object -ExpandProperty FullName)
$sources += Join-Path $project 'tests\Hub\UpdateChecks.cs'
$executable = Join-Path $output 'UpdateTests.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:exe /main:Toolbox.Tests.UpdateChecks /codepage:65001 "/out:$executable" $resources $references $sources
if ($LASTEXITCODE -ne 0) { throw 'Update tests did not compile.' }
& $executable
if ($LASTEXITCODE -ne 0) { throw 'Update tests failed.' }

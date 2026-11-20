param(
    [string]$OutputDirectory,
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '0.0.0'
)

$ErrorActionPreference = 'Stop'
$project = $PSScriptRoot
$sourceRoot = Join-Path $project 'src'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
    $compiler = Join-Path $framework 'csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework 4.x compiler is missing.' }
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $project 'dist' }
$output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$references = @('System.dll', 'System.Core.dll', 'System.Web.dll', 'System.Web.Extensions.dll', 'System.Net.Http.dll',
    'System.Security.dll', 'System.Xaml.dll', 'System.Xml.dll', 'WPF\WindowsBase.dll', 'WPF\PresentationCore.dll',
    'WPF\PresentationFramework.dll', 'WPF\UIAutomationTypes.dll') | ForEach-Object {
    '/reference:' + (Join-Path $framework $_)
}
$sources = Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -File -Recurse | Sort-Object FullName | Select-Object -ExpandProperty FullName
$file = Join-Path $output 'ThongKhongToolbox.exe'
$manifest = Join-Path $sourceRoot 'Hub\app.manifest'
$layout = Join-Path $sourceRoot 'Hub\Toolbox.xaml'
$icon = Join-Path $project 'assets\toolbox.ico'
$clientId = $env:TOOLBOX_GOOGLE_CLIENT_ID
$clientSecret = $env:TOOLBOX_GOOGLE_CLIENT_SECRET
if ([string]::IsNullOrWhiteSpace($clientId) -ne [string]::IsNullOrWhiteSpace($clientSecret)) {
    throw 'Set both Toolbox Google client environment variables or leave both empty.'
}
$resources = @("/resource:$layout,Toolbox.xaml", "/resource:$icon,Toolbox.ico")
$mikuPicture = Join-Path $project 'assets\miku\original-miku.jpg'
$resources += "/resource:$mikuPicture,Miku.original-miku.jpg"
$oauthResource = $null
$versionSource = [IO.Path]::GetTempFileName()
try {
    Set-Content -LiteralPath $versionSource -Encoding ascii -Value @(
        "[assembly: System.Reflection.AssemblyVersionAttribute(`"$Version.0`")]"
        "[assembly: System.Reflection.AssemblyFileVersionAttribute(`"$Version.0`")]"
        "[assembly: System.Reflection.AssemblyInformationalVersionAttribute(`"$Version`")]"
    )
    if (-not [string]::IsNullOrWhiteSpace($clientId)) {
        $clientId = $clientId.Trim()
        $clientSecret = $clientSecret.Trim()
        if ($clientId -notmatch '^\S+\.apps\.googleusercontent\.com$' -or $clientSecret -match '\s') {
            throw 'Supply valid Google Desktop OAuth client credentials.'
        }
        $oauthResource = [IO.Path]::GetTempFileName()
        @{ ClientId = $clientId; ClientSecret = $clientSecret } | ConvertTo-Json -Compress | Set-Content -LiteralPath $oauthResource -Encoding utf8
        $resources += "/resource:$oauthResource,GoogleOAuthClient,private"
    }
    & $compiler /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 "/win32manifest:$manifest" "/win32icon:$icon" "/out:$file" $resources $references $sources $versionSource
    if ($LASTEXITCODE -ne 0) { throw "Build failed with code $LASTEXITCODE" }
} finally {
    Remove-Item -LiteralPath $versionSource -ErrorAction SilentlyContinue
    if ($null -ne $oauthResource) { Remove-Item -LiteralPath $oauthResource -ErrorAction SilentlyContinue }
}
Get-Item -LiteralPath $file

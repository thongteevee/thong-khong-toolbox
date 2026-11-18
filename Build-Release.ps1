param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,
    [string]$OutputDirectory,
    [string]$InnoCompiler
)

$ErrorActionPreference = 'Stop'
if ($env:TOOLBOX_GOOGLE_CLIENT_ID -or $env:TOOLBOX_GOOGLE_CLIENT_SECRET) {
    throw 'Clear both Toolbox Google client environment variables before making public packages.'
}
if (-not $InnoCompiler) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        $InnoCompiler = $command.Source
    } else {
        $InnoCompiler = @(
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
            "$env:ProgramFiles\Inno Setup 7\ISCC.exe"
            "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
            "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe"
        ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
}
if (-not $InnoCompiler -or -not (Test-Path -LiteralPath $InnoCompiler -PathType Leaf)) {
    throw 'Install Inno Setup or pass the path to ISCC.exe with -InnoCompiler.'
}
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot "dist\v$Version" }
$output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
$portable = & (Join-Path $PSScriptRoot 'Build.ps1') -OutputDirectory $output -Version $Version

$script = Join-Path $PSScriptRoot 'installer\Toolbox.iss'
& $InnoCompiler "/DToolboxVersion=$Version" "/DToolboxExe=$($portable.FullName)" "/O$output" $script | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Installer build failed with code $LASTEXITCODE." }

$installer = Get-Item -LiteralPath (Join-Path $output 'ThongKhongToolboxSetup.exe')
$checksums = Join-Path $output 'SHA256SUMS.txt'
@($portable, $installer) | ForEach-Object {
    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($_.Name)"
} | Set-Content -LiteralPath $checksums -Encoding ascii
Get-Item -LiteralPath $portable.FullName, $installer.FullName, $checksums

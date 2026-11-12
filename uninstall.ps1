# Stop EgoGuard, remove it from the Startup folder, and delete this tool's files.
# Other files in this folder are left untouched.
# Usage: powershell -ExecutionPolicy Bypass -File uninstall.ps1
# -KeepFiles only stops EgoGuard and removes the Startup entry; install.ps1 uses it before upgrading.
param([switch]$KeepFiles)
$ErrorActionPreference = 'Stop'
$assembly = Join-Path $PSScriptRoot 'EgoGuard.dll'
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
$watchers = @(Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" |
    Where-Object { $_.CommandLine -and $_.CommandLine.Contains($assembly) })
if ($watchers.Count -gt 0) {
    & $dotnet $assembly --stop
    foreach ($watcher in $watchers) {
        $process = Get-Process -Id $watcher.ProcessId -ErrorAction SilentlyContinue
        if ($process -and -not $process.WaitForExit(10000)) { Stop-Process -Id $process.Id }
    }
}
$lnk = Join-Path ([Environment]::GetFolderPath('Startup')) 'EgoGuard.lnk'
if (Test-Path $lnk) { Remove-Item $lnk }
if ($KeepFiles) { Write-Host 'Stopped and removed from Startup.'; return }

$files = 'EgoGuard.cs', 'EgoGuard.dll', 'EgoGuard.runtimeconfig.json', 'close-user-tabs.mjs',
    'start-hidden.vbs', 'install.ps1', 'uninstall.ps1', 'guard.log', 'guard-request.json',
    'README.md', 'README.zh-CN.md', 'LICENSE', '.gitignore'
foreach ($name in $files) {
    $path = Join-Path $PSScriptRoot $name
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
}
Write-Host "Uninstalled. Tool files removed from $PSScriptRoot"

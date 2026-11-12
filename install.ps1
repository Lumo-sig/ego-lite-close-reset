# Build the event watcher, register hidden logon startup, and start it now.
# Usage: powershell -ExecutionPolicy Bypass -File install.ps1
$ErrorActionPreference = 'Stop'
$dir = $PSScriptRoot
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { throw '.NET 10 runtime is required.' }
if (-not ((& $dotnet --list-runtimes) -match '^Microsoft.NETCore.App 10\.0\.')) { throw '.NET 10 runtime is required.' }

# Compile and check the candidate before stopping the installed watcher.
$build = Join-Path $env:TEMP 'EgoGuard-build'
[void][IO.Directory]::CreateDirectory($build)
$candidate = Join-Path $build 'EgoGuard.dll'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /optimize+ ('/out:' + $candidate) (Join-Path $dir 'EgoGuard.cs')
if ($LASTEXITCODE -ne 0) { throw 'EgoGuard compilation failed.' }
Copy-Item -LiteralPath (Join-Path $dir 'EgoGuard.runtimeconfig.json') -Destination $build -Force
& $dotnet $candidate --probe
if ($LASTEXITCODE -ne 0) { throw 'EgoGuard runtime check failed.' }

$launcher = Join-Path $dir 'start-hidden.vbs'
$wscript = Join-Path $env:WINDIR 'System32\wscript.exe'
& (Join-Path $dir 'uninstall.ps1') -KeepFiles
Copy-Item -LiteralPath $candidate -Destination (Join-Path $dir 'EgoGuard.dll') -Force

$lnk = Join-Path ([Environment]::GetFolderPath('Startup')) 'EgoGuard.lnk'
$sh = New-Object -ComObject WScript.Shell
$s = $sh.CreateShortcut($lnk)
$s.TargetPath = $wscript
$s.Arguments = '//B //Nologo "' + $launcher + '"'
$s.WorkingDirectory = $dir
$s.Description = 'ego lite: reset user tabs on the last window close event'
$s.Save()

$start = New-Object Diagnostics.ProcessStartInfo $wscript, $s.Arguments
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
[void][Diagnostics.Process]::Start($start)
Write-Host "Installed and started. Startup shortcut: $lnk"

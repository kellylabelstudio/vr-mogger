# Runs only on an isolated GitHub Actions Windows machine. Never runs as part of a local build.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'Installer smoke tests require an isolated GitHub Actions runner.' }
$repoPath = Split-Path $PSScriptRoot -Parent
$installPath = Join-Path $env:RUNNER_TEMP ('VRMogger-install-test-' + [guid]::NewGuid().ToString('N'))
$setupPath = Join-Path $repoPath 'dist\VRMogger-1.1.0-beta.1-Setup.exe'
$base32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, [Microsoft.Win32.RegistryView]::Registry32)
$uninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\VRMogger'
$runKey = 'Software\Microsoft\Windows\CurrentVersion\Run'
$existingInstall = $base32.OpenSubKey($uninstallKey)
if ($null -ne $existingInstall) { $existingInstall.Dispose(); throw 'Refusing to overwrite an existing VR Mogger installation.' }
$existingRun = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($runKey)
if ($null -ne $existingRun) {
    try { if ($null -ne $existingRun.GetValue('VRMogger')) { throw 'Refusing to overwrite an existing VR Mogger startup entry.' } }
    finally { $existingRun.Dispose() }
}
$dataPath = Join-Path $env:APPDATA 'VRMogger'
$marker = Join-Path $dataPath 'installer-smoke-marker.txt'
New-Item $dataPath -ItemType Directory -Force | Out-Null
Set-Content $marker 'Retain user settings and recovery data on uninstall.'
try {
    $install = Start-Process $setupPath -ArgumentList "/S /D=$installPath" -Wait -PassThru
    if ($install.ExitCode -ne 0) { throw "Installer exited $($install.ExitCode)." }
    $installedExe = Join-Path $installPath 'VRMogger.exe'
    if (-not (Test-Path $installedExe)) { throw 'Installed executable missing.' }
    if ((Get-FileHash $installedExe).Hash -ne (Get-FileHash (Join-Path $repoPath 'bin\VRMogger.exe')).Hash) { throw 'Installed executable differs from the tested build.' }
    $registered = $base32.OpenSubKey($uninstallKey)
    if ($null -eq $registered) { throw 'Per-user uninstall registration missing.' }
    try {
        if ($registered.GetValue('InstallLocation') -ne $installPath) { throw 'InstallLocation does not match the chosen directory.' }
        if ($registered.GetValue('UninstallString') -ne ('"' + (Join-Path $installPath 'Uninstall.exe') + '"')) { throw 'The registered uninstall command is not correctly quoted.' }
    }
    finally { $registered.Dispose() }
    # Use the legacy switch to verify upgrades also remove their own startup entry.
    $startup = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($runKey)
    try { $startup.SetValue('VRMogger', ('"' + $installedExe + '" /minimized')) }
    finally { $startup.Dispose() }
    $uninstall = Start-Process (Join-Path $installPath 'Uninstall.exe') -ArgumentList '/S' -Wait -PassThru
    if ($uninstall.ExitCode -ne 0) { throw "Uninstaller exited $($uninstall.ExitCode)." }
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ((Test-Path $installedExe) -and $watch.Elapsed.TotalSeconds -lt 15) { Start-Sleep -Milliseconds 200 }
    if (Test-Path $installedExe) { throw 'Uninstaller did not remove the program.' }
    $remaining = $base32.OpenSubKey($uninstallKey)
    if ($null -ne $remaining) { $remaining.Dispose(); throw 'Uninstall registration was not removed.' }
    $startup = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($runKey)
    if ($null -ne $startup) {
        try { if ($null -ne $startup.GetValue('VRMogger')) { throw 'The matching startup entry was not removed.' } }
        finally { $startup.Dispose() }
    }
    if (-not (Test-Path $marker)) { throw 'Uninstall unexpectedly deleted AppData settings.' }
    'PASS Installer: per-user install, exact executable, uninstall registration, startup removal, and retained AppData.' | Tee-Object -FilePath (Join-Path $repoPath 'bin\installer-test-results.txt')
}
finally {
    $base32.Dispose()
    if (Test-Path $marker) { Remove-Item $marker -Force }
    if (Test-Path $installPath) { Remove-Item $installPath -Recurse -Force }
}

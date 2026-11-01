param([switch]$Installer)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectPath = $PSScriptRoot
$outputPath = Join-Path $projectPath 'bin'
$packagePath = Join-Path $projectPath 'dist'
$version = '1.1.0-beta.1'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $compilerPath)) { $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $compilerPath)) { throw 'The Windows .NET Framework C# compiler was not found.' }
New-Item $outputPath, $packagePath -ItemType Directory -Force | Out-Null

& $compilerPath /nologo /target:exe /optimize+ /warn:4 /warnaserror+ "/out:$outputPath\GenerateIcon.exe" /reference:System.Drawing.dll "$projectPath\tools\GenerateIcon.cs"
if ($LASTEXITCODE -ne 0) { throw 'Icon builder compilation failed.' }
& "$outputPath\GenerateIcon.exe" "$outputPath\App.ico"
if ($LASTEXITCODE -ne 0) { throw 'Icon generation failed.' }

$sources = @(Get-ChildItem (Join-Path $projectPath 'src') -Filter '*.cs' | Sort-Object Name | ForEach-Object { $_.FullName })
& $compilerPath /nologo /target:winexe /platform:anycpu /optimize+ /warn:4 /warnaserror+ "/out:$outputPath\VRMogger.exe" "/win32icon:$outputPath\App.ico" "/win32manifest:$projectPath\src\app.manifest" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'VR Mogger compilation failed.' }
& $compilerPath /nologo /target:exe /platform:anycpu /optimize+ /warn:4 /warnaserror+ "/out:$outputPath\VRMogger.Tests.exe" "/reference:$outputPath\VRMogger.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "$projectPath\tests\SessionTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
$env:VRMOGGER_TEST_OUTPUT = $outputPath
& "$outputPath\VRMogger.Tests.exe" 2>&1 | Tee-Object -FilePath "$outputPath\test-results.txt"
if ($LASTEXITCODE -ne 0) { throw 'Tests failed. No beta package was produced.' }

$portablePath = Join-Path $outputPath "VRMogger-$version-portable"
New-Item $portablePath -ItemType Directory -Force | Out-Null
Copy-Item "$outputPath\VRMogger.exe", "$projectPath\LICENSE", "$projectPath\README.md", "$projectPath\START-HERE.txt", "$projectPath\CHANGELOG.md" $portablePath -Force
Compress-Archive -Path "$portablePath\*" -DestinationPath "$packagePath\VRMogger-$version-portable.zip" -Force
Copy-Item "$outputPath\VRMogger.exe" "$packagePath\VRMogger.exe" -Force
if ($Installer) {
    $nsisPath = Join-Path ${env:ProgramFiles(x86)} 'NSIS\makensis.exe'
    if (-not (Test-Path $nsisPath)) {
        $nsisCommand = Get-Command makensis.exe -ErrorAction SilentlyContinue
        if ($null -eq $nsisCommand) { throw 'NSIS is required only to build the installer. Install NSIS, then rerun with -Installer.' }
        $nsisPath = $nsisCommand.Source
    }
    & $nsisPath /V3 /WX "$projectPath\installer\VRMogger.nsi"
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
}
Get-ChildItem $packagePath -File | Where-Object { $_.Extension -in '.exe', '.zip' } | Sort-Object Name | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($_.Name)"
} | Set-Content "$packagePath\SHA256SUMS.txt" -Encoding ascii
Write-Host "Beta packages: $packagePath"

# Publishes the Release build of Akari Dash and compiles the Inno Setup installer from it.
# The version comes from the App project's <Version>, so the installer and the app always agree.
#
#   powershell -ExecutionPolicy Bypass -File installer\Build-Installer.ps1
#
# Output: artifacts\installer\AkariDash-Setup-<version>.exe

#requires -Version 5.1

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\AkariDash.App\AkariDash.App.csproj'
$publish = Join-Path $root 'artifacts\publish'
$script = Join-Path $PSScriptRoot 'AkariDash.iss'

$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No <Version> in $project" }

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 (ISCC.exe) is not installed.' }

# A clean folder, so nothing from an older build ends up in the installer.
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

Write-Host "Publishing Akari Dash $version (Release, x64)..."
# Every project's Release output goes first, so a stale DLL from an earlier build can never be
# packaged. (Passing -t:Rebuild to publish instead leaves the app's .pri out, and without it the
# app cannot load its pages.)
Get-ChildItem (Join-Path $root 'src') -Directory | ForEach-Object {
    foreach ($folder in 'bin\x64\Release', 'obj\x64\Release') {
        $path = Join-Path $_.FullName $folder
        if (Test-Path $path) { Remove-Item $path -Recurse -Force }
    }
}

dotnet publish $project -c Release -p:Platform=x64 -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

foreach ($file in 'Akari Dash.exe', 'Akari Dash.pri', 'Assets\img.png') {
    if (-not (Test-Path (Join-Path $publish $file))) { throw "The publish folder has no $file." }
}

Write-Host "Compiling the installer..."
& $iscc "/DAppVersion=$version" $script
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }

Write-Host "Done: $(Join-Path $root "artifacts\installer\AkariDash-Setup-$version.exe")"

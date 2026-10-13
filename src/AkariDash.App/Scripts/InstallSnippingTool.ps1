# Snipping Tool (Windows 10): install. From AkariOS-Ultimate's bloatware Options action 'Install Snipping Tool' (Tweaks/Windows.ps1, MIT,
# Copyright (c) FR33THY, Copyright (c) 2026 isleap9), unchanged but for the console output and the internet check.

# download w10 snipping tool
IWR "https://github.com/isleap9/AkariOS-Files/releases/download/Files/snippingtool.exe" -OutFile "$env:SystemRoot\Temp\snippingtool.exe"

# install w10 snipping tool
cmd /c "$env:SystemRoot\Temp\snippingtool.exe >nul 2>&1"

# install w11 snipping tool
Get-AppXPackage -AllUsers *Microsoft.ScreenSketch* | Foreach {Add-AppxPackage -DisableDevelopmentMode -Register -ErrorAction SilentlyContinue "$($_.InstallLocation)\AppXManifest.xml"}

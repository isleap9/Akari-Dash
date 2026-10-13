# Remote Desktop Connection: install. From AkariOS-Ultimate's bloatware Options action 'Install Remote Desktop Connection' (Tweaks/Windows.ps1, MIT,
# Copyright (c) FR33THY, Copyright (c) 2026 isleap9), unchanged but for the console output and the internet check.

# download remote desktop connection
IWR "https://github.com/isleap9/AkariOS-Files/releases/download/Files/remotedesktopconnection.exe" -OutFile "$env:SystemRoot\Temp\remotedesktopconnection.exe"

# install remote desktop connection
cmd /c "$env:SystemRoot\Temp\remotedesktopconnection.exe >nul 2>&1"

# Microsoft Edge and WebView2: install. From AkariOS-Ultimate's 'edge-webview' tweak revert (Tweaks/Windows.ps1,
# MIT, Copyright (c) FR33THY, Copyright (c) 2026 isleap9): its two installers, from the AkariOS-Files release.
# The Edge settings import that follows them there is the separate 'Edge settings' tweak, so it is not here.


# stop edge running
$stop = "backgroundTaskHost", "Copilot", "CrossDeviceResume", "GameBar", "MicrosoftEdgeUpdate", "msedge", "msedgewebview2", "OneDrive", "OneDrive.Sync.Service", "OneDriveStandaloneUpdater", "Resume", "RuntimeBroker", "Search", "SearchHost", "Setup", "StoreDesktopExtension", "WidgetService", "Widgets"
$stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
Get-Process | Where-Object { $_.ProcessName -like "*edge*" } | Stop-Process -Force -ErrorAction SilentlyContinue

# download edge installer
IWR "https://github.com/isleap9/AkariOS-Files/releases/download/Files/edge.exe" -OutFile "$env:SystemRoot\Temp\edge.exe"

# start edge installer
Start-Process -Wait "$env:SystemRoot\Temp\edge.exe"

# stop edge running
$stop = "backgroundTaskHost", "Copilot", "CrossDeviceResume", "GameBar", "MicrosoftEdgeUpdate", "msedge", "msedgewebview2", "OneDrive", "OneDrive.Sync.Service", "OneDriveStandaloneUpdater", "Resume", "RuntimeBroker", "Search", "SearchHost", "Setup", "StoreDesktopExtension", "WidgetService", "Widgets"
$stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
Get-Process | Where-Object { $_.ProcessName -like "*edge*" } | Stop-Process -Force -ErrorAction SilentlyContinue

# download edge webview installer
IWR "https://github.com/isleap9/AkariOS-Files/releases/download/Files/edgewebview.exe" -OutFile "$env:SystemRoot\Temp\edgewebview.exe"

# start edge webview installer
Start-Process -Wait "$env:SystemRoot\Temp\edgewebview.exe"

# stop edge running
$stop = "backgroundTaskHost", "Copilot", "CrossDeviceResume", "GameBar", "MicrosoftEdgeUpdate", "msedge", "msedgewebview2", "OneDrive", "OneDrive.Sync.Service", "OneDriveStandaloneUpdater", "Resume", "RuntimeBroker", "Search", "SearchHost", "Setup", "StoreDesktopExtension", "WidgetService", "Widgets"
$stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
Get-Process | Where-Object { $_.ProcessName -like "*edge*" } | Stop-Process -Force -ErrorAction SilentlyContinue


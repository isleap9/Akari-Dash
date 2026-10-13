# Microsoft Edge: install. From AkariOS-Ultimate's 'edge-webview' tweak revert (Tweaks/Windows.ps1, MIT,
# Copyright (c) FR33THY, Copyright (c) 2026 isleap9): its Edge installer, unchanged. WebView2's is InstallWebView.ps1.

$stop = "backgroundTaskHost", "Copilot", "CrossDeviceResume", "GameBar", "MicrosoftEdgeUpdate", "msedge", "msedgewebview2", "OneDrive", "OneDrive.Sync.Service", "OneDriveStandaloneUpdater", "Resume", "RuntimeBroker", "Search", "SearchHost", "Setup", "StoreDesktopExtension", "WidgetService", "Widgets"
$stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
Get-Process | Where-Object { $_.ProcessName -like "*edge*" } | Stop-Process -Force -ErrorAction SilentlyContinue

# download edge installer
IWR "https://github.com/isleap9/AkariOS-Files/releases/download/Files/edge.exe" -OutFile "$env:SystemRoot\Temp\edge.exe"

# start edge installer
Start-Process -Wait "$env:SystemRoot\Temp\edge.exe"


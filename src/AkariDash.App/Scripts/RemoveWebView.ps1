# Microsoft Edge WebView2 Runtime: uninstall. The WebView2 half of AkariOS-Ultimate's 'edge-webview' tweak
# (Tweaks/Windows.ps1, MIT, Copyright (c) FR33THY, Copyright (c) 2026 isleap9). That tweak takes WebView2
# away by deleting its folder and its uninstall entry along with Edge's. Here WebView2 is its own
# Uninstaller row, so its own setup runs first, forced the way the tweak forces Edge's.

# stop webview running
Get-Process | Where-Object { $_.ProcessName -like "*msedgewebview2*" } | Stop-Process -Force -ErrorAction SilentlyContinue

# find webview uninstall string, and uninstall webview
$regview = [Microsoft.Win32.RegistryView]::Registry32
$uninstallregkey = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $regview).
OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView")
if ($uninstallregkey) {
$uninstallstring = $uninstallregkey.GetValue("UninstallString") + " --force-uninstall"
Start-Process cmd.exe "/c $uninstallstring" -WindowStyle Hidden -Wait
}

# remove edgewebview uninstaller
cmd /c "reg delete `"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView`" /f >nul 2>&1"

# remove webview folder
Remove-Item -Recurse -Force "$env:SystemDrive\Program Files (x86)\Microsoft\EdgeWebView" -ErrorAction SilentlyContinue | Out-Null

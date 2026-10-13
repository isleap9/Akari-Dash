# Snipping Tool (Windows 10): uninstall. From AkariOS-Ultimate's bloatware tweak (Tweaks/Windows.ps1, MIT, Copyright (c) FR33THY,
# Copyright (c) 2026 isleap9): its Legacy Apps removal, unchanged.

# windows 10 uninstall old snipping tool
try {
Start-Process "C:\Windows\System32\SnippingTool.exe" -ArgumentList "/Uninstall" -ErrorAction SilentlyContinue
} catch { }
# silent window for uninstall old snipping tool
$processExists = Get-Process -Name SnippingTool -ErrorAction SilentlyContinue
if ($processExists) {
$running = $true
$timeout = 0
do {
$snipProcess = Get-Process -Name SnippingTool -ErrorAction SilentlyContinue
if ($snipProcess -and $snipProcess.MainWindowHandle -ne 0) {
Stop-Process -Force -Name SnippingTool -ErrorAction SilentlyContinue | Out-Null
$running = $false
}
Start-Sleep -Milliseconds 100
$timeout++
if ($timeout -gt 100) {
Stop-Process -Name SnippingTool -Force -ErrorAction SilentlyContinue
$running = $false
}
} while ($running)
}
Start-Sleep -Seconds 1

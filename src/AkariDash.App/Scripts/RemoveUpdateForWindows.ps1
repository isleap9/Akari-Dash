# Update for x64-based Windows Systems (Windows 10): uninstall. From AkariOS-Ultimate's bloatware tweak (Tweaks/Windows.ps1, MIT, Copyright (c) FR33THY,
# Copyright (c) 2026 isleap9): its Legacy Apps removal, unchanged.

# windows 10 uninstall update for windows 10 for x64-based systems
$findupdateforwindows = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
$updateforwindows = Get-ItemProperty $findupdateforwindows -ErrorAction SilentlyContinue |
Where-Object { $_.DisplayName -like "*Update for x64-based Windows Systems*" }
if ($updateforwindows) {
$guid = $updateforwindows.PSChildName
Start-Process "msiexec.exe" -ArgumentList "/x $guid /qn /norestart" -Wait -NoNewWindow
}

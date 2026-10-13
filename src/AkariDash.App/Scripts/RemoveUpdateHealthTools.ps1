# Microsoft Update Health Tools (Windows 10): uninstall. From AkariOS-Ultimate's bloatware tweak (Tweaks/Windows.ps1, MIT, Copyright (c) FR33THY,
# Copyright (c) 2026 isleap9): its Legacy Apps removal, unchanged.

# windows 10 uninstall microsoft update health tools
$findupdatehealthtools = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
$updatehealthtools = Get-ItemProperty $findupdatehealthtools -ErrorAction SilentlyContinue |
Where-Object { $_.DisplayName -like "*Microsoft Update Health Tools*" }
if ($updatehealthtools) {
$guid = $updatehealthtools.PSChildName
Start-Process "msiexec.exe" -ArgumentList "/x $guid /qn /norestart" -Wait -NoNewWindow
}
cmd /c "reg delete `"HKLM\SYSTEM\ControlSet001\Services\uhssvc`" /f >nul 2>&1"
Unregister-ScheduledTask -TaskName PLUGScheduler -Confirm:$false -ErrorAction SilentlyContinue | Out-Null

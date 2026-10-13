# Braille (brlapi): uninstall. From AkariOS-Ultimate's bloatware tweak (Tweaks/Windows.ps1, MIT, Copyright (c) FR33THY,
# Copyright (c) 2026 isleap9): its Legacy Apps removal, unchanged.

# uninstall brlapi
cmd /c "sc stop `"brlapi`" >nul 2>&1"
cmd /c "sc delete `"brlapi`" >nul 2>&1"
cmd /c "takeown /f `"$env:SystemRoot\brltty`" /r /d y >nul 2>&1"
cmd /c "icacls `"$env:SystemRoot\brltty`" /grant *S-1-5-32-544:F /t >nul 2>&1"
Remove-Item "$env:SystemRoot\brltty" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null

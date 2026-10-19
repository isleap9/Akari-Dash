Write-Host "Intel: Debloating and installing driver..."
# download 7zip
IWR "https://github.com/isleap9/AkariOS-Files/releases/download/Files/7zip.exe" -OutFile "$env:SystemRoot\Temp\7zip.exe"
Start-Process -Wait "$env:SystemRoot\Temp\7zip.exe" -ArgumentList "/S"
cmd /c "reg add `"HKEY_CURRENT_USER\Software\7-Zip\Options`" /v `"ContextMenu`" /t REG_DWORD /d `"259`" /f >nul 2>&1"
cmd /c "reg add `"HKEY_CURRENT_USER\Software\7-Zip\Options`" /v `"CascadedMenu`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip\7-Zip File Manager.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
Write-Host "Select downloaded Intel driver..."
$InstallFile = Get-FilePath "Intel Driver (*.exe)|*.exe|All Files (*.*)|*.*"
if (-not $InstallFile) { Write-Log 'Cancelled'; return }
Write-Host "Debloating driver..."
& "$env:SystemDrive\Program Files\7-Zip\7z.exe" x "$InstallFile" -o"$env:SystemDrive\inteldriver" -y | Out-Null
Write-Host "Installing driver..."
Start-Process "cmd.exe" -ArgumentList "/c `"$env:SystemDrive\inteldriver\Installer.exe`" -f --noExtras --terminateProcesses -s" -WindowStyle Hidden -Wait
$IntelGraphicsSoftware = Get-ChildItem "$env:SystemDrive\inteldriver\Resources\Extras\IntelGraphicsSoftware_*.exe" | Select-Object -First 1 -ExpandProperty Name
if ($IntelGraphicsSoftware) { Start-Process "$env:SystemDrive\inteldriver\Resources\Extras\$IntelGraphicsSoftware" -ArgumentList "/s" -Wait -NoNewWindow }
$WshShell = New-Object -comObject WScript.Shell
$Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
$Shortcut = $WshShell.CreateShortcut("$Desktop\Intel Graphics Software.lnk")
$Shortcut.TargetPath = "$env:SystemDrive\Program Files\Intel\Intel Graphics Software\IntelGraphicsSoftware.exe"
$Shortcut.WorkingDirectory = "$env:SystemDrive\Program Files\Intel\Intel Graphics Software"
$Shortcut.Save()
$FileName = "Intel$([char]0xAE) Graphics Software"
cmd /c "reg delete `"HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run`" /v `"$FileName`" /f >nul 2>&1"
cmd /c "sc stop `"IntelGFXFWupdateTool`" >nul 2>&1"
cmd /c "sc delete `"IntelGFXFWupdateTool`" >nul 2>&1"
cmd /c "sc stop `"cplspcon`" >nul 2>&1"
cmd /c "sc delete `"cplspcon`" >nul 2>&1"
cmd /c "sc stop `"CtaChildDriver`" >nul 2>&1"
cmd /c "sc delete `"CtaChildDriver`" >nul 2>&1"
cmd /c "sc stop `"GSCAuxDriver`" >nul 2>&1"
cmd /c "sc delete `"GSCAuxDriver`" >nul 2>&1"
cmd /c "sc stop `"GSCx64`" >nul 2>&1"
cmd /c "sc delete `"GSCx64`" >nul 2>&1"
$stop = "IntelGraphicsSoftware", "PresentMonService"
$stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
Start-Sleep -Seconds 2
Remove-Item "$env:SystemDrive\Program Files\Intel\Intel Graphics Software\PresentMonService.exe" -Force -ErrorAction SilentlyContinue | Out-Null
Remove-Item "$InstallFile" -Force -ErrorAction SilentlyContinue | Out-Null
$FileName = "Intel$([char]0xAE) Graphics Software"
Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Intel\Intel Graphics Software\$FileName.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue
Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Intel" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$env:SystemDrive\Intel" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
Remove-Item "$env:SystemDrive\inteldriver" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
Write-Host "Importing Intel settings..."
$basePath = "HKLM:\System\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"
$adapterKeys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue
foreach ($key in $adapterKeys) { if ($key.PSChildName -match '^\d{4}$') { $regPath = $key.Name; cmd /c "reg add `"$regPath\3DKeys`" /f >nul 2>&1" } }
$basePath = "HKLM:\System\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"
$allKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue
$optionKeys = $allKeys | Where-Object { $_.PSChildName -eq "3DKeys" }
foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"Global_AsyncFlipMode`" /t REG_DWORD /d `"2`" /f >nul 2>&1" }
$basePath = "HKLM:\System\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"
$allKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue
$optionKeys = $allKeys | Where-Object { $_.PSChildName -eq "3DKeys" }
foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"Global_LowLatency`" /t REG_DWORD /d `"1`" /f >nul 2>&1" }
cmd /c "reg add `"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences`" /v `"DirectXUserGlobalSettings`" /t REG_SZ /d `"SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;`" /f >nul 2>&1"
Write-Host "Intel driver debloated, installed, and settings imported."
        

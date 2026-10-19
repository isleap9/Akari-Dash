Write-Host "Intel: Opening driver download page..."
Start-Process "https://www.intel.com/content/www/us/en/search.html#sortCriteria=%40lastmodifieddt%20descending&f-operatingsystem_en=Windows%2011%20Family*&f-downloadtype=Drivers&cf-tabfilter=Downloads&cf-downloadsppth=Graphics"
Start-Sleep -Seconds 5
Write-Host "Select downloaded driver..."
$InstallFile = Get-FilePath "Intel Driver Setup (*.exe)|*.exe|All Files (*.*)|*.*"
if (-not $InstallFile) { Write-Log 'Cancelled'; return }
Write-Host "Installing driver..."
Start-Process -Wait "$InstallFile"
$WshShell = New-Object -comObject WScript.Shell
$Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
$Shortcut = $WshShell.CreateShortcut("$Desktop\Intel Graphics Software.lnk")
$Shortcut.TargetPath = "$env:SystemDrive\Program Files\Intel\Intel Graphics Software\IntelGraphicsSoftware.exe"
$Shortcut.WorkingDirectory = "$env:SystemDrive\Program Files\Intel\Intel Graphics Software"
$Shortcut.Save()
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
Write-Host "Intel driver installed and settings imported."
        

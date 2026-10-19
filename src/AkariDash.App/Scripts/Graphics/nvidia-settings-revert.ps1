if (-not (Test-Connection -ComputerName '8.8.8.8' -Count 1 -Quiet -ErrorAction SilentlyContinue)) { Write-Log 'Internet connection required'; return }
# download nvidia control panel
IWR "https://github.com/isleap9/AkariOS-Files/releases/download/Files/nvp.appx" -OutFile "$env:SystemRoot\Temp\nvp.appx"

# install nvidia control panel
Add-AppxPackage -Path "$env:SystemRoot\Temp\nvp.appx" -ErrorAction SilentlyContinue | Out-Null

# create desktop shortcut
$WshShell = New-Object -comObject WScript.Shell
$Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
$Shortcut = $WshShell.CreateShortcut("$Desktop\NVIDIA Control Panel.lnk")
$Shortcut.TargetPath = "shell:appsFolder\NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIACorp.NVIDIAControlPanel"
$Shortcut.WorkingDirectory = "shell:appsFolder"
$Shortcut.Save()
# unblock drs files
$path = "C:\ProgramData\NVIDIA Corporation\Drs"
Get-ChildItem -Path $path -Recurse | Unblock-File

# revert set physx to gpu
cmd /c "reg delete `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"NvCplPhysxAuto`" /f >nul 2>&1"

# revert enable developer settings
cmd /c "reg delete `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"NvDevToolsVisible`" /f >nul 2>&1"

# revert allow access to the gpu performance counters to all users
$subkeys = Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue
foreach($key in $subkeys){
if ($key -notlike '*Configuration'){
cmd /c "reg delete `"$key`" /v `"RmProfilingAdminOnly`" /f >nul 2>&1"
}
}
cmd /c "reg delete `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"RmProfilingAdminOnly`" /f >nul 2>&1"

# revert disable show notification tray icon
cmd /c "reg delete `"HKCU\Software\NVIDIA Corporation\NvTray`" /f >nul 2>&1"

# revert enable nvidia legacy sharpen
cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Services\nvlddmkm\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Services\nvlddmkm\Parameters\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Services\nvlddmkm\Parameters\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"1`" /f >nul 2>&1"

# new folder
New-Item -Path "$env:SystemDrive\Program Files (x86)\Nvidia Profile Inspector" -ItemType Directory -Force -ErrorAction SilentlyContinue | Out-Null

# download nvidia profile inspector
IWR "https://github.com/isleap9/AkariOS-Files/releases/download/Files/inspector.exe" -OutFile "$env:SystemDrive\Program Files (x86)\Nvidia Profile Inspector\Nvidia Profile Inspector.exe"

# create desktop shortcut
$WshShell = New-Object -comObject WScript.Shell
$Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
$Shortcut = $WshShell.CreateShortcut("$Desktop\Nvidia Profile Inspector.lnk")
$Shortcut.TargetPath = "$env:SystemDrive\Program Files (x86)\Nvidia Profile Inspector\Nvidia Profile Inspector.exe"
$Shortcut.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\Nvidia Profile Inspector"
$Shortcut.Save()

# create start menu shortcut
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Nvidia Profile Inspector.lnk")
$Shortcut.TargetPath = "$env:SystemDrive\Program Files (x86)\Nvidia Profile Inspector\Nvidia Profile Inspector.exe"
$Shortcut.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\Nvidia Profile Inspector"
$Shortcut.Save()

# set config for inspector
$nipfile = @'
<?xml version="1.0" encoding="utf-16"?>
<ArrayOfProfile>
  <Profile>
    <ProfileName>Base Profile</ProfileName>
    <Executeables/>
    <Settings/>
  </Profile>
</ArrayOfProfile>
'@
Set-Content -Path "$env:SystemRoot\Temp\inspector.nip" -Value $nipfile -Force

# import nip
Start-Process -Wait "$env:SystemDrive\Program Files (x86)\Nvidia Profile Inspector\Nvidia Profile Inspector.exe" -ArgumentList "-silentImport -silent $env:SystemRoot\Temp\inspector.nip"

# open nvidiacontrolpanel
Start-Process "shell:appsFolder\NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIACorp.NVIDIAControlPanel"
    

# The helpers AkariOS-Ultimate's Akari.ps1 gives every Tweak script ($Helpers), so the Graphics
# scripts beside this file run exactly as they do there. Only Write-Log differs: Akari.ps1 queues the
# line for its log pane, Akari-Dash reads it from standard output.
$ProgressPreference = 'SilentlyContinue'
function Write-Log([string]$m) { if ($m -and $m.Trim()) { [Console]::Out.WriteLine($m.TrimEnd()) } }
function Write-Host {
    param([Parameter(Position = 0, ValueFromRemainingArguments = $true)]$Object, $ForegroundColor, $BackgroundColor, [switch]$NoNewline, $Separator = ' ')
    Write-Log (($Object | ForEach-Object { "$_" }) -join $Separator)
}
function Clear-Host { }
function show-menu { }
function Pause { }
function Read-Host {
    param([string]$Prompt = '')
    if ($Prompt) { Write-Log $Prompt }
    Add-Type -AssemblyName Microsoft.VisualBasic -ErrorAction SilentlyContinue
    $result = [Microsoft.VisualBasic.Interaction]::InputBox($Prompt, 'AkariOS-Ultimate')
    Write-Log "> $result"
    if ($result -eq '') { return $null }
    return $result
}
function Get-FilePath {
    param([string]$Filter = 'All Files (*.*)|*.*')
    Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue
    $dlg = New-Object System.Windows.Forms.OpenFileDialog
    $dlg.Filter = $Filter
    $result = $dlg.ShowDialog()
    if ($result -eq [System.Windows.Forms.DialogResult]::OK) { return $dlg.FileName }
    return $null
}
function Get-FolderPath {
    param([string]$Description = 'Select a folder')
    $shell = New-Object -ComObject Shell.Application
    $folder = $shell.BrowseForFolder(0, $Description, 0x00000040)
    if ($folder) { return $folder.Self.Path }
    return $null
}

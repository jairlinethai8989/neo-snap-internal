$ErrorActionPreference = 'Stop'
$helper = Join-Path $PSScriptRoot '..\installer\setup-options.ps1'
if (-not (Test-Path $helper)) { throw 'Setup needs checked shortcut and launch options before completion.' }
. (Join-Path $PSScriptRoot '..\installer\product-profile.ps1')
. $helper
$snapzyProfile = Get-ProductProfile 'Snapzy'
$form = New-SetupOptionsForm $snapzyProfile
try {
    if (-not $form.Controls['shortcuts'].Checked -or -not $form.Controls['launch'].Checked -or -not $form.Controls['startup'].Checked) { throw 'Setup defaults must be checked.' }
    if ($form.Controls['language'].SelectedIndex -ne 0) { throw 'Snapzy setup must default to English.' }
    if ($form.Text -ne 'Snapzy Setup' -or $form.Controls['product-label'].Text -ne 'Install Snapzy for this Windows account') { throw 'Snapzy setup must start in English.' }
    $form.Controls['shortcuts'].Checked = $false
    $form.Controls['launch'].Checked = $false
    $form.Controls['language'].SelectedIndex = 1
    if ($form.Controls['shortcuts'].Checked -or $form.Controls['launch'].Checked) { throw 'Options cannot be disabled.' }
    if ($form.Controls['language'].SelectedIndex -ne 1) { throw 'Setup must allow switching to Thai.' }
    if ($form.Text -ne 'ติดตั้ง Snapzy' -or $form.Controls['product-label'].Text -ne 'ติดตั้ง Snapzy สำหรับบัญชี Windows นี้') { throw 'Switching to Thai must localize setup labels.' }
    $form.Controls['language'].SelectedIndex = 0
    if ($form.Text -ne 'Snapzy Setup') { throw 'Switching back must restore English setup labels.' }
} finally { $form.Dispose() }
$neoForm = New-SetupOptionsForm (Get-ProductProfile 'NeoSnap')
try { if ($neoForm.Controls['language'].SelectedIndex -ne 1) { throw 'Neo Snap setup must preserve its Thai default.' } }
finally { $neoForm.Dispose() }
$launch = Get-Content (Join-Path $PSScriptRoot '..\installer\launch-after-setup.ps1') -Raw
if ($launch.IndexOf('Wait-Process') -gt $launch.IndexOf('Start-Process') -or -not $launch.Contains('Wait-Process')) { throw 'Launch must wait for setup process exit.' }
$info = New-Object System.Diagnostics.ProcessStartInfo
$info.FileName = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$info.Arguments = '-NoProfile -Command "Start-Sleep -Milliseconds 900"'
$info.UseShellExecute = $false
$info.CreateNoWindow = $true
$parent = [System.Diagnostics.Process]::Start($info)
$events = [Collections.Generic.List[string]]::new()
function Start-Process {
    param($FilePath, $WorkingDirectory, $WindowStyle)
    if (-not $parent.HasExited) { throw 'App launched while setup was still running.' }
    if ($WindowStyle -ne 'Hidden') { throw 'Helper must not flash a console.' }
    $events.Add('launch')
}
try {
    & (Join-Path $PSScriptRoot '..\installer\launch-after-setup.ps1') -SetupPid $parent.Id -AppPath $helper
    if ($events.Count -ne 1) { throw 'App was not launched after setup exit.' }
} finally { $parent.Dispose(); Remove-Item Function:\Start-Process }
Write-Output 'PASS installer: checked defaults / opt-out / wait before launch'

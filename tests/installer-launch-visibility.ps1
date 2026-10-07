$ErrorActionPreference = 'Stop'
$helper = Join-Path $PSScriptRoot '..\installer\launch-after-setup.ps1'
$launches = [Collections.Generic.List[string]]::new()
function Start-Process {
    param($FilePath, $WorkingDirectory, $WindowStyle)
    if ($WindowStyle -eq 'Hidden') { throw 'The requested application window is hidden. Only the PowerShell helper should be hidden.' }
    $launches.Add('launch')
}
try {
    & $helper -SetupPid 0 -AppPath $helper
    if ($launches.Count -ne 1) { throw 'The requested app was not launched.' }
} finally { Remove-Item Function:\Start-Process }
Write-Output 'PASS installer: launch opens the requested application visibly'

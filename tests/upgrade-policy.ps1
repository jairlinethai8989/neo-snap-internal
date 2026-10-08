$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\installer\upgrade-policy.ps1')
foreach ($case in @(@('', 'Install'), @('0.1.20', 'Update'), @('0.1.21', 'Repair'), @('0.1.22', 'Block'), @('0.1.9', 'Update'))) {
    if ((Get-UpgradeAction $case[0] '0.1.21') -ne $case[1]) { throw "Incorrect upgrade decision: $($case[0])" }
}
Write-Output 'PASS new install / older update / same repair / newer blocked / numeric ordering'

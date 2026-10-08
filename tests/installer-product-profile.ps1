$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\installer\product-profile.ps1')

$neo = Get-ProductProfile 'NeoSnap'
$snapzy = Get-ProductProfile 'Snapzy'
foreach ($field in @('Name','Version','InstallFolder','DataFolder','StartupValue','UninstallKey','InstanceId','Language')) {
    if ($neo[$field] -eq $snapzy[$field]) { throw "Product profiles must isolate $field." }
}
if ($neo.Name -ne 'Neo Snap' -or $neo.Version -ne '0.1.21' -or $neo.InstallFolder -ne 'SnapCraft' -or $neo.DataFolder -ne 'SnapCraft' -or $neo.StartupValue -ne 'NeoSnap' -or $neo.UninstallKey -ne 'SnapCraft' -or $neo.Language -ne 'th') {
    throw 'Neo Snap profile must preserve the existing installation identity.'
}
if ($snapzy.Name -cne 'SnapZy' -or $snapzy.Version -ne '1.0.1' -or $snapzy.InstallFolder -ne 'Snapzy' -or $snapzy.DataFolder -ne 'Snapzy' -or $snapzy.StartupValue -ne 'Snapzy' -or $snapzy.UninstallKey -ne 'Snapzy' -or $snapzy.Language -ne 'en') {
    throw 'Snapzy profile must use its public identity and English default.'
}
if ($snapzy.IconName -ne 'Snapzy-1.0.1-A.ico' -or $neo.IconName -ne 'NeoSnap-0.1.21.ico') {
    throw 'Snapzy icon A needs a new shortcut icon path to avoid the old Windows icon cache.'
}
if ($neo.OutputFile -ne 'Neo-Snap-Windows-Setup-0.1.21.exe' -or $snapzy.OutputFile -cne 'SnapZy-Setup-1.0.1.exe' -or $neo.PublishFolder -eq $snapzy.PublishFolder -or $neo.StagingFolder -eq $snapzy.StagingFolder) {
    throw 'Release artifacts and staging directories must remain flavor-specific.'
}
if (-not [guid]::TryParse($neo.InstanceId, [ref]([guid]::Empty)) -or -not [guid]::TryParse($snapzy.InstanceId, [ref]([guid]::Empty))) {
    throw 'Both products need valid instance identifiers.'
}
Write-Output 'PASS installer product identities and side-by-side paths'

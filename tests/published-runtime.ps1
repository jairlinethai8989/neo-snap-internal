param([Parameter(Mandatory)][string]$Directory)
$ErrorActionPreference = 'Stop'
$Directory = (Resolve-Path -LiteralPath $Directory).Path
$config = Get-Content -LiteralPath (Join-Path $Directory 'SnapCraft.runtimeconfig.json') -Raw | ConvertFrom-Json
. (Join-Path $PSScriptRoot '..\installer\runtime-policy.ps1')
. (Join-Path $PSScriptRoot '..\installer\payload-install.ps1')
$requirements=Get-DotNetRequirements $config
$recorded=Get-Content -LiteralPath (Join-Path $Directory 'runtime-requirements.json') -Raw | ConvertFrom-Json
Assert-DotNetRequirements $recorded
if ($recorded.hostSearch -cne 'Global') { throw 'Apphost and setup must use the same global runtime discovery policy.' }
foreach ($name in @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App')) {
    $runtime = @($requirements.frameworks | Where-Object { $_.name -eq $name })
    $manifest=@($recorded.frameworks | Where-Object { $_.name -eq $name })
    if ($manifest.Count -ne 1 -or $runtime[0].version -ne $manifest[0].version -or [version]$runtime[0].version -lt [version]'10.0.12') { throw "$name prerequisite does not match the application." }
}
foreach ($file in @('coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll', 'createdump.exe', 'System.Private.CoreLib.dll', 'System.Windows.Forms.dll', 'PresentationFramework.dll')) {
    if (Test-Path -LiteralPath (Join-Path $Directory $file)) { throw "The installer still bundles .NET: $file" }
}
$legacy=Get-Content -LiteralPath (Join-Path $Directory 'legacy-runtime-files.json') -Raw | ConvertFrom-Json
foreach ($file in 'coreclr.dll','hostfxr.dll','hostpolicy.dll','createdump.exe','System.Windows.Forms.dll') {
    if ($legacy.files -notcontains $file) { throw "Legacy cleanup omits an owned runtime file: $file" }
}
if ($legacy.files -contains 'ScreenRecorderLib.dll') { throw 'Runtime cleanup must not own the native recording library.' }
foreach ($file in @('LICENSE-DotNet.txt', 'THIRD-PARTY-NOTICES-DotNet.txt', 'LICENSE-WindowsDesktop.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $Directory $file))) { throw "Bundled runtime license missing: $file" }
}
Read-ManagedPayloadManifest $Directory -Validate | Out-Null
Write-Output "PASS framework-dependent .NET 10 payload: matching prerequisites, no bundled runtime, managed file digests: $Directory"

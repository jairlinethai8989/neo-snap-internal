param(
    [Parameter(Mandatory)][string]$Installer,
    [Parameter(Mandatory)][ValidateSet('NeoSnap','Snapzy')][string]$ProductFlavor,
    [string]$PreviousPayload
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$Installer=(Resolve-Path -LiteralPath $Installer).Path
$directory=Join-Path ([IO.Path]::GetTempPath()) ('shared-installer-check-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory | Out-Null
$process=Start-Process -FilePath $Installer -ArgumentList @('/Q','/C',"/T:$directory") -WindowStyle Hidden -PassThru
try {
    if (-not $process.WaitForExit(120000)) { $process.Kill(); throw 'Installer extraction timed out.' }
    if ($process.ExitCode -ne 0) { throw "Installer extraction failed: $($process.ExitCode)" }
} finally { $process.Dispose() }
foreach ($file in 'runtime-policy.ps1','runtime-setup.ps1','payload-install.ps1','runtime-requirements.json','legacy-runtime-files.json','install.cmd') {
    if (-not (Test-Path -LiteralPath (Join-Path $directory $file))) { throw "Installer omitted prerequisite support: $file" }
}
$bytes=[IO.File]::ReadAllBytes((Join-Path $directory 'runtime-setup.ps1'))
if ($bytes.Length -lt 3 -or $bytes[0] -ne 0xef -or $bytes[1] -ne 0xbb -or $bytes[2] -ne 0xbf) { throw 'Bilingual prerequisite UI is not Windows PowerShell 5.1 compatible UTF-8 BOM.' }
foreach ($script in (Get-ChildItem -LiteralPath $directory -Filter '*.ps1')) {
    $tokens=$null; $errors=$null
    [Management.Automation.Language.Parser]::ParseFile($script.FullName,[ref]$tokens,[ref]$errors) | Out-Null
    if ($errors.Count) { throw "Packaged installer syntax is invalid: $($script.Name)" }
}
if (-not (Get-Content (Join-Path $directory 'install.cmd') -Raw).Contains("-ProductFlavor $ProductFlavor")) { throw 'The package launches the wrong product installer.' }
. (Join-Path $directory 'runtime-setup.ps1')
. (Join-Path $directory 'payload-install.ps1')
$payload=Expand-ManagedPayload (Join-Path $directory 'payload.zip')
& (Join-Path $root 'tests/published-runtime.ps1') -Directory $payload
foreach ($file in 'runtime-policy.ps1','runtime-setup.ps1','payload-install.ps1','runtime-requirements.json','legacy-runtime-files.json') {
    if ((Get-FileHash (Join-Path $directory $file)).Hash -ne (Get-FileHash (Join-Path $payload $file)).Hash) { throw "Installer/payload mismatch: $file" }
}
if ($PreviousPayload) {
    $previous=(Resolve-Path -LiteralPath $PreviousPayload).Path
    $target=Join-Path $directory 'upgrade-target'
    New-Item -ItemType Directory -Path $target | Out-Null
    Copy-Item -Path (Join-Path $previous '*') -Destination $target -Recurse
    if (-not (Test-Path (Join-Path $target 'coreclr.dll'))) { throw 'Upgrade fixture is not a bundled-runtime package.' }
    [IO.File]::WriteAllText((Join-Path $target 'user-note.txt'),'keep my work')
    $legacy=Get-Content (Join-Path $payload 'legacy-runtime-files.json') -Raw | ConvertFrom-Json
    Install-ManagedPayload $payload $target $legacy
    & (Join-Path $root 'tests/published-runtime.ps1') -Directory $target
    if ([IO.File]::ReadAllText((Join-Path $target 'user-note.txt')) -ne 'keep my work') { throw 'Upgrade changed an unknown user file.' }
    if (-not (Test-Path (Join-Path $target 'ScreenRecorderLib.dll'))) { throw 'Upgrade removed the native recording library.' }
    Write-Output "PASS real legacy payload upgrade / owned runtime cleanup / user file preservation / recording library: $target"
}
Write-Output "PASS actual $ProductFlavor setup extraction / prerequisite modules / hashes / framework-dependent payload: $directory; payload=$payload. No app or runtime was installed."

param([Parameter(Mandatory)][string]$RequirementsPath)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../installer/runtime-setup.ps1')
Add-Type -AssemblyName System.Windows.Forms
$requirements=Get-Content -LiteralPath $RequirementsPath -Raw | ConvertFrom-Json
$state=@{Token=[Threading.CancellationTokenSource]::new();Label=[Windows.Forms.Label]::new();Progress=[Windows.Forms.ProgressBar]::new()}
$artifact=$null
try {
    # Exercise the real download boundary without executing any installer.
    $artifact=Get-VerifiedRuntimeDownload $requirements $state 'en'
    $actual=(Get-FileHash -LiteralPath $artifact.path -Algorithm SHA512).Hash
    if ($actual -ine $artifact.hash) { throw 'Microsoft download digest mismatch.' }
    if (-not (Test-MicrosoftBinary $artifact.path)) { throw 'Microsoft signature verification failed.' }
    if ((Get-PeArchitecture $artifact.path) -ne 'x64') {
        # The Desktop Runtime installer is an x86 bootstrapper carrying x64 packages.
        if ((Get-PeArchitecture $artifact.path) -ne 'x86') { throw 'Runtime installer is not a PE executable.' }
    }
    Write-Output "PASS actual Microsoft Desktop Runtime download / SHA-512 / trusted signature; $((Get-Item -LiteralPath $artifact.path).Length) bytes. No installer was executed."
} finally {
    if ($artifact) { Remove-RuntimeTemporaryDirectory $artifact.directory }
    $state.Label.Dispose(); $state.Progress.Dispose(); $state.Token.Dispose()
}

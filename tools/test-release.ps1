param(
    [ValidateSet('Both','NeoSnap','Snapzy')][string]$ProductFlavor = 'Both',
    [switch]$Desktop,
    [string]$SnapzyInstaller,
    [string]$PublishedRoot
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $root
try {
    $flavors = if ($ProductFlavor -eq 'Both') { @('NeoSnap','Snapzy') } else { @($ProductFlavor) }
    foreach ($flavor in $flavors) {
        $output = Join-Path $root "dist\release-check\$flavor"
        dotnet build tests/SnapCraft.Tests/SnapCraft.Tests.csproj -c Release -p:Platform=x64 -p:ProductFlavor=$flavor -p:TreatWarningsAsErrors=true -o $output
        if ($LASTEXITCODE) { throw "$flavor build failed." }
        $test = Join-Path $output 'SnapCraft.Tests.exe'
        $arguments = @('--assets')
        & $test @arguments
        if ($LASTEXITCODE) { throw "$flavor native/core tests failed." }
        if ($Desktop) {
            foreach ($group in @('--fast-startup','--editor-timing','--prepared-preview','--large-preview','--pending-editor','--pending-editor-fallback','--productivity','--editor-ocr','--ocr-ui','--scroll-occluded','--desktop-capture-only','--cloaked-selection-only','--window-fast-only','--window-occluded','--video-cpu','--video-preview','--recording-preview','--editor','--projects','--launcher-controls','--exit-editors','--scroll-esc','--editor-batch')) {
                & $test --desktop-only $group
                if ($LASTEXITCODE) { throw "$flavor $group failed." }
            }
        }
    }
    foreach ($file in (Get-ChildItem tests -Filter '*.cjs' | Sort-Object Name)) {
        node $file.FullName
        if ($LASTEXITCODE) { throw "Browser test failed: $($file.Name)" }
    }
    $windowsPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    foreach ($file in (Get-ChildItem tests -Filter '*.ps1' | Sort-Object Name)) {
        if ($file.Name -in 'snapzy-package.ps1', 'published-runtime.ps1') { continue }
        & $windowsPowerShell -NoProfile -File $file.FullName
        if ($LASTEXITCODE) { throw "Installer test failed: $($file.Name)" }
    }
    if ($SnapzyInstaller) {
        & $windowsPowerShell -NoProfile -File tests/snapzy-package.ps1 -Installer $SnapzyInstaller
        if ($LASTEXITCODE) { throw 'SnapZy package verification failed.' }
    }
    if ($PublishedRoot) {
        . (Join-Path $root 'installer\product-profile.ps1')
        foreach ($flavor in $flavors) {
            $profile = Get-ProductProfile $flavor
            & $windowsPowerShell -NoProfile -File tests/published-runtime.ps1 -Directory (Join-Path $PublishedRoot $profile.PublishFolder)
            if ($LASTEXITCODE) { throw "$flavor published runtime verification failed." }
        }
    }
    Write-Output "PASS release gate: $ProductFlavor; desktop=$Desktop"
    if (-not $Desktop) { Write-Warning 'Live desktop tests were not run. Use -Desktop before releasing.' }
    if (-not $PublishedRoot) { Write-Warning 'Published runtime payloads were not checked.' }
    if (-not $SnapzyInstaller) { Write-Warning 'Setup extraction was not checked.' }
} finally { Pop-Location }

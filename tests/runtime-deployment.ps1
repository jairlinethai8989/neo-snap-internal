$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
foreach ($project in @('src/SnapCraft/SnapCraft.csproj', 'tests/SnapCraft.Tests/SnapCraft.Tests.csproj')) {
    [xml]$xml = Get-Content -LiteralPath (Join-Path $root $project) -Raw
    if ($xml.Project.PropertyGroup.TargetFramework -notcontains 'net10.0-windows10.0.19041.0') { throw "$project must target .NET 10." }
}
$output = Join-Path $root ('dist/runtime-deployment-check-' + [guid]::NewGuid().ToString('N'))
foreach ($flavor in 'NeoSnap','Snapzy') {
    & pwsh -NoProfile -File (Join-Path $root 'installer/build-installer.ps1') -ProductFlavor $flavor -OutputDirectory $output -PublishOnly
    if ($LASTEXITCODE -ne 0) { throw "Framework-dependent publish failed for $flavor." }
    . (Join-Path $root 'installer/product-profile.ps1')
    $product = Get-ProductProfile $flavor
    & (Join-Path $PSScriptRoot 'published-runtime.ps1') -Directory (Join-Path $output $product.PublishFolder)
}
Write-Output "PASS both real framework-dependent payloads: $output"

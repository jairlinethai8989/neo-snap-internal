param(
    [ValidateSet('Both','NeoSnap','Snapzy')][string]$ProductFlavor = 'Both',
    [switch]$CoreOnly
)
$ErrorActionPreference = 'Stop'
$env:APPDATA = Join-Path $PSScriptRoot '.build-profile'
$env:DOTNET_CLI_HOME = $env:APPDATA
$env:NUGET_PACKAGES = Join-Path $env:USERPROFILE '.nuget\packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
New-Item -ItemType Directory -Force -Path $env:APPDATA | Out-Null

if (-not $CoreOnly) {
    & (Join-Path $PSScriptRoot 'tools/test-release.ps1') -ProductFlavor $ProductFlavor -Desktop
    exit $LASTEXITCODE
}

dotnet restore (Join-Path $PSScriptRoot 'tests/SnapCraft.Tests/SnapCraft.Tests.csproj') --configfile (Join-Path $PSScriptRoot 'NuGet.Config') -p:Platform=x64
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$flavors = if ($ProductFlavor -eq 'Both') { @('NeoSnap','Snapzy') } else { @($ProductFlavor) }
foreach ($flavor in $flavors) {
    $output = Join-Path $PSScriptRoot "dist/core-check/$flavor"
    dotnet build (Join-Path $PSScriptRoot 'tests/SnapCraft.Tests/SnapCraft.Tests.csproj') -c Release -p:Platform=x64 -p:ProductFlavor=$flavor --no-restore -p:TreatWarningsAsErrors=true -o $output
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & (Join-Path $output 'SnapCraft.Tests.exe') --assets
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
exit 0

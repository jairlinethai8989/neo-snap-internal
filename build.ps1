$ErrorActionPreference = 'Stop'
$env:APPDATA = Join-Path $PSScriptRoot '.build-profile'
$env:DOTNET_CLI_HOME = $env:APPDATA
$env:NUGET_PACKAGES = Join-Path $env:USERPROFILE '.nuget\packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
New-Item -ItemType Directory -Force -Path $env:APPDATA | Out-Null

dotnet restore (Join-Path $PSScriptRoot 'tests/SnapCraft.Tests/SnapCraft.Tests.csproj') --configfile (Join-Path $PSScriptRoot 'NuGet.Config') -p:Platform=x64 -p:NuGetAudit=false --ignore-failed-sources
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build (Join-Path $PSScriptRoot 'src/SnapCraft/SnapCraft.csproj') -c Release -p:Platform=x64 --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build (Join-Path $PSScriptRoot 'tests/SnapCraft.Tests/SnapCraft.Tests.csproj') -c Release -p:Platform=x64 --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& (Join-Path $PSScriptRoot 'tests/SnapCraft.Tests/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/SnapCraft.Tests.exe') --assets
exit $LASTEXITCODE

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dist = Join-Path $root 'dist'
$publish = Join-Path $dist 'publish'
$staging = Join-Path $dist 'setup-staging'
$output = Join-Path $dist 'SnapCraft-Windows-Setup-0.1.0.exe'
$env:APPDATA = Join-Path $root '.build-profile'
$env:DOTNET_CLI_HOME = $env:APPDATA
$env:NUGET_PACKAGES = Join-Path $env:USERPROFILE '.nuget\packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
New-Item -ItemType Directory -Force -Path $env:APPDATA, $dist, $staging | Out-Null

$project = Join-Path $root 'src\SnapCraft\SnapCraft.csproj'
dotnet restore $project --configfile (Join-Path $root 'NuGet.Config') -p:Platform=x64 -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Package restore failed.' }
dotnet publish $project -c Release -p:Platform=x64 --no-restore --self-contained false -o $publish
if ($LASTEXITCODE -ne 0) { throw 'App publish failed.' }
Copy-Item (Join-Path $PSScriptRoot 'uninstall.ps1') -Destination $publish -Force
$libraryLicense = Join-Path $env:NUGET_PACKAGES 'screenrecorderlib\7.0.1\LICENSE'
if (Test-Path -LiteralPath $libraryLicense) {
    Copy-Item -LiteralPath $libraryLicense -Destination (Join-Path $publish 'LICENSE-ScreenRecorderLib.txt') -Force
}

$inno = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if (-not $inno) {
    $known = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
    if (Test-Path -LiteralPath $known) { $inno = Get-Item -LiteralPath $known }
}
if ($inno) {
    & $inno.FullName (Join-Path $PSScriptRoot 'SnapCraft.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed.' }
    Write-Output "Installer: $output"
    exit 0
}

$archive = Join-Path $staging 'payload.zip'
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $archive -Force
Copy-Item (Join-Path $PSScriptRoot 'install.cmd'), (Join-Path $PSScriptRoot 'install.ps1') -Destination $staging -Force
$sedPath = Join-Path $staging 'SnapCraft.sed'
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }
$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=0
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=%InstallPrompt%
DisplayLicense=%DisplayLicense%
FinishMessage=%FinishMessage%
TargetName=%TargetName%
FriendlyName=%FriendlyName%
AppLaunched=%AppLaunched%
PostInstallCmd=%PostInstallCmd%
AdminQuietInstCmd=
UserQuietInstCmd=
SourceFiles=SourceFiles
[SourceFiles]
SourceFiles0=$staging\
[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
[Strings]
InstallPrompt=Install SnapCraft Capture for Windows?
DisplayLicense=
FinishMessage=SnapCraft setup finished.
TargetName=$output
FriendlyName=SnapCraft Capture
AppLaunched=install.cmd
PostInstallCmd=<None>
FILE0=payload.zip
FILE1=install.cmd
FILE2=install.ps1
"@
[IO.File]::WriteAllText($sedPath, (($sed -split '\r?\n') -join "`r`n"), [Text.Encoding]::ASCII)
& (Join-Path $env:WINDIR 'System32\iexpress.exe') /N /Q $sedPath
$lastSize = -1
$stableChecks = 0
for ($attempt = 0; $attempt -lt 90; $attempt++) {
    Start-Sleep -Seconds 1
    $file = Get-Item -LiteralPath $output -ErrorAction SilentlyContinue
    if ($file -and $file.Length -gt 1MB -and $file.Length -eq $lastSize) { $stableChecks++ }
    else { $stableChecks = 0 }
    if ($stableChecks -ge 3) { break }
    $lastSize = if ($file) { $file.Length } else { -1 }
}
if ($stableChecks -lt 3) { throw 'IExpress did not finish creating the setup file.' }
Write-Output "Installer: $output"

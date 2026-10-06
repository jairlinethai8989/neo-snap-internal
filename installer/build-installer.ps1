param([ValidateSet('NeoSnap','Snapzy')][string]$ProductFlavor = 'NeoSnap')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'product-profile.ps1')
$product = Get-ProductProfile $ProductFlavor
$productName = $product.Name
$version = $product.Version
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dist = Join-Path $root 'dist'
$publish = Join-Path $dist $product.PublishFolder
$staging = Join-Path $dist $product.StagingFolder
$output = Join-Path $dist $product.OutputFile
$env:APPDATA = Join-Path $root '.build-profile'
$env:DOTNET_CLI_HOME = $env:APPDATA
$env:NUGET_PACKAGES = Join-Path $env:USERPROFILE '.nuget\packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
New-Item -ItemType Directory -Force -Path $env:APPDATA, $dist, $staging | Out-Null

$project = Join-Path $root 'src\SnapCraft\SnapCraft.csproj'
dotnet restore $project --configfile (Join-Path $root 'NuGet.Config') -p:Platform=x64 -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Package restore failed.' }
dotnet publish $project -c Release -p:Platform=x64 -p:ProductFlavor=$ProductFlavor --no-restore --self-contained false -o $publish
if ($LASTEXITCODE -ne 0) { throw 'App publish failed.' }
# Debug symbols and API documentation are build artifacts, not runtime dependencies.
$publish = [IO.Path]::GetFullPath($publish)
if (-not $publish.StartsWith(([IO.Path]::GetFullPath($dist).TrimEnd('\') + '\'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid publish directory.' }
Get-ChildItem -LiteralPath $publish -Recurse -File | Where-Object { $_.Extension -in '.pdb', '.xml' } | Remove-Item -Force
Copy-Item (Join-Path $PSScriptRoot 'uninstall.ps1') -Destination $publish -Force
Copy-Item (Join-Path $PSScriptRoot 'launch-after-setup.ps1') -Destination $publish -Force
Copy-Item (Join-Path $PSScriptRoot 'product-profile.ps1') -Destination $publish -Force
Copy-Item -LiteralPath (Join-Path $publish 'Assets\icons\app.ico') -Destination (Join-Path $publish (Join-Path 'Assets\icons' $product.IconName)) -Force
Copy-Item -LiteralPath (Join-Path $env:NUGET_PACKAGES 'naudio\2.2.1\license.txt') -Destination (Join-Path $publish 'LICENSE-NAudio.txt') -Force
$libraryLicense = Join-Path $env:NUGET_PACKAGES 'screenrecorderlib\7.0.1\LICENSE'
if (Test-Path -LiteralPath $libraryLicense) {
    Copy-Item -LiteralPath $libraryLicense -Destination (Join-Path $publish 'LICENSE-ScreenRecorderLib.txt') -Force
}

# Use one installer flow so upgrade checks do not depend on tools installed on the build machine.

$archive = Join-Path $staging 'payload.zip'
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $archive -Force
Copy-Item (Join-Path $PSScriptRoot 'install.ps1') -Destination $staging -Force
Copy-Item (Join-Path $PSScriptRoot 'setup-options.ps1') -Destination $staging -Force
Copy-Item (Join-Path $PSScriptRoot 'upgrade-policy.ps1') -Destination $staging -Force
Copy-Item (Join-Path $PSScriptRoot 'product-profile.ps1') -Destination $staging -Force
$installCmd = "@echo off`r`npowershell.exe -NoProfile -ExecutionPolicy Bypass -File `"%~dp0install.ps1`" -ProductFlavor $ProductFlavor`r`nexit /b %errorlevel%`r`n"
[IO.File]::WriteAllText((Join-Path $staging 'install.cmd'), $installCmd, [Text.Encoding]::ASCII)
$sedPath = Join-Path $staging "$ProductFlavor.sed"
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
%FILE3%=
%FILE4%=
%FILE5%=
[Strings]
InstallPrompt=Install $productName for Windows?
DisplayLicense=
FinishMessage=
TargetName=$output
FriendlyName=$productName
AppLaunched=install.cmd
PostInstallCmd=<None>
FILE0=payload.zip
FILE1=install.cmd
FILE2=install.ps1
FILE3=setup-options.ps1
FILE4=upgrade-policy.ps1
FILE5=product-profile.ps1
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

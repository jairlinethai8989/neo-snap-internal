param([Parameter(Mandatory)][string]$PublishedRoot)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$PublishedRoot=(Resolve-Path -LiteralPath $PublishedRoot).Path
. (Join-Path $root 'installer/product-profile.ps1')
. (Join-Path $root 'installer/runtime-setup.ps1')
$qa=Join-Path $root ('dist/shared-runtime-app-check-'+[guid]::NewGuid().ToString('N'))
$private=Join-Path $qa 'dotnet'
New-Item -ItemType Directory -Path $private -Force | Out-Null
$globalHost=Join-Path (Get-RegisteredDotNetRoot) 'dotnet.exe'
if (-not (Test-MicrosoftBinary $globalHost)) { throw 'The QA host must be a trusted Microsoft executable.' }
Copy-Item -LiteralPath $globalHost -Destination (Join-Path $private 'dotnet.exe')
foreach ($name in 'Microsoft.NETCore.App','Microsoft.WindowsDesktop.App') {
    $package=Join-Path $env:USERPROFILE ('.nuget/packages/'+$name.ToLowerInvariant()+'.runtime.win-x64/10.0.12/runtimes/win-x64')
    $shared=Join-Path $private "shared/$name/10.0.12"
    New-Item -ItemType Directory -Path $shared -Force | Out-Null
    foreach ($folder in 'native','lib/net10.0') { Copy-Item -Path (Join-Path $package "$folder/*") -Destination $shared -Recurse }
    if ($name -eq 'Microsoft.NETCore.App') {
        $fxr=Join-Path $private 'host/fxr/10.0.12'
        New-Item -ItemType Directory -Path $fxr -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $shared 'hostfxr.dll') -Destination $fxr
    }
}
$hostFile=Join-Path $private 'dotnet.exe'
$inventory=@(& $hostFile --list-runtimes)
if ($LASTEXITCODE) { throw 'Private QA runtime could not enumerate frameworks.' }
foreach ($flavor in 'NeoSnap','Snapzy') {
    $product=Get-ProductProfile $flavor
    $published=Join-Path $PublishedRoot $product.PublishFolder
    & (Join-Path $root 'tests/published-runtime.ps1') -Directory $published
    $requirements=Get-Content -LiteralPath (Join-Path $published 'runtime-requirements.json') -Raw | ConvertFrom-Json
    if (-not (Test-DotNetRuntimeInventory $requirements $inventory $private 'x64')) { throw 'Private runtime does not satisfy the actual application.' }
    $output=Join-Path $qa $flavor
    & dotnet build (Join-Path $root 'tests/SnapCraft.Tests/SnapCraft.Tests.csproj') -c Release -t:Rebuild -p:Platform=x64 -p:ProductFlavor=$flavor -p:DefaultSelfContained=false -p:SelfContained=false -p:RollForward=LatestPatch -p:AppHostDotNetSearch=Global -p:TreatWarningsAsErrors=true -o $output
    if ($LASTEXITCODE) { throw "$flavor framework-dependent harness build failed." }
    Copy-Item -LiteralPath (Join-Path $published 'SnapCraft.dll') -Destination (Join-Path $output 'SnapCraft.dll') -Force
    if ((Get-FileHash (Join-Path $published 'SnapCraft.dll')).Hash -ne (Get-FileHash (Join-Path $output 'SnapCraft.dll')).Hash) { throw 'QA did not load the actual published application assembly.' }
    $test=Join-Path $output 'SnapCraft.Tests.dll'
    & $hostFile $test --assets
    if ($LASTEXITCODE) { throw "$flavor framework-dependent core tests failed." }
    foreach ($group in '--editor','--launcher-controls') {
        & $hostFile $test --desktop-only $group
        if ($LASTEXITCODE) { throw "$flavor framework-dependent $group failed." }
    }
}
Write-Output "PASS both actual published assemblies on isolated .NET Desktop Runtime 10.0.12: $qa. No global runtime, registry or installation was changed."

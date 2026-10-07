$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '..\installer\runtime-policy.ps1')
. (Join-Path $PSScriptRoot '..\installer\payload-install.ps1')
if (-not (Get-Command Invoke-ApplicationPayload -ErrorAction SilentlyContinue)) { throw 'Installer runtime gating is missing.' }
$root=Join-Path $env:TEMP ('installer-flow-tests-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
try {
    $stage=Join-Path $root 'stage'; New-Item -ItemType Directory -Path $stage | Out-Null
    [IO.File]::WriteAllText((Join-Path $stage 'SnapCraft.exe'),'new app')
    [IO.File]::WriteAllText((Join-Path $stage 'SnapCraft.dll'),'new core')
    $frameworks=@([pscustomobject]@{name='Microsoft.NETCore.App';version='10.0.12'},[pscustomobject]@{name='Microsoft.WindowsDesktop.App';version='10.0.12'})
    $config=[pscustomobject]@{runtimeOptions=[pscustomobject]@{tfm='net10.0';rollForward='LatestPatch';frameworks=$frameworks}}
    [IO.File]::WriteAllText((Join-Path $stage 'SnapCraft.runtimeconfig.json'),($config | ConvertTo-Json -Depth 8))
    $requirements=Get-DotNetRequirements $config; $requirements | Add-Member -NotePropertyName hostSearch -NotePropertyValue 'Global'
    [IO.File]::WriteAllText((Join-Path $stage 'runtime-requirements.json'),($requirements | ConvertTo-Json -Depth 8))
    [IO.File]::WriteAllText((Join-Path $stage 'managed-files.json'),((Get-ManagedPayloadManifest $stage) | ConvertTo-Json -Depth 8))
    $archive=Join-Path $root 'payload.zip'; Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archive
    foreach ($case in 'Canceled','RestartRequired','Failure','Reopened','Ready') {
        $target=Join-Path $root $case; New-Item -ItemType Directory -Path $target | Out-Null
        [IO.File]::WriteAllText((Join-Path $target 'SnapCraft.exe'),'old app')
        [IO.File]::WriteAllText((Join-Path $target 'notes.txt'),'user work')
        $script:checked=$false; $failed=$false; $status=$null
        try {
            $status=Invoke-ApplicationPayload $archive $target ([pscustomobject]@{schema=1;files=@()}) {
                param($requirements)
                if (-not (Test-Path -LiteralPath $requirements)) { throw 'Staged prerequisite manifest was not supplied.' }
                if ($case -eq 'Failure') { throw 'Controlled runtime failure' }
                if ($case -eq 'Reopened') { return 'Ready' }; return $case
            } { $script:checked=$true; if ($case -eq 'Reopened') { throw 'Application reopened' } }
        } catch { $failed=$true }
        if ((Get-Content -LiteralPath (Join-Path $target 'notes.txt') -Raw) -ne 'user work') { throw 'Preflight damaged user work.' }
        if ($case -eq 'Ready') {
            if ($status -ne 'Ready' -or -not $script:checked -or (Get-Content -LiteralPath (Join-Path $target 'SnapCraft.exe') -Raw) -ne 'new app') { throw 'Ready prerequisite did not permit a checked upgrade.' }
        } else {
            if ((Get-Content -LiteralPath (Join-Path $target 'SnapCraft.exe') -Raw) -ne 'old app') { throw "Preflight changed application files: $case" }
            if ($case -in 'Canceled','RestartRequired' -and ($status -ne $case -or $script:checked)) { throw 'Preflight cancellation/restart handling was bypassed.' }
            if ($case -in 'Failure','Reopened' -and -not $failed) { throw 'Failed/reopened preflight was treated as ready.' }
        }
    }
    Write-Output 'PASS installer runtime gate: actual staged payload, cancel/restart/failure/reopened preserve old app, ready upgrades, user work preserved'
} finally {
    $resolved=[IO.Path]::GetFullPath($root)
    if (-not $resolved.StartsWith([IO.Path]::GetFullPath($env:TEMP).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe flow fixture cleanup.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

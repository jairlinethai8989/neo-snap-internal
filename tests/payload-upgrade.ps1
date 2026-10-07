$ErrorActionPreference='Stop'
$module=Join-Path $PSScriptRoot '..\installer\payload-install.ps1'
if (-not (Test-Path -LiteralPath $module)) { throw 'Managed payload upgrade is missing.' }
. $module
$root=Join-Path $env:TEMP ('payload-upgrade-tests-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
function Assert($value,[string]$message) { if (-not $value) { throw $message } }
function Write-Fixture([string]$directory,[string]$path,[string]$text) {
    $file=Join-Path $directory $path
    New-Item -ItemType Directory -Path (Split-Path $file) -Force | Out-Null
    [IO.File]::WriteAllText($file,$text)
}
try {
    $legacy=[pscustomobject]@{schema=1;files=@('coreclr.dll','System.Private.CoreLib.dll','System.Windows.Forms.dll')}
    foreach ($case in 'upgrade','locked','corrupt','traversal') {
        $target=Join-Path $root "$case/installed"; $stage=Join-Path $root "$case/stage"
        Write-Fixture $target 'SnapCraft.exe' 'old-app'
        Write-Fixture $target 'SnapCraft.dll' 'old-core'
        Write-Fixture $target 'Assets/editor.html' 'old-editor'
        Write-Fixture $target 'notes.txt' 'user-owned text'
        Write-Fixture $target 'coreclr.dll' 'old-runtime'
        Write-Fixture $target 'ScreenRecorderLib.dll' 'old-recorder'
        Write-Fixture $target 'SnapCraft.runtimeconfig.json' '{"runtimeOptions":{"includedFrameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.12"}]}}'
        Write-Fixture $stage 'SnapCraft.exe' 'new-app'
        Write-Fixture $stage 'SnapCraft.dll' 'new-core'
        Write-Fixture $stage 'ScreenRecorderLib.dll' 'new-recorder'
        Write-Fixture $stage 'Assets/editor.html' 'new-editor'
        Write-Fixture $stage 'SnapCraft.runtimeconfig.json' '{"runtimeOptions":{"frameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.12"}]}}'
        $manifest=Get-ManagedPayloadManifest $stage
        Write-Fixture $stage 'managed-files.json' ($manifest | ConvertTo-Json -Depth 8)
        $locked=$null
        if ($case -eq 'corrupt') { Write-Fixture $stage 'SnapCraft.dll' 'tampered-core' }
        if ($case -eq 'traversal') { $manifest.files[0].path='../outside.txt'; Write-Fixture $stage 'managed-files.json' ($manifest | ConvertTo-Json -Depth 8) }
        if ($case -eq 'locked') { $locked=[IO.File]::Open((Join-Path $target 'SnapCraft.dll'),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None) }
        $failed=$false
        try { Install-ManagedPayload $stage $target $legacy } catch { $failed=$true }
        finally { if ($locked) { $locked.Dispose() } }
        Assert ((Get-Content -LiteralPath (Join-Path $target 'notes.txt') -Raw) -eq 'user-owned text') "User file was changed: $case"
        if ($case -eq 'upgrade') {
            Assert (-not $failed) 'Valid upgrade failed.'
            Assert ((Get-Content -LiteralPath (Join-Path $target 'SnapCraft.exe') -Raw) -eq 'new-app') 'Upgrade did not replace application.'
            Assert (-not (Test-Path -LiteralPath (Join-Path $target 'coreclr.dll'))) 'Bundled runtime was left behind.'
            Assert ((Get-Content -LiteralPath (Join-Path $target 'ScreenRecorderLib.dll') -Raw) -eq 'new-recorder') 'Native recorder was removed.'
        } else {
            Assert $failed "Invalid or blocked upgrade was accepted: $case"
            Assert ((Get-Content -LiteralPath (Join-Path $target 'SnapCraft.exe') -Raw) -eq 'old-app') "Old application was not preserved: $case"
            Assert ((Get-Content -LiteralPath (Join-Path $target 'SnapCraft.dll') -Raw) -eq 'old-core') "Old core was not preserved: $case"
            Assert ((Get-Content -LiteralPath (Join-Path $target 'coreclr.dll') -Raw) -eq 'old-runtime') "Old runtime was lost on failed upgrade: $case"
            Assert ((Get-Content -LiteralPath (Join-Path $target 'Assets/editor.html') -Raw) -eq 'old-editor') "Old editor was not restored: $case"
        }
    }
    $badZip=Join-Path $root 'traversal.zip'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    Add-Type -AssemblyName System.IO.Compression
    $zip=[IO.Compression.ZipFile]::Open($badZip,[IO.Compression.ZipArchiveMode]::Create)
    try { $entry=$zip.CreateEntry('../outside.txt'); $writer=[IO.StreamWriter]::new($entry.Open()); $writer.Write('escape'); $writer.Dispose() } finally { $zip.Dispose() }
    $rejected=$false; try { Expand-ManagedPayload $badZip | Out-Null } catch { $rejected=$true }
    Assert $rejected 'ZIP traversal was accepted.'
    Assert (-not (Test-Path -LiteralPath (Join-Path $root 'outside.txt'))) 'ZIP escaped the staging directory.'
    Write-Output 'PASS managed upgrade: staged digest validation, runtime cleanup, native/user file preservation, locked-file rollback, manifest/ZIP traversal rejection'
} finally {
    $resolved=[IO.Path]::GetFullPath($root)
    if (-not $resolved.StartsWith([IO.Path]::GetFullPath($env:TEMP).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture cleanup.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

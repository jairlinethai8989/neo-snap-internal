param([string]$Installer)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '..\installer\payload-install.ps1')
. (Join-Path $PSScriptRoot '..\installer\runtime-policy.ps1')
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$root=Join-Path ([IO.Path]::GetTempPath()) ('payload-package-tests-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
function Assert($value,[string]$message) { if (-not $value) { throw $message } }
try {
    $source=Join-Path $root 'source'
    New-Item -ItemType Directory -Path (Join-Path $source 'Assets\icons') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $source 'SnapCraft.exe'),'fixture-app')
    [IO.File]::WriteAllText((Join-Path $source 'Assets\icons\app.ico'),'fixture-icon')
    Get-ManagedPayloadManifest $source | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $source 'managed-files.json') -Encoding UTF8
    $archive=Join-Path $root 'windows-compressed.zip'
    Compress-Archive -Path (Join-Path $source '*') -DestinationPath $archive
    $stage=Expand-ManagedPayload $archive
    try {
        Assert ((Get-Content -LiteralPath (Join-Path $stage 'Assets\icons\app.ico') -Raw) -ceq 'fixture-icon') 'Windows ZIP nested asset was not extracted correctly.'
        $target=Join-Path $root 'installed'
        Install-ManagedPayload $stage $target ([pscustomobject]@{schema=1;files=@()})
        Assert ((Get-Content -LiteralPath (Join-Path $target 'SnapCraft.exe') -Raw) -ceq 'fixture-app') 'Windows ZIP application was not installed.'
        Read-ManagedPayloadManifest $target -Validate | Out-Null
    } finally { Remove-ManagedStagingDirectory $stage }
    foreach ($name in @('../escape.txt','..\escape.txt','Assets\../../escape.txt','/root.txt','\root.txt','C:\escape.txt','Assets\..\escape.txt','Assets\bad:stream')) {
        $badZip=Join-Path $root ([guid]::NewGuid().ToString('N')+'.zip')
        $zip=[IO.Compression.ZipFile]::Open($badZip,[IO.Compression.ZipArchiveMode]::Create)
        try { $entry=$zip.CreateEntry($name); $writer=[IO.StreamWriter]::new($entry.Open()); try { $writer.Write('escape') } finally { $writer.Dispose() } } finally { $zip.Dispose() }
        $rejected=$false
        try { $unexpected=Expand-ManagedPayload $badZip; Remove-ManagedStagingDirectory $unexpected } catch {
            Assert ($_.Exception.Message -match 'managed file path') "ZIP failed for a reason other than unsafe path rejection: $name"
            $rejected=$true
        }
        Assert $rejected "Unsafe ZIP path was accepted: $name"
    }
    if ($Installer) {
        $extracted=Join-Path $root 'setup'
        New-Item -ItemType Directory -Path $extracted | Out-Null
        $process=Start-Process -FilePath (Resolve-Path -LiteralPath $Installer).Path -ArgumentList @('/Q','/C',"/T:$extracted") -WindowStyle Hidden -Wait -PassThru
        Assert ($process.ExitCode -eq 0) 'Setup extraction failed.'
        # Use the packaged modules, not a more permissive test-only extraction path.
        . (Join-Path $extracted 'payload-install.ps1')
        . (Join-Path $extracted 'runtime-policy.ps1')
        $legacy=Get-Content -LiteralPath (Join-Path $extracted 'legacy-runtime-files.json') -Raw | ConvertFrom-Json
        $target=Join-Path $root 'candidate-install'
        $status=Invoke-ApplicationPayload (Join-Path $extracted 'payload.zip') $target $legacy {
            param($requirementsPath)
            Assert (Test-Path -LiteralPath $requirementsPath) 'Runtime preparation was not reached.'
            return 'Ready'
        } { }
        Assert ($status -eq 'Ready') 'Packaged payload installation failed.'
        Read-ManagedPayloadManifest $target -Validate | Out-Null
        Assert (Test-Path -LiteralPath (Join-Path $target 'Assets\editor-productivity-controls.js')) 'New editor tools are missing.'
        foreach ($icon in @('ocr-scan.png','ocr-read.png','ocr-copy.png','ocr-done.png')) {
            Assert (Test-Path -LiteralPath (Join-Path $target "Assets\icons\$icon")) "Native OCR icon is missing from setup: $icon"
        }
        Write-Output "PASS actual packaged payload install to isolated target: $Installer"
    }
    Write-Output 'PASS Windows ZIP nested assets: real expansion/install/digests and unsafe slash/backslash path rejection'
} finally {
    $resolved=[IO.Path]::GetFullPath($root)
    $temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if (-not $resolved.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolved -Leaf) -notmatch '^payload-package-tests-[0-9a-f]{32}$') { throw 'Unsafe fixture cleanup.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

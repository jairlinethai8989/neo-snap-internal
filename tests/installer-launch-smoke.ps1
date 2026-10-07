$ErrorActionPreference = 'Stop'
$directory = Join-Path $env:TEMP ('snapzy-launch-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory | Out-Null
$probe = Join-Path $directory 'probe.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /reference:System.Windows.Forms.dll /out:$probe (Join-Path $PSScriptRoot 'Fixtures\InstallerLaunchProbe.cs.txt')
if ($LASTEXITCODE -ne 0) { throw 'Could not build the isolated application visibility probe.' }
$previous = $env:SNAPZY_LAUNCH_PROBE
$env:SNAPZY_LAUNCH_PROBE = Join-Path $directory 'visibility.txt'
$info = New-Object Diagnostics.ProcessStartInfo
$info.FileName = "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe"
$info.Arguments = '-NoProfile -Command "Start-Sleep -Seconds 2"'
$info.UseShellExecute = $false; $info.CreateNoWindow = $true
$parent = [Diagnostics.Process]::Start($info)
$helper = $null
try {
    $path = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\installer\launch-after-setup.ps1'))
    $arguments = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -SetupPid {1} -AppPath "{2}"' -f $path, $parent.Id, $probe
    $helper = Start-Process -FilePath $info.FileName -ArgumentList $arguments -WindowStyle Hidden -PassThru
    while (-not $parent.HasExited) {
        if (Test-Path -LiteralPath $env:SNAPZY_LAUNCH_PROBE) { throw 'The application appeared before setup finished.' }
        Start-Sleep -Milliseconds 100
    }
    for ($i = 0; $i -lt 100; $i++) {
        if (Test-Path -LiteralPath $env:SNAPZY_LAUNCH_PROBE) { break }
        Start-Sleep -Milliseconds 100
    }
    if (-not (Test-Path -LiteralPath $env:SNAPZY_LAUNCH_PROBE)) { throw 'The application never reached its shown event.' }
    if ((Get-Content -LiteralPath $env:SNAPZY_LAUNCH_PROBE -Raw) -ne 'VISIBLE') { throw 'The helper launched an invisible application window.' }
    $helper.WaitForExit()
    if ($helper.ExitCode -ne 0) { throw 'The setup launch helper failed.' }
    Write-Output 'PASS real Windows PowerShell helper: waits for setup exit, keeps its console hidden and shows the requested app'
} finally {
    $env:SNAPZY_LAUNCH_PROBE = $previous
    $parent.Dispose(); if ($helper) { $helper.Dispose() }
}

$ErrorActionPreference = 'Stop'
$module = Join-Path $PSScriptRoot '..\installer\runtime-policy.ps1'
if (-not (Test-Path -LiteralPath $module)) { throw 'Shared runtime prerequisite policy is missing.' }
. $module
function Assert($value, [string]$message) { if (-not $value) { throw $message } }
$requirements = [pscustomobject]@{ schema = 1; architecture = 'x64'; rollForward = 'LatestPatch'; frameworks = @(
    [pscustomobject]@{ name = 'Microsoft.NETCore.App'; version = '10.0.12' },
    [pscustomobject]@{ name = 'Microsoft.WindowsDesktop.App'; version = '10.0.12' }
) }
$temp = Join-Path $env:TEMP ('runtime-policy-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    foreach ($fx in $requirements.frameworks) {
        foreach ($v in '10.0.1','10.0.12','10.0.13','10.0.14-preview.1','11.0.0') {
            $dir = Join-Path $temp ("shared/{0}/{1}" -f $fx.name,$v)
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $file = if ($fx.name -eq 'Microsoft.NETCore.App') { 'coreclr.dll' } else { 'System.Windows.Forms.dll' }
            [IO.File]::WriteAllBytes((Join-Path $dir $file), [byte[]](1,2,3))
            if ($fx.name -eq 'Microsoft.WindowsDesktop.App') {
                [IO.File]::WriteAllText((Join-Path $dir 'Microsoft.WindowsDesktop.App.runtimeconfig.json'),
                    ('{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"'+$v+'"}}}'))
            }
        }
    }
    function Inventory([string]$core, [string]$desktop) {
        @("Microsoft.NETCore.App $core [$temp\shared\Microsoft.NETCore.App]", "Microsoft.WindowsDesktop.App $desktop [$temp\shared\Microsoft.WindowsDesktop.App]")
    }
    foreach ($case in @(
        @{ Core='10.0.12'; Desktop='10.0.12'; Architecture='x64'; Want=$true },
        @{ Core='10.0.13'; Desktop='10.0.13'; Architecture='x64'; Want=$true },
        @{ Core='10.0.1'; Desktop='10.0.1'; Architecture='x64'; Want=$false },
        @{ Core='10.0.12'; Desktop='10.0.1'; Architecture='x64'; Want=$false },
        @{ Core='10.0.1'; Desktop='10.0.12'; Architecture='x64'; Want=$false },
        @{ Core='10.0.12'; Desktop='10.0.13'; Architecture='x64'; Want=$false },
        @{ Core='10.0.12'; Desktop='10.0.12'; Architecture='x86'; Want=$false },
        @{ Core='10.0.14-preview.1'; Desktop='10.0.14-preview.1'; Architecture='x64'; Want=$false },
        @{ Core='11.0.0'; Desktop='11.0.0'; Architecture='x64'; Want=$false }
    )) {
        $got = Test-DotNetRuntimeInventory $requirements (Inventory $case.Core $case.Desktop) $temp $case.Architecture
        Assert ($got -eq $case.Want) "Incorrect runtime compatibility: $($case.Core)/$($case.Desktop)/$($case.Architecture)"
    }
    Assert (-not (Test-DotNetRuntimeInventory $requirements @("Microsoft.NETCore.App 10.0.12 [$temp\shared\Microsoft.NETCore.App]") $temp 'x64')) 'Core-only runtime was accepted.'
    Remove-Item -LiteralPath (Join-Path $temp 'shared/Microsoft.WindowsDesktop.App/10.0.12/System.Windows.Forms.dll')
    Assert (-not (Test-DotNetRuntimeInventory $requirements (Inventory '10.0.12' '10.0.12') $temp 'x64')) 'Stale inventory or empty runtime directory was accepted.'
    Assert (-not (Test-DotNetRuntimeInventory $requirements @('Microsoft.NETCore.App 10.0.12 [C:\outside]','Microsoft.WindowsDesktop.App 10.0.12 [C:\outside]') $temp 'x64')) 'Inventory from a different host root was accepted.'
    $config = [pscustomobject]@{ runtimeOptions = [pscustomobject]@{ tfm='net10.0'; rollForward='LatestPatch'; frameworks=$requirements.frameworks } }
    $derived = Get-DotNetRequirements $config
    Assert ($derived.architecture -eq 'x64' -and $derived.frameworks.Count -eq 2 -and $derived.frameworks[1].version -eq '10.0.12') 'Requirements did not match actual runtime configuration.'
    foreach ($bad in @(
        [pscustomobject]@{ runtimeOptions=[pscustomobject]@{ tfm='net10.0'; includedFrameworks=$requirements.frameworks } },
        [pscustomobject]@{ runtimeOptions=[pscustomobject]@{ tfm='net10.0'; rollForward='Major'; frameworks=$requirements.frameworks } }
    )) { $rejected=$false; try { Get-DotNetRequirements $bad | Out-Null } catch { $rejected=$true }; Assert $rejected 'Inconsistent runtime configuration was accepted.' }

    function Release([string]$version, [string]$url='') {
        if (-not $url) { $url="https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/$version/windowsdesktop-runtime-$version-win-x64.exe" }
        [pscustomobject]@{ runtime=[pscustomobject]@{version=$version}; windowsdesktop=[pscustomobject]@{version=$version;files=@(
            [pscustomobject]@{name='windowsdesktop-runtime-win-x64.exe';rid='win-x64';url=$url;hash=('a'*128)}
        )} }
    }
    $metadata=[pscustomobject]@{releases=@((Release '10.0.1'),(Release '10.0.13'),(Release '10.0.12'),(Release '11.0.0'),(Release '10.0.14-preview.1'))}
    Assert ((Get-DotNetInstallerRelease $requirements $metadata).version -eq '10.0.13') 'Installer did not choose the highest compatible stable patch.'
    foreach ($url in 'http://builds.dotnet.microsoft.com/bad.exe','https://builds.dotnet.microsoft.com.evil.example/bad.exe','https://evil.example/bad.exe','https://user@builds.dotnet.microsoft.com/bad.exe') {
        $bad=[pscustomobject]@{releases=@((Release '10.0.12' $url))}
        $rejected=$false; try { Get-DotNetInstallerRelease $requirements $bad | Out-Null } catch { $rejected=$true }
        Assert $rejected "Unsafe runtime URL accepted: $url"
    }
    $bad=[pscustomobject]@{releases=@((Release '10.0.12'))}; $bad.releases[0].windowsdesktop.files[0].hash='invalid'
    $rejected=$false; try { Get-DotNetInstallerRelease $requirements $bad | Out-Null } catch { $rejected=$true }; Assert $rejected 'Missing SHA-512 metadata was accepted.'

    foreach ($scenario in 'existing','decline','success','bad-hash','bad-signature','offline','cancel','uac','failure','restart','failed-recheck') {
        $script:ready=$scenario -eq 'existing'; $script:acquired=$false; $script:installed=$false
        $download=Join-Path $temp 'download.exe'; [IO.File]::WriteAllText($download,'generated installer fixture')
        $hash=(Get-FileHash -LiteralPath $download -Algorithm SHA512).Hash
        $ops=@{
            Detect={ $script:ready }
            Consent={ $scenario -ne 'decline' }
            Acquire={
                $script:acquired=$true
                if ($scenario -eq 'offline') { throw 'Controlled network failure' }
                [pscustomobject]@{path=$download;hash=$(if ($scenario -eq 'bad-hash') {'b'*128} else {$hash})}
            }
            Signature={param($path) $scenario -ne 'bad-signature'}
            Install={param($path)
                $script:installed=$true
                if ($scenario -eq 'uac') { throw [ComponentModel.Win32Exception]::new(1223) }
                if ($scenario -eq 'success') { $script:ready=$true }
                switch ($scenario) { 'cancel' {1602} 'failure' {1603} 'restart' {3010} default {0} }
            }
            Cleanup={param($artifact) if (Test-Path -LiteralPath $artifact.path) { Remove-Item -LiteralPath $artifact.path } }
        }
        $result=$null; $errorCaught=$false
        try { $result=Invoke-DotNetPrerequisite $requirements $ops } catch { $errorCaught=$true }
        switch ($scenario) {
            'existing' { Assert ($result -eq 'Ready' -and -not $script:acquired) 'Existing compatible runtime triggered a download.' }
            'decline' { Assert ($result -eq 'Canceled' -and -not $script:acquired) 'Consent refusal caused download/install.' }
            'success' { Assert ($result -eq 'Ready' -and $script:installed) 'Successful verified install was not ready.' }
            'cancel' { Assert ($result -eq 'Canceled') 'Runtime installer cancellation was ignored.' }
            'uac' { Assert ($result -eq 'Canceled') 'UAC refusal was not handled.' }
            'restart' { Assert ($result -eq 'RestartRequired') 'Restart requirement was reported as ready.' }
            default { Assert $errorCaught "Failed preflight was reported as ready: $scenario" }
        }
        if ($scenario -in 'bad-hash','bad-signature','offline','decline','existing') { Assert (-not $script:installed) "Unapproved/unverified installer was executed: $scenario" }
        if ($scenario -notin 'existing','decline','offline') { Assert (-not (Test-Path -LiteralPath $download)) "Installer download was left behind: $scenario" }
    }
    Write-Output 'PASS shared runtime policy: compatible inventory, stable x64 release, consent, SHA-512/signature, cancellation, restart, recheck, cleanup'
} finally {
    $resolved=[IO.Path]::GetFullPath($temp)
    if (-not $resolved.StartsWith([IO.Path]::GetFullPath($env:TEMP).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

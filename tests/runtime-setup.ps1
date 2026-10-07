param([string]$SetupDirectory=(Join-Path $PSScriptRoot '..\installer'))
$ErrorActionPreference='Stop'
$file=Join-Path $SetupDirectory 'runtime-setup.ps1'
if (-not (Test-Path -LiteralPath $file)) { throw 'Shared runtime setup is missing.' }
. $file
$root=Get-RegisteredDotNetRoot
$hostFile=Join-Path $root 'dotnet.exe'
if (Test-Path -LiteralPath $hostFile) {
    if ((Get-PeArchitecture $hostFile) -ne 'x64') { throw 'Global runtime discovery selected the wrong architecture.' }
    if (-not (Test-MicrosoftBinary $hostFile)) { throw 'Global host signature was not recognized.' }
}
$installedRequirements=[pscustomobject]@{schema=1;architecture='x64';rollForward='LatestPatch';frameworks=@(
    @{name='Microsoft.NETCore.App';version='10.0.12'},@{name='Microsoft.WindowsDesktop.App';version='10.0.12'})}
$compatible=$true
foreach ($framework in 'Microsoft.NETCore.App','Microsoft.WindowsDesktop.App') {
    $frameworkRoot=Join-Path $root "shared\$framework"
    $versions=@(Get-ChildItem -LiteralPath $frameworkRoot -Directory -ErrorAction SilentlyContinue | Where-Object {
        $_.Name -match '^10\.0\.(\d+)$' -and [int]$Matches[1] -ge 12
    })
    if ($versions.Count -eq 0) { $compatible=$false }
}
if ($compatible) {
    # A short-lived dotnet host must retain its exit code on Windows PowerShell 5.1.
    for ($attempt=0; $attempt -lt 5; $attempt++) {
        if (-not (Get-DotNetRuntimeStatus $installedRequirements)) {
            throw "An installed compatible global Desktop Runtime was rejected (PowerShell $($PSVersionTable.PSVersion), attempt $attempt)."
        }
    }
    $installedPath=Join-Path $env:TEMP ('runtime-installed-'+[guid]::NewGuid().ToString('N')+'.json')
    $unexpectedConsent=$false
    Add-Type -AssemblyName System.Windows.Forms
    $timer=[Windows.Forms.Timer]::new(); $timer.Interval=100
    $timer.Add_Tick({
        foreach ($dialog in @([Windows.Forms.Application]::OpenForms)) {
            if ($dialog.Text -eq 'Installed Runtime Test - .NET' -and $dialog.Controls['accept']) {
                $script:unexpectedConsent=$true
                $dialog.DialogResult=[Windows.Forms.DialogResult]::Cancel
                $dialog.Close()
            }
        }
    })
    try {
        $installedRequirements | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $installedPath -Encoding UTF8
        $timer.Start()
        if ((Install-RequiredDesktopRuntime $installedPath 'Installed Runtime Test' 'en') -ne 'Ready' -or $unexpectedConsent) {
            throw 'An already installed compatible runtime must proceed without installation consent.'
        }
    } finally {
        $timer.Stop(); $timer.Dispose()
        Remove-Item -LiteralPath $installedPath -Force
    }
    Write-Output 'PASS installed runtime: five real host probes and setup proceeds without reinstalling .NET'
} else { Write-Output 'SKIP live positive detection: global Core/Desktop 10.0.12+ is not installed.' }
foreach ($language in 'en','th') {
    foreach ($name in 'Neo Snap','SnapZy') {
        $form=New-DotNetConsentForm $name $language '10.0.12'
        try {
            if ($form.Text -notlike "*$name*" -or -not $form.Controls['message'].Text.Contains('10.0.12') -or
                -not $form.Controls['message'].Text.Contains('x64')) { throw 'Prerequisite consent does not identify product, minimum version and architecture.' }
            if ($form.AcceptButton -ne $form.Controls['accept'] -or $form.CancelButton -ne $form.Controls['cancel'] -or
                $form.Controls['cancel'].DialogResult -ne [Windows.Forms.DialogResult]::Cancel) { throw 'Consent cannot be refused safely.' }
            if ($language -eq 'en' -and $form.Controls['accept'].Text -ne 'Install .NET') { throw 'English consent was not translated.' }
            if ($language -eq 'th' -and $form.Controls['accept'].Text -eq 'Install .NET') { throw 'Thai consent was not translated.' }
            $message=$form.Controls['message']
            $measured=[Windows.Forms.TextRenderer]::MeasureText($message.Text,$message.Font,[Drawing.Size]::new($message.Width,1000),[Windows.Forms.TextFormatFlags]::WordBreak)
            if ($measured.Height -gt $message.Height) { throw "Runtime consent text is clipped: $language" }
        } finally { $form.Dispose() }
    }
}
$unsigned=Join-Path $env:TEMP ('unsigned-runtime-'+[guid]::NewGuid().ToString('N')+'.exe')
try {
    [IO.File]::WriteAllText($unsigned,'not a signed installer')
    if (Test-MicrosoftBinary $unsigned) { throw 'Unsigned installer was trusted.' }
    if ((Get-PeArchitecture $unsigned) -eq 'x64') { throw 'Non-PE executable was accepted.' }
} finally { Remove-Item -LiteralPath $unsigned -Force }
$requirements=[pscustomobject]@{schema=1;architecture='x64';rollForward='LatestPatch';frameworks=@(
    @{name='Microsoft.NETCore.App';version='10.0.9999'},@{name='Microsoft.WindowsDesktop.App';version='10.0.9999'})}
$path=Join-Path $env:TEMP ('runtime-consent-'+[guid]::NewGuid().ToString('N')+'.json')
try {
    $requirements | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $path -Encoding UTF8
    foreach ($language in 'en','th') {
        $timer=[Windows.Forms.Timer]::new(); $timer.Interval=100
        $timer.Add_Tick({
            foreach ($dialog in @([Windows.Forms.Application]::OpenForms)) {
                if ($dialog.Text -eq 'Runtime Consent Test - .NET' -and $dialog.Controls['accept']) {
                    $dialog.DialogResult=[Windows.Forms.DialogResult]::Cancel
                    $dialog.Close()
                }
            }
        })
        try {
            $timer.Start()
            if ((Install-RequiredDesktopRuntime $path 'Runtime Consent Test' $language) -ne 'Canceled') { throw 'Actual prerequisite UI did not honor refusal.' }
        } finally { $timer.Stop(); $timer.Dispose() }
    }
} finally { Remove-Item -LiteralPath $path -Force }
Write-Output 'PASS runtime setup: registered x64 host, real Microsoft signature, bilingual consent text fit, actual consent refusal, unsigned rejection'

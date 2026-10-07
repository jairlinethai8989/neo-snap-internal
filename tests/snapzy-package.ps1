param([string]$Installer = (Join-Path $PSScriptRoot '..\dist\SnapZy-Setup-1.0.0.exe'))
$ErrorActionPreference = 'Stop'
$Installer = (Resolve-Path -LiteralPath $Installer).Path
$directory = Join-Path $env:TEMP ('snapzy-package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory | Out-Null
$process = Start-Process -FilePath $Installer -ArgumentList @('/Q', '/C', "/T:$directory") -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Package extraction failed: $($process.ExitCode)" }
$bytes = [IO.File]::ReadAllBytes((Join-Path $directory 'setup-options.ps1'))
if ($bytes[0] -ne 0xef -or $bytes[1] -ne 0xbb -or $bytes[2] -ne 0xbf) { throw 'Packaged setup options lost the Windows PowerShell UTF-8 BOM fix.' }
. (Join-Path $directory 'product-profile.ps1')
$profile = Get-ProductProfile 'Snapzy'
if ($profile.Name -cne 'SnapZy' -or $profile.Language -ne 'en') { throw 'Packaged setup must use the SnapZy display name and English default.' }
if ($profile.IconName -ne 'Snapzy-1.0.0-A.ico') { throw 'The installer still uses a cached old icon path.' }
$payload = Join-Path $directory 'payload'
Expand-Archive -LiteralPath (Join-Path $directory 'payload.zip') -DestinationPath $payload
$install = Get-Content -LiteralPath (Join-Path $directory 'install.ps1') -Raw
if (-not $install.Contains('$oldLink -ne $startLink -and $oldLink -ne $desktopLink')) { throw 'Packaged installer can delete its newly created shortcuts.' }
if ($install.Contains('ConvertFrom-Json -AsHashtable')) { throw 'Packaged installer loses settings on Windows PowerShell 5.1.' }
if (-not $install.Contains('$ProductFlavor -ne ''Snapzy'' -and $savedLanguage')) { throw 'Packaged setup must not override the English default with the previously selected Thai language.' }
$launch = Get-Content -LiteralPath (Join-Path $payload 'launch-after-setup.ps1') -Raw
if (-not $launch.Contains('-WindowStyle Normal') -or $launch.Contains('-WindowStyle Hidden')) { throw 'Packaged launcher hides the requested application.' }
$icon = Join-Path $payload ('Assets\icons\' + $profile.IconName)
if ((Get-FileHash -LiteralPath $icon).Hash -ne (Get-FileHash -LiteralPath (Join-Path $payload 'Assets\icons\snapzy.ico')).Hash) { throw 'Shortcut and runtime icons differ.' }
Add-Type -AssemblyName System.Drawing
$embedded = [Drawing.Icon]::ExtractAssociatedIcon((Join-Path $payload 'SnapCraft.exe'))
try {
    $bitmap = $embedded.ToBitmap()
    try {
        $pixel = $bitmap.GetPixel([int]($bitmap.Width / 2), [int]($bitmap.Height / 2))
        if ([Math]::Abs($pixel.R - 99) -gt 5 -or [Math]::Abs($pixel.G - 209) -gt 5 -or [Math]::Abs($pixel.B - 244) -gt 5) { throw 'The executable does not embed the approved cyan pane of icon A.' }
    } finally { $bitmap.Dispose() }
} finally { $embedded.Dispose() }
foreach ($asset in @('editor-help.js', 'icons\snapzy.svg')) {
    if (-not (Test-Path -LiteralPath (Join-Path $payload ('Assets\' + $asset)))) { throw "Package is missing $asset." }
}
$version = (Get-Item -LiteralPath (Join-Path $payload 'SnapCraft.exe')).VersionInfo
if ($version.ProductName -cne 'SnapZy' -or [version]$version.FileVersion -ne [version]'1.0.0.0') { throw 'Incorrect product identity or version.' }
Write-Output 'PASS extracted Snapzy package: UTF-8 BOM, shortcut retention, visible launch, settings preservation, icon A, Help and version'
Write-Output "Extracted for inspection only: $directory"

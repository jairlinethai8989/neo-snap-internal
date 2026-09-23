$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
try {
    if (Get-Process SnapCraft -ErrorAction SilentlyContinue) { throw 'Close SnapCraft before uninstalling it.' }
    $base = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs'))
    $target = [IO.Path]::GetFullPath((Join-Path $base 'SnapCraft'))
    if (-not $target.StartsWith(($base.TrimEnd('\') + '\'), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The installation path is invalid.'
    }
    $startLink = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\SnapCraft.lnk'
    $desktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) 'SnapCraft.lnk'
    foreach ($link in @($startLink, $desktopLink)) {
        if (Test-Path -LiteralPath $link) { Remove-Item -LiteralPath $link -Force }
    }
    Remove-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SnapCraft' -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
    [System.Windows.Forms.MessageBox]::Show('SnapCraft was removed. Your exported images and videos were not deleted.', 'SnapCraft', [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
} catch {
    [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, 'SnapCraft uninstall failed', [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
    exit 1
}

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

function Show-Notice([string]$message, [string]$title = 'SnapCraft') {
    [System.Windows.Forms.MessageBox]::Show($message, $title, [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
}

try {
    if (-not [Environment]::Is64BitOperatingSystem) { throw 'SnapCraft requires 64-bit Windows.' }
    $build = [int](Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion').CurrentBuildNumber
    if ($build -lt 19041) { throw 'SnapCraft requires Windows 10 version 2004 or later.' }
    if (Get-Process SnapCraft -ErrorAction SilentlyContinue) { throw 'Close SnapCraft before installing an update.' }
    $target = Join-Path $env:LOCALAPPDATA 'Programs\SnapCraft'
    $archive = Join-Path $PSScriptRoot 'payload.zip'
    if (-not (Test-Path -LiteralPath $archive)) { throw 'The installer payload is missing.' }
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Expand-Archive -LiteralPath $archive -DestinationPath $target -Force
    $app = Join-Path $target 'SnapCraft.exe'
    if (-not (Test-Path -LiteralPath $app)) { throw 'SnapCraft.exe was not installed.' }

    $shell = New-Object -ComObject WScript.Shell
    $startLink = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\SnapCraft.lnk'
    $desktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) 'SnapCraft.lnk'
    foreach ($link in @($startLink, $desktopLink)) {
        $shortcut = $shell.CreateShortcut($link)
        $shortcut.TargetPath = $app
        $shortcut.WorkingDirectory = $target
        $shortcut.IconLocation = "$app,0"
        $shortcut.Save()
    }

    $uninstall = Join-Path $target 'uninstall.ps1'
    $uninstallCommand = '"{0}" -NoProfile -ExecutionPolicy Bypass -File "{1}"' -f (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'), $uninstall
    $reg = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SnapCraft'
    New-Item -Path $reg -Force | Out-Null
    New-ItemProperty -Path $reg -Name DisplayName -Value 'SnapCraft Capture' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $reg -Name DisplayVersion -Value '0.1.0' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $reg -Name Publisher -Value 'SnapCraft' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $reg -Name InstallLocation -Value $target -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $reg -Name UninstallString -Value $uninstallCommand -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $reg -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $reg -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null

    $missing = @()
    $dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    $runtimes = if (Test-Path -LiteralPath $dotnet) { & $dotnet --list-runtimes } else { @() }
    if (-not ($runtimes | Where-Object { $_ -match '^Microsoft\.WindowsDesktop\.App 8\.' })) {
        $missing += '.NET 8 Desktop Runtime: https://dotnet.microsoft.com/download/dotnet/8.0'
    }
    $webViewId = '{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}'
    $webViewKeys = @(
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\$webViewId",
        "HKCU:\Software\Microsoft\EdgeUpdate\Clients\$webViewId"
    )
    $webViewInstalled = $false
    foreach ($key in $webViewKeys) {
        $version = (Get-ItemProperty -Path $key -Name pv -ErrorAction SilentlyContinue).pv
        if ($version -and $version -ne '0.0.0.0') { $webViewInstalled = $true; break }
    }
    if (-not $webViewInstalled) {
        $missing += 'WebView2 Runtime: https://developer.microsoft.com/microsoft-edge/webview2/'
    }
    $vc = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64' -ErrorAction SilentlyContinue
    if ($vc.Installed -ne 1) {
        $missing += 'Visual C++ Redistributable x64: https://aka.ms/vs/17/release/vc_redist.x64.exe'
    }
    if ($missing.Count) {
        Show-Notice ("SnapCraft is installed. Install these Microsoft components before opening it:`n`n" + ($missing -join "`n`n"))
    } else {
        Show-Notice 'SnapCraft was installed. You can open it from the desktop or Start menu.'
        Start-Process -FilePath $app -WorkingDirectory $target
    }
} catch {
    [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, 'SnapCraft setup failed', [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
    exit 1
}

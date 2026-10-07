$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'installer\install.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'Installer has syntax errors.' }
$merge = $ast.Find({ param($node) $node -is [Management.Automation.Language.IfStatementAst] -and $node.Extent.Text.StartsWith('if (Test-Path -LiteralPath $settingsFile)') }, $true)
if (-not $merge) { throw 'Installer settings merge block was not found.' }
$directory = Join-Path $env:TEMP ('snapzy-settings-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory | Out-Null
$settingsFile = Join-Path $directory 'settings.json'
@{ Language = 'th'; HotkeyModifiers = 6; HotkeyKey = 83; CaptureDelay = 2; Nested = @{ Keep = $true } } | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $settingsFile -Encoding UTF8
$settings = @{}
. ([scriptblock]::Create($merge.Extent.Text))
$settings['Language'] = 'en'
if ($settings.Language -ne 'en' -or $settings.HotkeyModifiers -ne 6 -or $settings.HotkeyKey -ne 83 -or $settings.CaptureDelay -ne 2 -or -not $settings.Nested.Keep) {
    throw 'Reinstall lost existing settings while changing the selected language.'
}
Write-Output 'PASS installer: existing settings survive reinstall on Windows PowerShell 5.1'

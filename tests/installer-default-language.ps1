$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $root 'installer\product-profile.ps1')
. (Join-Path $root 'installer\setup-options.ps1')
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'installer\install.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'Installer syntax is invalid.' }
$read = $ast.Find({ param($node) $node -is [Management.Automation.Language.TryStatementAst] -and $node.Body.Extent.Text.TrimStart('{', [char]13, [char]10, [char]32).StartsWith('$savedLanguage =') }, $true)
if (-not $read) { throw 'Installer language initialization was not found.' }
$directory = Join-Path $env:TEMP ('snapzy-setup-language-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory | Out-Null
$settingsFile = Join-Path $directory 'settings.json'
@{ Language = 'th' } | ConvertTo-Json | Set-Content -LiteralPath $settingsFile -Encoding UTF8
foreach ($ProductFlavor in @('Snapzy', 'NeoSnap')) {
    $product = Get-ProductProfile $ProductFlavor
    $initialLanguage = $product.Language
    . ([scriptblock]::Create($read.Extent.Text))
    $form = New-SetupOptionsForm $product $initialLanguage
    try {
        $expected = if ($ProductFlavor -eq 'Snapzy') { 0 } else { 1 }
        if ($form.Controls['language'].SelectedIndex -ne $expected) { throw 'Public setup must start in English even when the installed app uses Thai.' }
        $form.Controls['language'].SelectedIndex = 1
        if ($form.Controls['language'].SelectedIndex -ne 1) { throw 'Thai must remain selectable.' }
    } finally { $form.Dispose() }
}
Write-Output 'PASS public installer defaults to English on reinstall; Thai remains selectable; Neo Snap is unchanged'

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'installer\install.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'Installer has syntax errors.' }
$create = $ast.Find({ param($node) $node -is [Management.Automation.Language.ForEachStatementAst] -and $node.Body.Extent.Text.Contains('$shortcut.Save()') }, $true)
$cleanup = $ast.Find({ param($node) $node -is [Management.Automation.Language.ForEachStatementAst] -and $node.Body.Extent.Text.Contains('$oldLink =') }, $true)
if (-not $create -or -not $cleanup) { throw 'Shortcut creation and legacy cleanup blocks were not found.' }
. (Join-Path $root 'installer\product-profile.ps1')
$directory = Join-Path $env:TEMP ('snapzy-shortcuts-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory | Out-Null
$shell = New-Object -ComObject WScript.Shell
foreach ($flavor in @('Snapzy','NeoSnap')) {
    $product = Get-ProductProfile $flavor
    $target = Join-Path $directory $flavor
    New-Item -ItemType Directory -Path $target | Out-Null
    $app = Join-Path $target 'SnapCraft.exe'
    $icon = Join-Path $root 'src\SnapCraft\Assets\icons\app.ico'
    $startLink = Join-Path $target ($product.Name + '-start.lnk')
    $desktopLink = Join-Path $target ($product.Name + '.lnk')
    $createShortcuts = $true
    & ([scriptblock]::Create($create.Extent.Text))
    & ([scriptblock]::Create($cleanup.Extent.Text))
    foreach ($link in @($startLink,$desktopLink)) {
        if (-not (Test-Path -LiteralPath $link)) { throw "$flavor lost the requested shortcut after legacy cleanup: $link" }
        if ($shell.CreateShortcut($link).TargetPath -ne $app) { throw 'Shortcut targets the wrong application.' }
    }
}
Write-Output 'PASS installer: requested Desktop and Start menu shortcuts survive cleanup in both editions'

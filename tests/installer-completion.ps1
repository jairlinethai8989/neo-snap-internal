$ErrorActionPreference = 'Stop'
$file = Join-Path $PSScriptRoot '..\installer\install.ps1'
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile([IO.Path]::GetFullPath($file), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'Installer has syntax errors.' }
$completion = $ast.Find({ param($node)
    $node -is [Management.Automation.Language.IfStatementAst] -and
    $node.Clauses[0].Item1.Extent.Text -eq '$missing.Count'
}, $true)
if (-not $completion) { throw 'Installer prerequisite completion branch was not found.' }
$script:events = [Collections.Generic.List[string]]::new()
function Show-Notice([string]$message) { $script:events.Add('completion-visible') }
function Start-Process { $script:events.Add('application-started-before-installer-finished') }
$app = 'test-owned-placeholder.exe'
$target = 'test-owned-placeholder'
$launchAfterSetup = $false
& ([scriptblock]::Create(($completion.ElseClause.Statements.Extent.Text -join "`n")))
if ($script:events.Count -ne 1 -or $script:events[0] -ne 'completion-visible') {
    throw "Setup must leave the user at completion without starting another window: $($script:events -join ', ')"
}
Write-Output 'PASS installer completion: no application launches before setup dismissal'

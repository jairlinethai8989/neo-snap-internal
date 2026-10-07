$ErrorActionPreference = 'Stop'
$installer = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\installer'))
foreach ($script in (Get-ChildItem -LiteralPath $installer -Filter '*.ps1')) {
$setupOptions = $script.FullName
$bytes = [IO.File]::ReadAllBytes($setupOptions)
$text = [Text.Encoding]::UTF8.GetString($bytes)
if ($text -match '[^\x00-\x7F]' -and ($bytes.Length -lt 3 -or $bytes[0] -ne 0xEF -or $bytes[1] -ne 0xBB -or $bytes[2] -ne 0xBF)) {
    throw "$($script.Name) must be UTF-8 with BOM so Windows PowerShell 5.1 can read localized text."
}

$encodedPath = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($setupOptions))
$command = @"
`$path = [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String('$encodedPath'))
`$tokens = `$null
`$errors = `$null
[System.Management.Automation.Language.Parser]::ParseFile(`$path, [ref]`$tokens, [ref]`$errors) | Out-Null
if (`$errors.Count) { `$errors | ForEach-Object { [Console]::Error.WriteLine(`$_.Message) }; exit 1 }
"@
$encodedCommand = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
$windowsPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$output = & $windowsPowerShell -NoProfile -EncodedCommand $encodedCommand 2>&1
if ($LASTEXITCODE -ne 0) { throw "Windows PowerShell 5.1 could not parse $($script.Name): $($output -join ' ')" }
}

Write-Output 'PASS every installer module parses in Windows PowerShell 5.1 with valid localized encoding'

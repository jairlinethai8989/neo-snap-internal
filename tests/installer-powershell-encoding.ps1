$ErrorActionPreference = 'Stop'
$setupOptions = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\installer\setup-options.ps1'))
$bytes = [IO.File]::ReadAllBytes($setupOptions)
if ($bytes.Length -lt 3 -or $bytes[0] -ne 0xEF -or $bytes[1] -ne 0xBB -or $bytes[2] -ne 0xBF) {
    throw 'Setup options must be UTF-8 with BOM so Windows PowerShell 5.1 can read the Thai labels.'
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
if ($LASTEXITCODE -ne 0) { throw "Windows PowerShell 5.1 could not parse setup-options.ps1: $($output -join ' ')" }

Write-Output 'PASS installer setup options parse in Windows PowerShell 5.1'

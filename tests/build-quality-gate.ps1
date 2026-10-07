$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot '..'
$builder = Get-Content -LiteralPath (Join-Path $root 'installer/build-installer.ps1') -Raw
if (-not $builder.Contains('WaitForExit(900000)') -or -not $builder.Contains('$package.ExitCode')) {
    throw 'The installer build must wait for packaging completion and check the process exit code.'
}
if (-not $builder.Contains('-ArgumentList "/N /Q $sedPath"') -or -not $builder.Contains('.ShortPath')) {
    throw 'IExpress requires an unquoted SED argument; paths with spaces need a short path.'
}
$build = Get-Content -LiteralPath (Join-Path $root 'build.ps1') -Raw
if (-not $build.Contains('[switch]$CoreOnly') -or -not $build.Contains("tools/test-release.ps1") -or -not $build.Contains('-Desktop') -or -not $build.Contains("= 'Both'")) {
    throw 'The default build must validate both products and desktop behavior, with core-only checks explicitly requested.'
}
Write-Output 'PASS build quality contract: real packaging completion and both-product desktop gate'

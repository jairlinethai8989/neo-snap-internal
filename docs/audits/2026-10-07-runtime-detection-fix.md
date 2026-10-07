# Windows PowerShell Runtime Detection Fix

Date: 2026-10-07. Local candidates only; product versions are unchanged.

## Root Cause

The user successfully installed global Microsoft.NETCore.App and
Microsoft.WindowsDesktop.App 10.0.12 x64. Independent runtime enumeration,
PE architecture and Microsoft Authenticode validation all succeeded.

The installer runs Windows PowerShell 5.1. Its Start-Process -PassThru probe
returned a process whose ExitCode was null after a successful, short-lived
dotnet --list-runtimes invocation. The process exited and wrote 736 bytes of
valid inventory with no stderr. The check null -ne 0 rejected that inventory.
The same probe succeeded under PowerShell 7. This was an installer detection
bug, not failed runtime installation or missing user permission.

## Change

The probe now owns a System.Diagnostics.Process handle, with hidden execution,
asynchronous stdout/stderr reads and a bounded wait. It still requires exit
code zero, signed Microsoft x64 host, valid inventory, compatible frameworks
and the output size limit. It does not treat null as success or bypass runtime
validation. No runtime reinstallation, elevation or app installation was run.

## Regression Evidence

- New live test failed on the original helper under Windows PowerShell 5.1.
- The same test failed against the module extracted from the previous setup.
- Corrected source passed five real host probes and actual prerequisite flow
  returned Ready without consent/download/install under PowerShell 5.1 and 7.
- The live positive test explicitly skips when global Core/Desktop 10.0.12+
  is absent. Existing policy fixtures still exercise missing/incompatible
  runtimes independently. Consent refusal remains tested in both languages.
- Both-product release gate passed native/core suites, browser editor/launcher
  suites and installer tests in Windows PowerShell 5.1. Both actual
  framework-dependent payloads were rebuilt and validated by the deployment
  test. No live desktop capture tests were requested in this installer fix.

## Scope

Both Neo Snap and SnapZy consume the shared corrected module. Existing
worktree changes and previous installer candidates are preserved. Clean-PC
elevated runtime installation, restart and full app setup remain pilot checks.

## Corrected Local Installers

Folder: `dist/runtime-detection-fix-candidates`.

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| Neo-Snap-Windows-Setup-0.1.20.exe | 7,938,048 | 06973DB4AC5EEA93963D71AFCF82F8B1B6A61D2386EBDB54D49FB6E364BA7717 |
| SnapZy-Setup-1.0.0.exe | 7,933,952 | 97E042F45040776A85BE893A6BCB32AF1FC714E009F3BA5DF1EBBBE1F7ECB273 |

Both actual setup executables passed extraction-only checks: prerequisite
modules match the payload, managed digests validate, and .NET is not bundled.
The extracted module from each executable passed the live positive detection
and no-reinstall prerequisite flow in Windows PowerShell 5.1, plus EN/TH
consent refusal. SnapZy's extracted-package identity, icon A, language,
shortcut/launch/settings and Help checks passed. No public upload occurred.

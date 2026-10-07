# SnapZy and Neo Snap Quality Audit

Date: 2026-10-07 (Asia/Bangkok)
Verdict: Core checks pass, but the release gate is not fully green.

## Scope

Audited the current working tree, including existing uncommitted changes, against
HEAD `141d58a91596f033910088da514b6eb369415277`. This is not an audit of only the
committed release. SnapZy and Neo Snap share the application core.

Used the installed systematic-debugging and verification-before-completion
skills. There is no dedicated tester skill in the supplied installed-skill list.
No production code, installer, installed settings, or release was changed by
this audit. Diagnostic files were added under ignored `dist/`; this report is
the only audit deliverable outside that directory. No commit or push was made.

## Findings

### 1. P2: Window capture can capture an obscuring application

Locations: `src/SnapCraft/CaptureBackend.cs:13`,
`src/SnapCraft/CaptureBackend.cs:44`, `src/SnapCraft/NativeInput.cs:93`.

The fast path requests foreground focus, waits 80 ms, and copies desktop pixels
at the target window bounds. The result of SetForegroundWindow is ignored. If
focus is refused or another application covers the target, a valid PNG of the
wrong content is returned. The window-specific fallback only runs on an
exception; wrong pixels do not cause that exception.

The existing window-fast smoke test failed twice on pixel validation. Its
failure image contained an unrelated application instead of the fixture. A
separate generated, visible, topmost-window probe passed twice. These results
show a foreground/capture reliability risk, not a proof that normal window
capture always fails. Concurrent desktop activity or fixture behavior has not
been ruled out as a contributor to the smoke-test failures.

Before release: add an occluded/failed-focus regression test, verify target
visibility, and choose a window-specific capture or an explicit failure when
the target cannot be captured reliably. Do not share the failed capture image;
it may contain unrelated private desktop content.

### 2. P2: Failed hotkey persistence leaves the live binding changed

Locations: `src/SnapCraft/MainForm.cs:173`,
`src/SnapCraft/AppSettings.cs:40`.

The handler changes the native registration and in-memory settings before
writing settings.json. If that write fails, the error handler does not restore
the previous registration or settings. The session can therefore use the new
shortcut while the next launch loads the old shortcut. The settings file is
also overwritten directly rather than replaced atomically.

Evidence: inspected control flow. A supplementary diagnostic probe for a
locked settings file did not complete and was terminated; it is not counted
as successful experimental reproduction. Normal hotkey-save tests pass.

Before release: transactional registration/persistence with rollback, atomic
settings replacement, and a regression test that denies settings writes.

### 3. P2: Video preview is Thai even when SnapZy uses English

Locations: `src/SnapCraft/VideoPreviewForm.cs:70`,
`src/SnapCraft/Assets/video-preview.html:2`.

The preview URL does not carry the selected language and the preview page is
hardcoded Thai rather than using the shared translations. Opening the actual
page with `product=SnapZy&language=en` returned document language `th` and Thai
Copy Clip / Save MP4 button text. This contradicts the English-default public
product experience.

Before release: localize the preview page and native preview messages, propagate
the selected language, and test both EN and TH.

### 4. P2: Two test harness failures and an incomplete default gate

Locations: `tests/launcher-import.cjs:6`,
`tests/installer-options.ps1:17`, `build.ps1:15`.

The import test executes only the first three launcher script lines, then
fails because URLSearchParams is absent from its VM. This is a stale harness,
not proof that the real browse-images button is broken. Native import tests
passed. Replace positional script slicing with a complete browser test.

The installer-options test contains Thai literals in UTF-8 without a BOM.
Windows PowerShell 5.1 misreads the test literals and fails its Thai assertion.
The same test passes in PowerShell 7. Production setup-options.ps1 has a UTF-8
BOM and its encoding check passes. This is a test encoding defect, not evidence
that production installer Thai labels are incorrect.

The default build script runs only the core --assets suite. Browser, installer,
video, editor and window tests must be included in an explicit release gate.

## Verification Results

Environment: Windows 11 build 26100, x64; SDKs 8.0.416 and 10.0.101;
Desktop runtimes 8.0.22 and 10.0.1. The repository has no root SDK pin.

| Check | Result | Limit |
| --- | --- | --- |
| SnapZy build with SDK 10.0.101 | Pass; zero warnings/errors | Targets .NET 8, not .NET 10 |
| Neo Snap build with SDK 8.0.416 | Pass; zero warnings/errors | Fresh SDK-8 restore required |
| JavaScript test files | 14 / 15 pass | launcher-import harness fails |
| Installer tests on Windows PowerShell 5.1 | 10 / 11 pass | installer-options test encoding |
| installer-options on PowerShell 7 | Pass | Does not erase 5.1 gate failure |
| Core assets tests, both product outputs | Pass | Local machine |
| Core suite forced onto .NET 10 | Pass | Not full desktop compatibility |
| Existing fast-window capture test | Failed twice | Foreground/fixture risk unresolved |
| Separate visible topmost-window probe | Pass twice | No occlusion scenario |
| NuGet transitive vulnerability query | None reported | Not a full security audit |

Native suites passed for image/PNG atomic-save handling, frozen header/footer
stitching fixtures, imported images, project round trips, editor close/cancel,
batch close and late-image retention, launcher language and normal shortcut
save, input latency, exit protection, scrolling Esc completion, CPU video with
system audio, countdown, recording status, and playable video preview.

Browser coverage also passed annotation effects, curves, 31 editor edge cases,
project data, toolbars, crop/view/tail behavior, frozen scrolling fixtures,
launcher controls, layer ordering, localization, SnapZy UI, stroke/numbering,
team readiness, and video-preview playback/copy/save-failure states. These are
automated fixtures, not a live Lark Sheets verification.

Measured locally: cold launcher 1,828 ms; area input 130 ms; window input 361 ms;
scroll input 154 ms; shortcut-message area input 44 ms; tray input 29 ms.
Input measurements are not end-to-end capture or encoding completion times,
and are not performance guarantees for other PCs.

The existing SnapZy-Setup-1.0.0.exe SHA-256 remained unchanged:
`5403D25C19FF56F48183F68D2575B212DC862E32B7138B3C9C6A4DD00C74525E`.

## .NET Compatibility and Lifecycle

The project targets `net8.0-windows10.0.19041.0`, publishes `win-x64`, uses
WinForms/WPF and WebView2, and is framework-dependent. Published runtimeconfig
requires Microsoft.NETCore.App 8 and Microsoft.WindowsDesktop.App 8.

Deployment therefore needs .NET Desktop Runtime 8 x64 plus WebView2. Having
only .NET Framework 4.x, Desktop Runtime 9, or Desktop Runtime 10 is not enough
under the current default runtime selection. Building with SDK 10 does not
change the application's target runtime. These distinctions follow
[Microsoft runtime selection documentation](https://learn.microsoft.com/en-us/dotnet/core/versions/selection).

As of this audit, .NET 8 support ends on November 10, 2026; .NET 10 LTS support
ends on November 14, 2028. See the
[official .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy).
Plan a .NET 10 migration with native capture, audio, WebView2, installer, and
editor regression coverage before adopting it. Do not simply enable automatic
major-version roll-forward as a substitute for compatibility testing. Local
runtime patches are older than the current supported patches; none were
automatically updated in this audit.

## Remaining Release Checks

- Fix the findings and rerun all test groups in one reproducible release gate.
- Clean-machine Windows 10/11 x64 install, upgrade, launch and uninstall.
- Missing Desktop Runtime / WebView2 and restricted-user installation paths.
- Live Lark worksheets with different frozen rows/columns; repeated captures.
- Live Excel, Google Sheets and PDF readers; confirm seamless actual pixels.
- Multi-monitor mixed DPI, minimized/occluded windows and focus rejection.
- GPU/driver recording, microphone permission/absence and combined audio.
- Disk-full/access-denied settings, export and recording destinations.
- Full .NET 10 desktop/native suite before changing the target framework.

No Windows ARM64 or x86 compatibility is claimed. No real installation or
uninstallation, user-app closure, or release publication was performed.

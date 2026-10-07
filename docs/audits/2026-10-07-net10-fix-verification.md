# Shared .NET 10 Fix Verification

Date: 2026-10-07 (Asia/Bangkok).
Scope: approved follow-up to `2026-10-07-quality-audit.md` for both products.
No version increment, commit, push or GitHub release was made.

## Migration and Product Boundaries

Both application and test projects target `net10.0-windows10.0.19041.0`, x64.
The stable SDK baseline is 10.0.100 with latest-feature roll-forward; this PC
selected SDK 10.0.101. Both installers bundle Core and Windows Desktop Runtime
10.0.12. Trimming is disabled. Framework-reference metadata pins the runtime
without incorrectly applying that version to Windows SDK reference packages.

The machine-wide Desktop Runtime is only 10.0.1, while the tested self-contained
outputs contain 10.0.12. Runtime configuration, local runtime files and licenses
were checked in published folders and in extracted installer payloads.
Separate machine-wide .NET installation is no longer required. WebView2 and
Visual C++ x64 remain prerequisites; the installer still checks them.

SnapZy remains 1.0.0, English by default, with Thai switching, icon A and Donate.
Neo Snap remains 0.1.20, Thai by default, with English switching and no Donate.
Existing install/data folders, instance IDs and registry identities are retained.

## Audit Finding Resolutions

- Window capture: removed the ignored foreground request and fixed delay. The
  desktop-copy path checks target visibility, Z-order occlusion and geometry
  before and after capture. Covered targets use bounded window rendering for
  compatible GDI windows or the existing window-specific GPU fallback. Chrome
  and WPF surfaces bypass the GDI renderer. An actual magenta-cover regression
  failed before the fix and passed with the target's blue pixels afterwards.
- Hotkeys/settings: added transactional registration and persistence, rollback
  of both in-memory and live bindings, atomic settings replacement and temporary
  cleanup. Real native hotkey registrations with a locked settings file verify
  failure preservation, replacement, disabling and successful persistence.
- Video preview: page, native title, buttons and messages follow EN/TH; language
  changes update already-open previews. Browser tests cover both languages and
  products; native tests verify playback, cancellation, file-drop copy and save.
- Test harness: replaced positional launcher-script slicing with a full browser
  interaction and corrected the PowerShell 5.1 test's UTF-8 BOM. Desktop groups
  run in isolated processes to avoid global UI/focus state leaking between tests.
- Default gate: `build.ps1` now validates both products, native desktop groups,
  browser tests and PowerShell 5.1 checks. `-CoreOnly` is an explicit fast option.
- Packaging: replaced a 90-second file polling assumption with process completion
  and exit-code checks, followed by file stability verification. IExpress rejects
  quoted SED arguments on this PC; the corrected invocation uses an unquoted
  argument and requests a short path when necessary. The corrected SnapZy builder
  completed successfully, and both actual installers were extracted and checked.

## Fresh Verification

| Check | Result |
| --- | --- |
| Default `build.ps1`, both products | Exit 0; zero build warnings/errors |
| Core tests, both products | Pass, including stitching and locked-file regressions |
| Isolated desktop groups | 22 / 22 pass, 11 per product |
| Browser test files | 15 / 15 pass |
| Windows PowerShell 5.1 checks | 12 / 12 pass |
| Explicit `build.ps1 -CoreOnly`, both products | Exit 0; zero build warnings/errors |
| Published and extracted .NET payloads | Core/Desktop 10.0.12, local DLLs and licenses pass |
| Extracted SnapZy installer | BOM, shortcuts, launch, settings, icon A, Help and identity pass |
| Extracted Neo Snap installer | Thai default, original identity/version and local runtime pass |
| NuGet transitive vulnerability query | None reported; not a complete security audit |
| `git diff --check` | Exit 0; Git emitted normal LF/CRLF conversion notices |

The default gate warns when installer artifact parameters are omitted. Those
artifact checks were performed separately here; the warnings were not ignored.
An early preview test failed clipboard restoration and opened a WinForms error
dialog. That run was not counted as passing. The owned test process was stopped;
its original clipboard snapshot was not restored. The harness now restores before
closing its last window, retries boundedly, and reports cleanup errors as failures.
Subsequent successful native runs restored their incoming clipboard snapshots.

## Local Candidates, Not Published Releases

Files are under `dist/net10-candidates/`, separate from existing release files.

| Candidate | Bytes | SHA-256 |
| --- | ---: | --- |
| SnapZy-Setup-1.0.0.exe | 82702336 | 9A6DFB3D40CDB0653B248ECE7E63951ADEF5E3C1A3FA1B52A7913152FC98C935 |
| Neo-Snap-Windows-Setup-0.1.20.exe | 82706432 | B2873B4AD9B5EC3C3A2B82F7669897A27AC1EEB03AA04F04A074796D3F049F73 |

The original `dist/SnapZy-Setup-1.0.0.exe` hash remains unchanged:
`5403D25C19FF56F48183F68D2575B212DC862E32B7138B3C9C6A4DD00C74525E`.

## Release Limits

No real installed app was replaced, and no install/uninstall flow was exercised
against the user's actual registry or startup settings. Clean Windows 10/11 x64
installation, upgrade and uninstall must still be tested. Live Lark/Excel/PDF,
mixed-DPI monitors, occluded hardware-rendered windows, GPU-driver recording,
microphone permissions and restricted/disk-full machines require manual checks.
No ARM64/x86 compatibility or performance guarantee for other PCs is claimed.
Existing Lark stitching heuristics were not changed without a fresh reproduction;
frozen-header/footer and variable-height pixel regressions pass for both products.

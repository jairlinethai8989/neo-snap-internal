# Neo Snap 0.1.20

## Installer Refresh: 2026-10-07

The user approved publication after successfully installing the corrected
package. The version remains 0.1.20. The installer is 7,938,048 bytes and no
longer bundles .NET. It requires global .NET Desktop Runtime 10.0.12 x64 or a
compatible newer stable 10.0 patch, asks before installing a missing runtime,
and preserves app/settings when prerequisite preparation fails or is canceled.

Fixed a Windows PowerShell 5.1 process-exit-code detection bug that incorrectly
reported missing .NET after successful runtime installation. Existing compatible
runtime installations are reused without another download/install prompt.

The refresh also includes the approved shared .NET 10 migration, editor Help
icon coverage, desktop/window capture fixes, bilingual setup, shortcut/launch
and editor-window closing improvements. Native/core, browser and installer
regressions passed; the corrected packaged runtime probe passed on the actual
Windows PowerShell 5.1 host. Clean-PC UAC/restart and every live capture target
have not been independently revalidated. See the private audit reports.

[Download](../downloads/Neo-Snap-Windows-Setup-0.1.20.exe) and
[verify SHA-256](../checksums/Neo-Snap-Windows-Setup-0.1.20.exe.sha256).

SHA-256: `06973DB4AC5EEA93963D71AFCF82F8B1B6A61D2386EBDB54D49FB6E364BA7717`.

## Original Layer Update

Updated 2026-10-06 (Asia/Bangkok). Developer: jairlinethai.

The editor now has four layer commands: bring to front, raise one layer, lower one layer and send to back. They apply to selected annotations and added image layers. The original capture background stays underneath the editable layers. Image ordering carries annotations owned by that image; individual annotations can also be reordered.

Selection stays on the moved item. Commands that cannot change the order are disabled and do not create history entries. Undo/Redo restores the order. PNG export, clipboard image rendering, picking and editable project files use the same object order. Existing project format remains version 1.

The previous image-only front/back controls were replaced by the common toolbar controls. About shows the new layer features. Existing installation/startup/shortcut behavior from 0.1.19 is retained.

Verified with tests/layer-order.cjs: one-step moves and extremes, retained selection, boundary history, Undo/Redo, exact overlap pixels, PNG export, editable project round-trip, picking, image ownership, icon rendering and desktop/narrow layout. editor-project.cjs and team-readiness.cjs passed. build.ps1 passed with zero build warnings/errors and all default native regressions.

Screenshots: dist/layer-order-1100.png and dist/layer-order-380.png. Installer: dist/Neo-Snap-Windows-Setup-0.1.20.exe.

This change concerns editor layers. Existing capture latency and intermittent native window-capture observations remain described in docs/performance-audit-2026-10-06.md and are not claimed resolved by this release.

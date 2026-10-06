# Neo Snap 0.1.20

Updated 2026-10-06 (Asia/Bangkok). Developer: jairlinethai.

The editor now has four layer commands: bring to front, raise one layer, lower one layer and send to back. They apply to selected annotations and added image layers. The original capture background stays underneath the editable layers. Image ordering carries annotations owned by that image; individual annotations can also be reordered.

Selection stays on the moved item. Commands that cannot change the order are disabled and do not create history entries. Undo/Redo restores the order. PNG export, clipboard image rendering, picking and editable project files use the same object order. Existing project format remains version 1.

The previous image-only front/back controls were replaced by the common toolbar controls. About shows the new layer features. Existing installation/startup/shortcut behavior from 0.1.19 is retained.

Verified with tests/layer-order.cjs: one-step moves and extremes, retained selection, boundary history, Undo/Redo, exact overlap pixels, PNG export, editable project round-trip, picking, image ownership, icon rendering and desktop/narrow layout. editor-project.cjs and team-readiness.cjs passed. build.ps1 passed with zero build warnings/errors and all default native regressions.

Screenshots: dist/layer-order-1100.png and dist/layer-order-380.png. Installer: dist/Neo-Snap-Windows-Setup-0.1.20.exe.

This change concerns editor layers. Existing capture latency and intermittent native window-capture observations remain described in docs/performance-audit-2026-10-06.md and are not claimed resolved by this release.

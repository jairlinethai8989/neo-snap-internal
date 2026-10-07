# .NET 10 and Shared Quality Fixes

Approved scope: migrate SnapZy and Neo Snap together to .NET 10 LTS and bundle
the x64 runtime in both installers. Keep existing versions, product identifiers,
data paths, language defaults, icons and donation policy. No release or push.

Framework-dependent .NET 10 would produce smaller packages but require a
separate runtime installation. Keeping .NET 8 with major roll-forward would
not constitute a tested migration. The approved design is a self-contained,
untrimmed .NET 10 publish for both products; WebView2 and VC++ remain external.

Implementation order and acceptance checks:

1. Regression tests for blocked settings writes, occluded window pixels,
   bilingual video preview, and self-contained package contents.
2. Shared target framework, SDK pin, publish and prerequisite configuration.
3. Transactional shortcut changes and atomic settings replacement; failures
   must preserve saved and active bindings and clean temporary files.
4. Window capture must never accept an occluder's desktop pixels. Keep a fast
   desktop path only for verified unobstructed windows; otherwise render the
   target window or use the existing window-specific recorder fallback.
5. Shared video preview translations, native messages and language updates.
6. Real-browser import test, PowerShell 5.1-safe test encoding, and a full
   release validation script for both flavors.
7. Builds and core/native/browser/installer tests; publish candidates into
   separate directories without replacing existing release installers.

Do not change Lark stitching heuristics without a reproduced failure. Existing
frozen-region regression tests remain mandatory. Clean-PC, live Lark, mixed-DPI
and hardware-driver tests must be recorded as limitations when unavailable.

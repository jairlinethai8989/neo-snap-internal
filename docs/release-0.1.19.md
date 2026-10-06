# Neo Snap 0.1.19

Updated 2026-10-06 (Asia/Bangkok). Developer: jairlinethai.
Release authorized by the user. Installer: dist/Neo-Snap-Windows-Setup-0.1.19.exe.

## Changes

- Checked new-install options for opening after setup, Desktop shortcuts and starting with Windows.
- Windows startup launches with --tray; the launcher stays hidden and the tray and shortcuts are available. Toggle startup through the tray menu.
- Ctrl+PrtSc is the new-install area-capture shortcut. PrtSc is selectable in shortcut settings. Existing saved shortcuts are preserved; conflicts use the existing registration error handling.
- Setup identifies older, equal and newer versions. Older versions update in place, equal versions can be repaired and newer versions block downgrade.
- A running application triggers Retry/Cancel instructions to save images, finish recording and exit. Setup does not kill the process. It checks again before replacing files.
- Existing startup and Desktop shortcut preferences are shown on upgrade. Exported work and settings are outside the replaced application directory.
- About in both launcher and editor shows short release highlights, version, developer and update date.
- Region PNG encoding runs off the UI thread. Invalid persisted capture delays are normalized; valid black screen copies no longer trigger GPU retries. Added bounded asynchronous stage timings under the application data directory's Logs folder.

## Verification

- Application and native test build passed without warnings/errors.
- Default native regression suite passed, including image/video file safety, stitching, coordinates, delay and hotkey tests.
- --tray-startup --input-latency passed: hidden initial window with live handle/tray; area, window and scroll buttons; hotkey-message routing; tray restore and minimize handling.
- Installer options, numeric upgrade decisions and completion-before-launch tests passed.
- team-readiness browser checks passed for launcher and editor. Added UI checks for Ctrl+PrtSc selection/submission and About highlights; About screenshot visually inspected.
- Installer built successfully. No existing installation was replaced during development verification.

## Remaining validation

This release does not establish that all tester-reported latency has been resolved. Cold WebView2 startup and first editor readiness still require measurements on the tester machine. In the prior audit, the native exact-window capture smoke test intermittently captured the underlying Codex window; its root cause remains unresolved. See performance-audit-2026-10-06.md. Live Lark/Excel/PDF, physical shortcut input, actual Windows sign-in startup and a full interactive install/upgrade should be checked on the target PC.

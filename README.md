# SnapCraft Capture for Windows

Standalone Windows capture app. The Chrome extension is not required. This build targets Windows 10 (2004+) and Windows 11, x64.

## Install

Run `dist/SnapCraft-Windows-Setup-0.1.0.exe`. The unsigned installer may show a Windows SmartScreen warning. The app installs for the current user and appears on the Desktop, Start menu, and in Installed apps for removal.

The current installer requires [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0), [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/), and the [Visual C++ x64 Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe). It checks these after installation and shows download links if any are missing. No administrator permission is required for SnapCraft itself.

## Use

- **Area:** drag a rectangle on the screen.
- **Window:** click a window.
- **Scroll:** drag a rectangle inside the target window, excluding fixed headers where practical. SnapCraft scrolls and stitches changed content. `Esc` or **Finish** keeps the partial image; **Cancel** discards it. Use **Manual** and scroll the target yourself when automatic scrolling cannot move it.
- **Video:** choose screen or window, select an MP4 file, then press **Stop** to save or **Cancel** to discard. There is no 30-second limit.
- Captured images open in the editor for crop, drawing, text, blur, and PNG export. Choose whether new captures open as tabs or windows in the toolbar settings.

## Verify on a real desktop

This build was compiled and its stitching and asset checks passed in a virtualized environment. Windows Graphics Capture was unavailable in that session; the Desktop Duplication fallback returned a black virtual display. MP4 capture could not start there. The following live checks remain **unverified**:

1. Open Google Sheets and Lark Sheets in Chrome. Scroll capture a range covering at least three viewports. Check that row numbers increase in order, with no duplicated bands. Repeat and press `Esc` halfway through; the partial image should open in the editor.
2. Open a long PDF in a desktop reader, then an Excel workbook. Repeat the scroll test. Try Manual when the reader or sheet does not respond to automatic input.
3. Check area/window capture, capture delay, crop auto-scroll near the editor viewport edge, PNG save/copy, and MP4 Start/Stop/Cancel on the target PC.

Automatic scrolling cannot be guaranteed for every app. SnapCraft stops after unchanged frames rather than saving a repeated viewport. If a join cannot be verified, it switches to Manual. Capturing protected or hardware-accelerated windows may be blocked by Windows or the app itself.

## Build

Run `./installer/build-installer.ps1` from PowerShell. It uses Inno Setup when available; otherwise it packages with Windows IExpress. The build requires .NET 8 SDK, the NuGet packages in `src/SnapCraft/SnapCraft.csproj`, and a writable workspace. `./build.ps1` builds and runs the synthetic checks.

# SnapCraft for Windows: Design

## Goal

Deliver a standalone Windows installer for SnapCraft. It captures the screen,
individual windows, selected regions, scrolling content, and MP4 video without
requiring a Chrome extension. The existing SnapCraft image editor remains the
editing experience.

Priority targets for scrolling capture are Google Sheets and Lark Sheets in
Chrome, then PDF readers, then Microsoft Excel for Windows. No claim is made
that automatic scrolling works in every third-party application.

## Platform and architecture

- Target Windows 10 version 1903 or newer and Windows 11.
- Use a .NET desktop shell with a compact capture toolbar and selection overlay.
- Acquire screen and window frames through Windows Graphics Capture. A region
  capture crops frames from the selected display or window in physical pixels.
- Host the existing HTML/CSS/JavaScript editor in WebView2. Replace Chrome
  storage, download, and clipboard calls with a narrow desktop bridge while
  retaining crop, drawing, blur, text, object movement, undo/redo, and PNG export.
- Publish the .NET app self-contained and package it with Inno Setup as a
  per-user Windows setup executable. The setup checks for WebView2 Runtime and
  installs or directs the user to it when absent. An unsigned build may trigger
  Windows SmartScreen; code signing helps establish publisher trust but does
  not guarantee that every warning disappears.

## Scrolling capture

1. The user chooses a target window and a scrollable rectangle, including a
   point within that rectangle where input should be sent. A short delay can be
   selected before capture starts.
2. Capture the first frame. Detect scrolling support with UI Automation and
   prefer its Scroll pattern when available. Otherwise send mouse-wheel input
   to the selected point; offer Page Down only as a user-selected fallback.
3. Wait for a stable frame, then compare the selected region with the previous
   frame. Estimate actual vertical displacement from overlapping content and
   append only new rows. Fixed headers, toolbars, and scrollbars must be outside
   the selected rectangle or removed from each tile before stitching.
4. Stop automatically at the bottom when scrolling reports its end or repeated
   attempts produce no new content. Bound attempts, elapsed time, output size,
   and memory; never spin indefinitely on identical frames.
5. Esc means finish and open the partial image. Cancel discards it. A manual
   scrolling mode lets users advance a difficult application while SnapCraft
   captures and stitches verified new content.
6. Report unsupported capture surfaces, failed scrolling, and ambiguous joins
   explicitly. Never silently save a duplicated or corrupted long image.

The desktop scroll engine works on rendered pixels and input, not page DOM or
Chrome debugger APIs. The existing stitcher is a reference, but its browser
assumptions must be isolated behind a frame/region interface.

## Video and editor flow

- Start and Stop controls record a chosen display or window without a 30-second
  cap. Cancel discards the recording. Encode H.264 video into MP4 with a Windows
  media encoder; show a clear error if the encoder is unavailable.
- Captured images open in the shared editor. The user may choose a new app
  window or an editor tab and remember that choice.
- Keep the compact toolbar out of captures. Long images remain scrollable in
  the editor, including while dragging a crop boundary near the viewport edge.
- Store intermediate captures in a per-user temporary location, clean them
  after successful export or cancellation, and avoid persistent background
  recording.

## Acceptance checks

- On Google Sheets and Lark Sheets in Chrome, capture multiple screen heights
  without repeating a viewport; Esc opens the partial result. If live Lark
  access is unavailable during development, mark that check unverified and
  provide a reproducible test procedure rather than claiming it passed.
- On at least one PDF reader and Excel for Windows, select the scroll region,
  capture to the bottom, and verify joins. When auto-scroll is blocked, manual
  scrolling still yields a valid partial or complete image.
- Selected region and window capture, delayed capture, editing, crop scrolling,
  PNG save/copy, and MP4 Start/Stop/Cancel work without Chrome extension APIs.
- Repeated unchanged frames terminate promptly with a useful message instead
  of flashing or looping.
- A clean Windows machine can install and launch the packaged app, subject to
  Windows version, WebView2 availability, and SmartScreen signing status.

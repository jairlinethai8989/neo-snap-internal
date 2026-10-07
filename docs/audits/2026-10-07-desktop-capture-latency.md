# Desktop Window Capture and Latency

## Evidence

- Reproduced the reported error by passing the actual Windows shell handle to
  `CaptureWindowAsync`: desktop screen-copy safety rejected the shell behind
  other windows, and the application-window GPU fallback returned black frames.
- Installed Neo Snap performance logs recorded failed window captures taking
  4,823-5,176 ms after selection, separate from a configured 3,000 ms delay.
- A successful area capture on October 7 recorded 3,014.7 ms configured delay,
  884.6 ms screen copy, 22.5 ms PNG save, and 938.7 ms editor readiness. The
  capture-total measurement includes time spent selecting the target; it is
  not a measurement of backend capture alone.
- Immediate capture is already the default. Existing user settings are retained.

## Changes

- Identify the desktop/shell explicitly, including Explorer-owned WorkerW
  surfaces. Normal application windows and the taskbar are not desktop targets.
- For desktop selection, highlight and capture the monitor under the click,
  including its taskbar, using native-size screen pixels and lossless PNG.
- Do not send desktop targets through application-window GPU black-frame retries.
- Recheck the window beneath each window-mode click; a cached desktop-sized
  hover rectangle must not retain a stale target when moving over an application.
- Preserve the occlusion checks and window-surface fallback for application windows.
- Highlight a nonzero delay in the launcher. Preserve 0/3/5/10-second choices,
  language switching, and the user's saved delay.

## Verification

- Red/green desktop regression: original error reproduced, then actual shell
  captures passed in both editions. Tests include native monitor dimensions,
  explicit selected-monitor bounds, off-screen rejection, and cancellation.
- Both release builds: zero warnings/errors; both core suites passed.
- Browser checks: all 16 files passed; delay states exercised for both products
  and both languages. All 12 Windows PowerShell installer checks passed.
- Neo Snap: all 12 isolated live desktop groups passed in the combined gate.
- SnapZy: the combined desktop gate stopped at `--scroll-esc` with
  `Window MP4 was not written`. That group passed when rerun separately, and
  the remaining `--editor-batch` group passed separately. The combined gate
  was not wholly green; retain this intermittent video result as a limitation.
- Visible-window capture, legitimate black regions, occluded-window correctness,
  nonactivating selection, Esc, editor tabs, projects, and input response passed
  in both editions. Backend timings are not end-to-end preview timings.
- Tests ran on one 1366x768 display. Negative-origin coordinate mapping has
  automated coverage; physical multi-monitor/DPI combinations still need testing.

No release version was incremented and nothing was published to GitHub. Local
candidate installers are separate from previous artifacts.

## Packaged Candidates

Both setups completed and were extracted without installing or changing user
settings. Both extracted payloads passed self-contained .NET 10 verification,
actual-binary desktop regression, and complete bilingual Help icon tests. The
packaged launcher script/style hashes match the browser-tested source.

- Neo Snap: `dist/capture-fix-candidates/Neo-Snap-Windows-Setup-0.1.20.exe`
  SHA256 `2B074C89383011C6A0496AF0DD9FF501EB3AADC9F64309CA8AF22AAA4CAB4E2F`.
  Extracted-binary captures: 1,138 / 91 / 81 ms. The first run demonstrates that
  cold/backend timings can vary; no fixed end-to-end preview deadline is claimed.
- SnapZy: `dist/capture-fix-candidates/SnapZy-Setup-1.0.0.exe`
  SHA256 `4851F523B0AC5527A4F106E2971A47C7723D24B98E7F1B2CE86D5532DC88A661`.
  Extracted-binary captures: 171 / 110 / 104 ms. Packaged identity, icon A,
  PowerShell BOM, shortcut retention, launch options, and English default passed.

Close the running application before manually installing a candidate. Select
Delay = None (Thai: Immediate) when evaluating response time; selecting 3/5/10
seconds deliberately adds that delay after the target selection.

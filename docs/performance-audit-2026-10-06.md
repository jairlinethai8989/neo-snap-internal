# Performance investigation - 2026-10-06

Status: development changes only, NOT release-ready. Version remains 0.1.18.
No installer was generated or replaced. User approval is required for release.

## Confirmed issues and changes

- Region PNG encoding ran on the UI thread after capture. Encoding now runs on a worker thread to avoid freezing the interface while saving large images.
- Persisted delay values were not restricted to the choices shown by the UI. Unsupported values now reset to zero; supported values are 0, 3000, 5000 and 10000 ms. Capture also caps the delay defensively.
- Successful screen copies of genuinely black content were treated as failures and unnecessarily retried with GPU capture. Successful GDI copies are now accepted; GPU startup blank-frame retries remain unchanged.
- Asset preparation no longer blocks creation of the launcher. It runs in the background while WebView2 initializes. Navigation and capture/import await preparation.
- Added bounded, asynchronous, rotating timing logs under the application data directory, Logs/performance.log. Entries contain stage, duration, process ID and version, not image content or window titles.
- Added startup, browser controller, editor readiness, capture and PNG timing points to separate startup latency from capture latency.

## Measurements

Local observations, not controlled benchmarks or a speed guarantee:

- Baseline launcher ready: 3435 ms; tray restore: 41 ms.
- Intermediate launcher ready: 2600 ms; tray restore: 32 ms.
- Latest launcher ready: 1852 ms; tray restore: 44 ms.
- An intermediate successful window test recorded 127, 133 and 136 ms including the existing 80 ms settling delay, with exact edge pixels at 742 x 462.
- Instrumentation in that run showed screen copies around 23-24 ms, asset preparation 5.4 ms, and launcher WebView2 controller creation 2094.6 ms.

WebView2 initialization is a significant observed startup cost. Cache state and scheduling differ between runs, so the launcher measurements do not establish a percentage improvement from these changes alone.

## Verification

- build.ps1: passed, zero build warnings/errors. Includes delay validation, timing log, image save/import, video file safety, stitcher/frozen headers/footer regressions, coordinates and hotkey tests.
- node tests/team-readiness.cjs: passed.
- node tests/editor-project.cjs: passed.
- Latest native --launcher --editor --exit-editors: passed (launcher/tray, editor tabs/cleanup, application exit confirmation and cancellation).
- Native --window-fast: inconsistent. Passed in an intermediate run, but failed before and after changes in other runs. Latest failure captured pixels from the Codex window rather than the synthetic target. Diagnostic output is in the test-output directory as window-capture-failure.png. Root cause is not established; do not report this test as passing.
- A proposed optimization to skip the 80 ms delay for an already-foreground window was withdrawn. The mismatch also reproduced with the original delay restored.
- The new legitimate-black-screen live assertion is not yet verified: the preceding window assertion failed before it could run.

## Remaining release gates

1. Reproduce and resolve the intermittent wrong-window native test, distinguish foreground/compositor or desktop-test conditions from capture implementation defects, and obtain repeated exact-pixel passes.
2. Run the legitimate-black-region live regression successfully.
3. Obtain tester-machine timing logs for first launch, tray restore, first capture and subsequent captures. Compare the same resolution and capture mode, excluding selection time and deliberate delay.
4. Confirm end-to-end large-image responsiveness and first-capture-to-editor latency on the tester machine.
5. Request release approval only after validation. Do not silently replace the installed app.

## Installer preservation

Existing dist/Neo-Snap-Windows-Setup-0.1.18.exe remains unchanged.
SHA256: 2BE04D71A77DC0A45A832BD484E636A7781EDA7FD4589C3C27BBF5AA164D9CAF

The existing installer does NOT contain the development changes described above.

## Follow-up: slow launch, capture button and shortcut

The installed running application was identified at LocalAppData/Programs/SnapCraft/SnapCraft.exe. Its saved DelayMs is 0, so a configured countdown does not explain this machine's report. It has not been replaced by the development build.

Added an opt-in --input-latency test. It triggers each launcher capture button through WebView2, waits for the visible selection form and cancels. It also posts WM_HOTKEY with the registered test binding ID to exercise message dispatch without sending physical keystrokes into the user's applications. Registration uses a separate test chord, not the installed application's bindings.

Latest run passed:

| Stage | Milliseconds |
| --- | ---: |
| Launcher ready | 1608 |
| Area button to selection form | 255 |
| Window button to selection form | 268 |
| Scroll button to selection form | 105 |
| Hotkey message to area selection form | 41 |
| Tray restore | 10 |

These are single-run diagnostic observations on the development build. They do not measure physical keyboard delivery, finished capture, editor readiness, actual paint completion or the tester's installed build. The first test attempt used the wrong replacement hotkey ID; the test was corrected to query the active matching ID, and the complete run then passed. No application hotkey fix was inferred from that test-harness mistake.

The latest follow-up adds tests only; it does not establish a new production performance fix or resolve the wrong-window capture regression. Startup still has measurable WebView2 cost. Do not state that the user's reported latency is resolved.

# Startup and capture responsiveness

Scope: Neo Snap 0.1.21 and SnapZy 1.0.1 development candidates. Keep the current
versions while testing; no production installer or GitHub download is replaced.

## Reproduced problems

- A prepared editor timed `editor.ready` from browser initialization, including
  idle time before the user captured an image. The regression measured a real
  image load of 219.1 ms but logged 3,632.4 ms. Existing ready timings cannot be
  used as capture latency benchmarks.
- Prepared editors bypassed the native image preview. A regression that delays
  browser image loading failed because no preview appeared. Ordinary new tabs
  already had this preview.
- A capture received during editor preparation created a new editor rather than
  consuming the pending one. The regression checks ownership, complete image
  loading, original pixels and safe closing.
- The next standby editor started preparing before the current capture was
  rendered. A second regression rejects this competing background preparation.

## Changes

- Time each image from `AddCapture` until the editor reports `editorReady`.
  Remove that clock on completion or tab removal; unrelated ready messages do
  not create duplicate image timing entries. Measure warmup separately.
- Show the native preview for both completed and pending prepared editors.
  PNG pixels and dimensions remain original; the preview's zoom affects only
  its display. Remove the preview when the editable image is ready.
- Consume a pending prepared editor instead of starting the same preparation
  again. Wait for preparation asynchronously. If preparation fails, keep the
  opened editor and retry normal image/project navigation rather than disposing
  the user's capture along with the background standby editor.
- Record selection-window setup separately from user selection time, and record
  selection-to-file including the configured countdown and settling interval.
- Defer the next standby editor until current captures report ready. Image-ready
  messages no longer incorrectly complete a separate warmup's readiness task.
- Apply the same runtime changes to the independent SnapZy source tree while
  retaining its product identity and English default.

## Timing definitions

| Stage | What it measures |
| --- | --- |
| `launcher.visible` | Main-form construction to the Shown event, not full process startup or completed screen paint |
| `launcher.ready` | Main-form construction to launcher web-page ready |
| `capture.selection-visible` | Overlay construction to Show returning, not user selection or a physical keyboard event |
| `capture.total-including-selection` | Entire capture interaction, including user selection |
| `capture.selection-to-file` | Accepted selection to output file, including deliberate delay and settling |
| `capture.after-selection` | Backend capture/file writing after deliberate delay and settling |
| `editor.warmup` | Preparing the hidden editor, excluding later idle time |
| `editor.native-preview` | Decode/copy PNG and attach the PictureBox; completed screen paint is not measured |
| `editor.ready` | Receive image to editable page ready, including pending preparation if necessary |

Measurements on this machine are observations, not guarantees. Keep resolution,
product, delay, display layout and target workload fixed for comparisons. Report
first launch and later captures separately. The input test posts the registered
hotkey's Windows message; it does not measure delivery from a physical keyboard.

## Verification and remaining limits

Focused desktop regressions cover idle-time exclusion, a deliberately delayed
browser image, 1920 x 5000 source pixels, captures received during preparation,
and recovery after preparation failure. All 11 final focused desktop groups
passed separately for both editions after deferring standby preparation.
Native preview observations were 159-188 ms for 1200 x 800 and 367-382 ms for
1920 x 5000. These measure preview attachment, not completed screen paint.

The final input tests observed 185-589 ms from a capture button to selection
setup, 79-100 ms from a posted hotkey message, and 39-60 ms for tray restoration.
Prepared images became editable in 361-618 ms; a first capture received during
preparation took 3,127-4,519 ms. The launcher web UI took 2,889-2,947 ms in these
runs. These are single-run observations, not controlled before/after speedups.

The final core/browser/installer gate passed for both editions: Release builds
with warnings treated as errors, native/core checks, 18 browser test files,
17 Windows PowerShell installer tests and framework-dependent publish checks.
The independent public SnapZy source also built with zero warnings and errors.
The gate log is `dist/performance-core-browser-installer.log`.

The broad live desktop gate is not wholly green: `--window-fast-only` captured
the generated window's pixels correctly but stopped when Chrome covered the
region fixture; `--scroll-esc` also rejected a covered/moved target. These tests
remain failures, not passing capture regressions. Target protection is retained.
Changing the fixture monitor did not resolve coverage; that test-only experiment
was removed. Full window/scroll validation needs an unobstructed desktop.

Cold WebView2 setup can still take seconds. This iteration does not establish a
percentage startup speedup or certify live Lark scrolling. Tester-machine timing
and visible response remain required before publishing a release.

## Local test packages

Candidates are built separately under `dist/performance-candidates-2026-10-08`:

- `Neo-Snap-Windows-Setup-0.1.21-PERF-TEST.exe`
- `SnapZy-Setup-1.0.1-PERF-TEST.exe`

Both candidates are 10.67 MiB. Actual setup extraction, packaged prerequisite
scripts, manifest hashes and framework-dependent .NET 10 payload verification
passed for each candidate without installing the app or runtime.
SnapZy's extracted package also passed its icon A, product/version, English
default, Help assets, shortcut retention and visible-launch checks.

| Candidate | SHA-256 |
| --- | --- |
| Neo Snap | `33CD4273BCA81F7B0F5962C6813CDA2197BC408F685A3A8835772A86976ABFDF` |
| SnapZy | `3AA8B35087F714800C1EAF5AD6200E515A8295AF59AB1A80B31F840B2418AB2B` |

The test suffix marks the file only; installed version numbers stay unchanged.
Close the app from its tray menu before installing. Use Delay = None and test
first launch, later captures, physical shortcuts, tray restoration and large
images separately. Do not publish these candidates until user acceptance and
unobstructed window/scroll validation. No GitHub push is part of this iteration.

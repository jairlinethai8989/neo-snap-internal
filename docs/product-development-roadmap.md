# Capture and productivity development

Approved scope: 2026-10-08. Applies to SnapZy and Neo Snap.

Performance is the prerequisite for every feature. Measure cold launch, tray
activation, button/hotkey-to-selection, selection-to-image and editor readiness
separately. Do not include deliberate capture delays or user selection time in
capture latency. Keep screenshots at native pixels and lossless PNG.

## Delivery order

0. Responsive startup and capture: immediate native capture controls while the
   browser loads, silent tray browser preparation, one bounded preloaded editor,
   reusable image loading and end-to-end timing. Avoid background work on the
   input path. Test cancellation, shutdown and multiple editors.
1. Scroll quality: retain verified displacement; reject ambiguous joins; report
   suspicious joins and support choosing a smaller region/retrying. Preserve
   one frozen header and final footer. Do not invent pixels at unverified joins.
2. Reports: select multiple edited captures, order them, add headings/captions,
   choose bug report / instructions / before-after layout, export PDF and Word.
3. Safe sharing: user-reviewed sensitive-data suggestions plus manual opaque
   redaction. Create a flattened new image with no hidden original assets.
4. OCR: local Windows text recognition and an optional verified Tesseract
   Thai/English model download, editable text and table TSV before clipboard
   export. Never infer invisible spreadsheet data or upload image contents.
5. Recovery/history: opt-in local editable autosaves, restore after interruption,
   retention choices and explicit deletion. Never clean active sources.
6. Comparison: choose two edited images, aligned slider and difference view,
   native-pixel comparison with a noise threshold and export.
7. Unlimited recording: no duration cutoff, disk streaming, elapsed time beyond
   24 hours, free-space monitoring and safe finalization. Remove the WAV 4GB
   ceiling from CPU audio. Preserve preview/copy/save; no video editing.

## Verification

Focused regression checks for each subsystem precede the broad product build.
Use generated documents/media, not private company data. Cold/warm performance
results are observations on the current machine, not guarantees for all PCs.
Long-running recordings, live Lark sheets and physical mixed-DPI monitors still
require target-machine testing. No Store submission is part of this work.

## Development Status

The shared application now has initial implementations of all seven areas,
plus startup/capture improvements. This is a development candidate, not a new
version or published installer. Product identities and existing defaults remain
separate. SnapZy public-source synchronization is a separate release step.

- Scroll: stable frame sampling, rejection of ambiguous joins, stored join
  boundaries and a review window showing the original capture. Stop before
  capturing or scrolling a covered/moved target. Per-segment repair and learned
  application profiles remain future work; live Lark reliability is not yet
  certified by the generated fixtures.
- Reports: headings, captions, image ordering, three PDF layouts and editable
  Word output. Word currently uses a sequential image layout, not the PDF's
  side-by-side before/after layout. Original PNG bytes are embedded in Word.
- Safe sharing: unchecked email/long-number suggestions and manual opaque
  rectangles, exported as a new flattened PNG. Review all private information;
  suggestions are not a guarantee of complete detection. The old tab remains
  unchanged, and opt-in history may still contain the original.
- OCR: installed Windows languages or optional Thai/English Tesseract models.
  Consent precedes the approximately 5 MB model download; SHA-256 and length
  must match pinned files. TSV uses visual gaps and must be reviewed before
  pasting. Models and OCR engines are not initialized on the capture path.
- History: off by default; editable snapshots every 10 seconds while idle,
  maximum 50 items / 512 MiB, retention 1/7/30 days, restore/delete/clear.
  A crash can lose the most recent ten seconds. Do not share recovery projects
  as redacted exports, since they contain original assets.
- Compare: native-coordinate alignment, slider and thresholded differences,
  PNG export. No automatic image registration or rescaling is performed.
- Recording: no application duration limit. Streams to disk, raw PCM audio
  removes the WAV 4 GiB limit, fragmented CPU MP4, total-hour timer and periodic
  disk checks. Hardware limits, disk capacity and finalization remain practical
  limits. Low-space stops or failures preserve available recording files but
  cannot guarantee recovery from hardware failure. No clip editing was added.

## Verification Observations (2026-10-08)

Measured on this development machine, excluding user selection and configured
delay; values are observations, not performance guarantees:

| Operation | Observed time |
| --- | --- |
| Main-window construction to visible native controls | 231-318 ms |
| Native image preview during editor loading | 157-240 ms |
| Prepared image to editable preview | 233-439 ms |
| Area/window/scroll button to selection | 143-287 ms |
| Registered hotkey message to selection | 58 ms |
| Tray click to launcher | 25 ms |
| Native desktop capture | 65-385 ms |
| Full cold web launcher ready | 2563 ms |

Native capture buttons are available while the full web launcher is preparing;
tray startup prepares one hidden editor without showing the launcher. This
trades a bounded background WebView for faster first capture.

Passed: warning-free builds of both product flavors, core regression suites,
all browser suites, installer/runtime policy tests, native desktop capture,
startup/hotkey dispatch, warm preview and editable recovery, real CPU video
with/without system audio, recording-to-playable-preview, report PDF preview,
manual redaction region, compare window and batch editor close.

The native covered-target regression failed before the guard and passed after
it. Two live screen-region/scroll scenarios were obscured by another window
in this interactive desktop; their foreground capture/wheel assertions could
not pass here. Keep these gates open. Do not label the full desktop release
gate as passed, and do not publish until they pass on a clear desktop. Synthetic
frozen-header/footer cases pass, but real Lark sheets, multi-hour recordings,
microphone/device changes, clean-machine OCR and Word rendering need team QA.

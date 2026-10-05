# Neo Snap: Intermittent Scroll Seams, Native Resolution, and Movable Bubble Tails

## Approved Direction

The user reports intermittent repeated horizontal bands in Lark scrolling
captures, requests sharp original-resolution screenshots by default, and
requests a movable pointer on speech bubbles. The user approved investigating
the seams first, retaining native 1:1 PNG output, adding a 100% editor view,
and dragging the bubble pointer independently of its body.

Windows application only. Preserve existing annotation effects, 5px stroke
defaults, numbered markers, recording, hotkeys and installation behavior.
Do not edit the Chrome extension or the user's sheets or document settings.

## Evidence and Uncertainty

The reported images show repeated scrollbar-like bands in one output and a
continuous table in the other. These final stitched images do not provide
the original input frames or exact capture timing. Do not claim a confirmed
live-Lark root cause from them alone.

The current stitcher detects the fixed footer only at its first successful
join and reuses that height. Existing regression cases cover stationary
footer chrome and sparse color bleed through 1-2px separators, but not a
footer that changes during the initial pair and stabilizes later. This is
a concrete hypothesis to reproduce before changing production behavior.

## Scroll Investigation and Preferred Approach

First add generated-frame regression cases for an initially changing footer,
later stabilization, scrollbar visibility changes, and thin separators. Compare
complete stitched body pixels, header once, footer once, and original width.
Include no-footer sheets and varying row heights as false-positive controls.
Failures must identify duplicated chrome or lost body pixels rather than
merely checking a detector return value.

Prefer evidence across settled frames over a footer decision based on one
early pair. If initial footer evidence is insufficient, permit re-evaluation
before permanently retaining strips. Bound any extra raw-frame retention and
release it once the boundary is established. Do not attempt to repair missing
body pixels using already-trimmed strips, nor delete arbitrary horizontal
lines from the final image: sheet row borders and text are legitimate content.

Alternatives considered: a fixed bottom crop is faster but loses content on
other applications; a larger fixed delay is simpler but does not establish
stability and makes every capture slower. Neither is the default solution.

Implement only the correction supported by the reproducing tests. If a join
cannot be verified, keep the existing partial-image/Manual behavior rather
than knowingly append an incorrect viewport. Esc finishes and retains the
partial capture; Cancel remains a separate discard action. Do not silently
jump to the top of the document or change zoom/frozen rows in Lark.

Diagnostic metrics may record frame sizes, displacement, candidate footer
height and accept/reject reasons locally. Do not automatically retain raw
screenshots, private sheet text or upload anything. If generated tests cannot
explain the report, state that limit and request explicit user assistance for
the smallest additional reproduction data needed.

## Native Screenshot Resolution

The primary screen-copy path currently creates a bitmap matching the selected
screen rectangle and copies those pixels directly. PNG save is lossless.
The editor currently scales its CSS display to fit; that is not a PNG resize.
There is no screenshot quality/resolution preference in existing settings.

Keep original physical-pixel dimensions as the default for area, window and
scroll captures, with no lossy encoding, downsampling or artificial upscaling.
Audit GPU fallback coordinate mapping and per-monitor DPI handling with
controlled tests. Correct dimension or mapping errors if reproduced; do not
invent extra detail or promise capture beyond what Windows exposes.

Add a compact Fit / 100% segmented view control to the editor with active
state, keyboard access and tooltips. Fit remains the initial viewing mode so
large images do not overwhelm the editor. 100% displays one image pixel per
CSS pixel and enables scrolling in both directions. WebView/window scaling
may still affect physical display size; this is an image zoom level, not a
guarantee of one physical monitor pixel per CSS pixel.

Changing view mode must not modify canvas backing dimensions, annotations,
crop, undo history, dirty state or exported PNG pixels. Preserve the viewport
center where practical during zoom changes and keep drawing/hit testing
correct at each zoom level. Do not add a redundant screenshot-quality setting
that merely relabels the existing lossless default.

## Speech Bubble Pointer

When a speech bubble is selected, show a distinct draggable handle at its
pointer tip, separate from the body resize handle. Dragging changes only the
tip, not the body or text. The attachment follows the nearest suitable edge
of the rounded body so the pointer can extend left, right, above or below.
Keep the attachment clear of rounded corners and avoid inverted/self-crossing
paths when the tip is near the body. If dragged inside the body, use a short
valid pointer outside the nearest edge rather than corrupting the outline.

Store the tip in image-pixel offsets relative to the body origin. Moving the
whole object moves the tip with it. Resizing changes the body/wrapping without
stretching the font; retain the tip location relative to the origin and
recompute its attachment. Legacy bubbles without tip properties retain their
current lower-left shape until edited.

Clamps during dragging respect the current crop's image bounds. Hit testing,
selection extents, movement and shadow composition must account for both body
and extended tip while keeping body-resize geometry distinct. Double-click
text editing must preserve the customized pointer. One pointer drag creates
one undo entry and participates in existing interruption cleanup. Pointer
handles/selection decorations never appear in PNG copy/save.

## Architecture and Verification

Keep image-boundary matching in the stitcher/capture boundary, bubble geometry
in the editor's annotation helper, and view zoom in the editor view state.
Do not broaden the work into codec, capture-backend or UI redesigns.

- Observe failing behavior before each production fix or new feature.
- Native tests cover temporal footer changes, exact stitched pixels, no-footer
  false positives, output dimensions and capture-coordinate mapping.
- Browser tests cover Fit/100% backing/export invariance, long-image scrolling,
  pointer drag to all four sides, move/resize/text edit, crop offsets, undo/redo,
  interrupted gestures, shadow extents and exported pointer pixels.
- Inspect screenshots on desktop and narrow viewports; keep controls compact.
- Run existing browser and native regression suites and build checks before
  packaging the next Windows installer. Verify package identity and report
  unavailable live-Lark or multi-monitor verification explicitly.
- Use synthetic fixtures; do not install the build or close the user's real
  application automatically.

## Written Review

This document records the approved direction and defines precise pointer and
zoom behavior. Obtain user review of this written spec before implementation.

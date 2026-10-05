# Neo Snap: Team Release Roadmap

Updated: 2026-10-05
Baseline: 0.1.11
Status: approved; phase 1 implemented and checked in 0.1.12; phase 2 started
with installer completion visibility. Other phase 2 prerequisites remain next.

## Release Strategy

Improve one subsystem at a time. Preserve the scrolling capture behavior that
the user confirmed working in 0.1.11. Do not bundle unrelated UI changes or
capture-engine refactoring into editor safety work.

Each phase must have focused tests, an explicit account of remaining risks,
and a user-verifiable result before proceeding. Do not label an untested
installer or hardware-dependent feature as release-ready.

## Phase 1: Protect Images When Closing

Priority: before pilot distribution.

Current evidence: EditorHubForm.ClosePage removes the tab and deletes its source
capture without asking. Closing the editor window also deletes open captures.
The editor starts a PNG download without tracking its successful completion.

Scope:

- Ask Save / Discard / Cancel for a new capture or unsaved edits.
- Handle the tab close icon, editor close command, window close, and application
  exit while images are open.
- Keep the tab and its image if saving fails or the Save dialog is canceled.
- Mark a saved state only after a successful, completed file write.
- Detect edits after saving, including crop, text, style changes, move/resize,
  clear, undo, and redo. Tool selection and viewport scrolling are not edits.
- Confirm each affected image when closing multiple tabs. Cancel must stop the
  remaining close operation; already completed saves remain valid.
- Do not silently discard images if the embedded editor cannot respond.

Approved default: a successful clipboard copy counts as keeping that exact image
state, with a new warning if edited afterward. A failed copy never counts.
Clipboard copy is not a persistent backup or crash recovery.

Acceptance:

- Cancel leaves image, edits, and source file intact.
- Discard closes only the intended tab and releases its temporary source file.
- Save closes only after success; cancellation/failure leaves the tab open.
- Save or copy followed by another edit produces a new warning.
- Closing multiple images cannot skip an unkept image.
- Existing editor and scrolling regression tests continue to pass.

Approach choices:

1. Always prompt: simple but interrupts users even after a successful save.
2. Track kept image state and prompt only when needed: recommended; requires a
   small WebView/native state contract and actual export completion tracking.
3. Persist complete editing sessions first: stronger crash recovery, but adds
   disk privacy, retention, and migration concerns. Defer to phase 4.

## Phase 2: Make Installation Predictable

Priority: before pilot distribution.

- Check .NET Desktop Runtime, WebView2, and native runtime prerequisites before
  launching the installed application.
- Make prerequisite behavior consistent across installer build paths.
- Clearly report recording backend availability, including the external
  FFmpeg fallback, before a user starts recording.
- Provide approved prerequisite installation steps for team computers.
- Check fresh install, upgrade, uninstall, and preservation of user settings.
- Coordinate dependency distribution and third-party notices with team IT.

Acceptance: a clean test machine can reach capture and the editor through a
documented installation flow; missing recording prerequisites produce a clear
action, not a late generic recording failure.

## Phase 3: Useful Errors and Safe Recording Recovery

Priority: before pilot distribution.

- Use concise Thai error messages with an actionable next step.
- Give capture, recording start, recording stop, and save failures identifiers.
- Provide a diagnostic export containing version, runtime/backend status, and
  relevant logs. Do not include screenshots, document contents, or audio by
  default; review paths and window titles for sensitive information.
- Surface preserved recording files and offer recovery without overwriting
  existing files.
- Verify stop, cancel, countdown, permission-denied saves, and low disk space.

Acceptance: failures retain recoverable work where possible, and a user can
provide a useful, privacy-reviewed report without copying a large error popup.

## Phase 4: Privacy, Retention, and Crash Recovery

Priority: retention before wider rollout; recovery after its design is approved.

- Inventory screenshot originals, recording intermediates, editor data, and logs.
- Add explicit cache clearing and configurable retention with safe defaults.
- Never clean sources used by an open editor or an active recording.
- Keep saved user files outside automatic cleanup.
- Separate explicit solid redaction from translucent annotation and blur.
- Design opt-in editing-session recovery together with retention and deletion;
  do not introduce permanent original-image copies as an incidental backup.

Acceptance: users understand what remains on disk, can clear inactive temporary
data, and cannot accidentally delete an active capture or a saved output.

## Phase 5: Small Team Pilot

Priority: before declaring the release ready for the full team.

Run a pilot with 3-5 people on more than one machine. Record version, device,
display scaling, application, steps, expected result, and actual result.

Matrix:

- Region and window capture, including first capture after launch.
- Lark sheets with different row heights and frozen panes; Google Sheets,
  Excel, and PDF viewers.
- Scrolling to the end and finishing with Esc; Cancel remains discard.
- 100%, 125%, and 150% display scaling; multi-monitor layouts where available.
- Video: silent, microphone, system sound, and both; available GPU/CPU backends.
- PNG save/copy, long-image crop, repeated editing, and safe close.
- Taskbar/tray activation, repeated launch, shortcut conflicts, and upgrades.

Acceptance: no unresolved work-loss defect in tested workflows; critical
capture/recording failures have a fix or an explicitly documented limitation.
Keep untested configurations visible rather than treating them as passes.

## Phase 6: Trusted Distribution and Updates

Priority: before broad distribution.

- Agree with team IT on installer signing and trusted distribution.
- Show publisher, version, update date, and support contact consistently.
- Preserve settings during updates and provide a rollback path.
- Verify update provenance/integrity before offering automatic updates.
- Include a brief Thai quick-start guide and known limitations.

Acceptance: the team can identify the installed version, install an approved
release, and return to the previous stable release without losing settings.

## Work Log

- 2026-10-05: inspected current editor close/export behavior, installer paths,
  temporary capture handling, and existing editor tests. Recorded the roadmap.
- 2026-10-05: user approved the roadmap and added installer visibility,
  truthful activity status, and compact launcher requirements.
- 0.1.12: guarded close/save, exact exported-state tracking, atomic PNG output,
  and application-exit confirmation implemented. Focused Windows tests cover
  Cancel, save-dialog cancellation, Discard, newly arriving images during close,
  and canceling application exit. Loading must finish before disposing its
  WebView, avoiding a close hang when a new capture arrives during shutdown.
- 0.1.12: PowerShell setup no longer launches the app; IExpress has no duplicate
  final notice. Inno launch is opt-in. Launcher is 380 x 156 logical pixels,
  expands temporarily for dialogs, is not permanently topmost, and clears stale
  capture status. Browser layout/status tests and native launcher checks pass.
- Next: consistent prerequisite checks and guided installation on clean team
  PCs. Crash recovery, retention controls, signing, and automatic updates remain
  pending their respective phases. Full installer upgrade on the user's PC and
  target-device capture/recording checks are not claimed as automated passes.
- Optional real EXE relaunch test (`--single-instance`) failed in setup with
  "Test launch host did not receive the click", before relaunch behavior could
  be measured. Do not count taskbar/EXE focus restoration as verified in this
  increment. Native tray activation and minimize restoration did pass.

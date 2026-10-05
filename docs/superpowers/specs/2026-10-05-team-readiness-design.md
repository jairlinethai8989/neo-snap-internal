# Neo Snap: First Team Readiness Increment

Approved scope: roadmap phases, plus installer completion visibility, truthful
capture status, and a smaller launcher requested on 2026-10-05.

## Design

- Do not automatically launch Neo Snap from the PowerShell/IExpress installation
  flow. Keep one completion notice. Inno's post-install launch is opt-in.
- The launcher is a normal foreground window, not permanently topmost. Its
  compact client area is 380 x 156 logical pixels; dialogs temporarily expand
  it to 380 x 320 and closing the dialog restores compact dimensions.
- Keep mode selection visible, but distinguish selected mode from actual busy
  capture. Clear stale capture success on returning to the launcher. Only an
  actual started operation displays the working state.
- Protect every image when closing a tab, the editor, or exiting the app.
  New captures are unkept. Ask Save / Discard / Cancel only for an unkept image
  state. Successful save or successful clipboard copy keeps that exact state;
  editing afterward makes the current state unkept again.
- Save completion is confirmed by a native file write, not by clicking the
  export button. Dialog cancellation and write failures never authorize close.
- Export the flattened PNG and its exact editor snapshot together. Write to a
  temporary sibling and replace the chosen output only after the write succeeds.
  Mark only the exported snapshot kept, even if another edit occurs during export.
- Re-entrant close/save requests cannot discard an image or open duplicate dialogs.
- Crash recovery and automatic updates remain separate roadmap phases.

## Implementation Plan

1. Add regression tests for installer success flow, compact launcher geometry,
   stale/busy status, and editor kept-state semantics. Observe expected failures.
2. Fix the installer launch ordering and launcher presentation/status. Verify.
3. Add editor snapshot/export contract and native guarded close/save lifecycle.
4. Integrate application exit with open editors; cancel exit if any image remains.
5. Run default native tests, editor/launcher browser regressions, and focused
   native close/save scenarios. Inspect compact/dialog screenshots.
6. Prepare version 0.1.12 installer and update roadmap with passed checks and
   remaining hardware/install limitations. Do not install it on the user's PC.

## Acceptance

- Completing setup never starts a topmost app before installer dismissal.
- Compact launcher controls fit without overlapping; dialog controls are reachable.
- Opening the launcher without capturing shows no capture-success claim.
- Cancel close/save preserves source and edits; Discard affects only intended tab.
- Successful copy/save does not warn until the image differs from the kept state.
- Undo back to the exported snapshot does not warn; redo to newer edits does.
- Failed copy/save, pending text edits, and unloaded editor are never silently lost.
- Lark stitching code is unchanged and its existing regression suite stays green.

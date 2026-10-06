# Implementation Plan

1. Add failing editor behavioral tests: numbered-marker deletion/undo/clear,
   2px defaults, per-object patterns, live changes, undo, rendered gaps.
2. Implement stroke controls and derive numbering from remaining objects.
3. Add failing native clip-store and preview tests. Add generated-media
   browser tests for preview playback and copy/save bridge states.
4. Implement local preview, file clipboard/drag, atomic save, guarded close,
   retained-file cleanup, and recording-stop integration.
5. Run all existing browser suites, native build/default tests and focused
   editor/launcher/preview tests. Check responsive screenshots.
6. Build the next Windows installer and report verified behavior and any
   real-client compatibility still requiring user testing.

The writing-plans skill is not installed; this plan follows the approved
specification and the repository's existing test and implementation patterns.

# Annotation Effects Implementation Plan

The approved spec is 2026-10-05-annotation-shadows-speech-design.md.
The writing-plans skill is not installed in this environment; this plan
records the implementation and verification steps directly.

1. Add browser behavior tests and observe failures before production edits:
   actual 5px strokes, annotation shadows, text outlines, bubble editing and
   resizing, color isolation, undo and export.
2. Add bounded effect composition and text/bubble layout helpers. Integrate
   them with existing object rendering, selection and history.
3. Add compact shadow settings for all annotation tools, contextual text
   outlines and bubble styling. Reuse the existing color palette.
4. Integrate bubble creation, double-click editing and size-independent
   wrapping. Update default-width expectations for Windows only.
5. Run browser regression suites, inspect desktop/narrow screenshots and
   verify native editor integration and the full native build.
6. Update Windows release metadata to 0.1.14, package the installer, verify
   output hashes and provide installer plus a verification report. Do not
   install or alter live user documents, chats or clipboard contents.

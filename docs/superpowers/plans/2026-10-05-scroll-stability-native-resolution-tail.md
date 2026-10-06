# Implementation Plan: Stable Scroll Joins and Editor View/Pointer Controls

Approved spec: 2026-10-05-scroll-stability-native-resolution-tail-design.md.
The writing-plans skill is unavailable; this plan records the steps directly.

1. Add failing exact-pixel native cases for an initially changing scrollbar
   footer that becomes stationary later. Observe output failures before edits.
2. Correct footer learning only where supported by those failures. Retain
   bounded look-behind rows with novel strips so a later boundary can recover
   body pixels without keeping every full frame. Preserve memory limits.
3. Add failing browser tests for Fit/100% export invariance and bubble tip
   gestures on all four edges, history, crop and object movement.
4. Add bubble pointer geometry and separate tip/body handles; integrate
   with existing rendering, shadows, history and interrupted-gesture cleanup.
5. Add view-only zoom and verify native pixel mapping. Do not change capture
   resolution or encode quality without a reproduced mapping failure.
6. Run complete native/build and browser regressions, inspect screenshots,
   package version 0.1.15 and verify published files. Report live-Lark and
   multi-monitor limits honestly. Do not install automatically.

## Verification Outcome

Implemented and packaged as 0.1.15 on 2026-10-05. Initial changing-footer
fixtures failed with repeated footer pixels before the correction; variable
row heights plus an initially hidden footer also reproduced an incorrect
478px displacement instead of 150px. Both now preserve exact body pixels.
Footer boundaries are re-evaluated, strip look-behind is bounded, and stationary
bottom pixels cannot vote for an incorrect displacement at the old endpoint.

Browser RED cases initially lacked 100% controls and the tip handle. Four
view/pointer groups now pass, including a separately reproduced crop-edge
tip escape. Existing annotation, stroke/numbering, editor edge-case, toolbar,
launcher, kept-state, frozen-scroll and video-preview suites pass. Native
build/default and editor/launcher/exit checks pass. A controlled visible test
window preserved screenshot dimensions and exact colored edge pixels.

Published DLL/assets and staging ZIP entries match the tested files. Installer
0.1.15 was delivered without running installation. Live Lark temporal behavior,
physical mixed-DPI/multi-monitor configurations and clean-PC installation remain
pilot checks, not automated passes. Extra raw rows are retained only in memory
until the capture is disposed; no supplied private sheet data is packaged.

# Editor Help Icon Fix

Date: 2026-10-07. Applies to SnapZy and Neo Snap through their shared editor assets.

## Reproduction

The guide copied only an SVG from the first control described by each entry.
Home, About, language, stroke width, stroke patterns, opacity, shadow, text
outline and actual-size view have no SVG on their text/slider/checkbox controls.
All nine entries consequently had an empty icon column in both EN and TH.
The palette copied its menu arrow rather than a palette symbol. The numbered
marker copied its circle but lost the separate numeral overlay.

Before changing production assets, the new real-browser regression failed with
all nine missing icons for each product/language combination. A second run
also reproduced the missing numeral and incorrect palette symbol.

## Fix

- Added an inert HTML template of Lucide symbols for the non-icon controls and
  the palette. The existing asset builder discovers these literal declarations.
- The guide uses those symbols when appropriate, otherwise copying the existing
  toolbar SVG. It initializes newly inserted Lucide placeholders after rendering.
- Preserved the numbered marker's numeral in a fixed, relative-positioned icon
  wrapper. Filled shapes retain their filled styling; outline shapes stay hollow.
- Regenerated only the Windows icon bundle: 50 icons, 12896 bytes. App logos and
  extension outputs were not regenerated. No drawing behavior was changed.

## Verification

- `node tests/editor-help-icons.cjs`: pass for both products and both languages.
  Every guide entry has nonempty SVG geometry and stable display dimensions.
  Checks include numeral alignment, filled/outline distinction, palette semantics,
  language changes while the guide is open, compact viewports and Esc/tool retention.
- The same browser test passed against both actual built `Assets` directories.
- Both corrected installers built successfully. Each was extracted without
  installation; the same Help regression passed against the extracted assets.
  Core/Desktop Runtime 10.0.12 and product identities also passed verification.
- `node tests/snapzy-ui.cjs help`: pass; toolbar descriptions and language coverage
  remain intact, and closing Help preserves the selected drawing tool.
- `tools/test-release.ps1` without `-Desktop`: exit 0, both product builds with
  zero warnings/errors, both core suites, 16 browser files and 12 PowerShell checks.
- Visual inspection of rendered EN/TH screenshots confirmed readable alignment
  and complete icons in the formerly empty file/style rows.

The default desktop gate failed at `NeoSnap --window-fast-only`: its generated
region fixture was covered by an existing LINE window. The isolated retry failed
for the same obstruction. Target-window captures themselves passed their pixel
checks, but the region assertion correctly refused to capture the wrong window.
No user window was closed or moved, and no assertion was weakened. Therefore a
fully passing desktop gate is not claimed for this turn. Clean-PC installer and
live capture checks remain separate release requirements.

## Delivery

Candidate installers are built separately under `dist/editor-help-candidates/`.
The previous release and .NET 10 candidate directories are retained. Versions
remain SnapZy 1.0.0 and Neo Snap 0.1.20; no GitHub publication or push is performed.

| Candidate | SHA-256 |
| --- | --- |
| SnapZy-Setup-1.0.0.exe | 0A4544F85D37948B0B5395D7E18253A76E6CB01065FD0704DDA43962F5FC6090 |
| Neo-Snap-Windows-Setup-0.1.20.exe | 303CBD27797CA79C01B191A9B61D42750135C150D2A7C58F54BC881A7844B561 |

The previous SnapZy release and .NET 10 candidate hashes were verified unchanged.

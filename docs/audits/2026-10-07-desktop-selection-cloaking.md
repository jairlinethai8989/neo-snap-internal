# Desktop selection: DWM-hidden targets

## Symptom and evidence

Window capture on exposed Desktop still reported an unsafe desktop copy and a
black frame after the first-stage Desktop backend fix. The running Neo Snap DLL
matched the first-stage candidate SHA-256
`682DA8D5F4ED5296E293D93252E738CF615F3C09FF6938A75EB0582AC7251F3C`.
This was not an outdated installation.

The installed application's timing log recorded `capture.window`, not
`capture.desktop`, followed by roughly 5.1 seconds after selection. Metadata-only
inspection found several `Windows.UI.Core.CoreWindow` / `ApplicationFrameWindow`
targets with WS_VISIBLE, full-monitor rectangles, and DWM cloak state 2 ahead of
Explorer's Desktop in Z-order. No window titles, document text, or user screen
images were collected by the inspection tool.

`WindowBelow` previously enumerated visible-style rectangles without excluding
DWM-cloaked windows. It selected an invisible app instead of Desktop. The same
visibility check could falsely mark a normal window as obstructed and bypass the
fast screen-copy path.

Microsoft documents the difference between visible style and DWM cloaking:
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-iswindowvisible
- https://learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute

## Scoped change

- `IsCaptureTargetVisible` excludes minimized and DWM-cloaked windows.
- `WindowBelow` uses this shared predicate, as does existing occlusion checking.
- Desktop classification, native monitor dimensions, and the safe fallback for
  genuinely covered application windows remain unchanged.
- No version bump, Git push, public release, or user settings changes.

## Regression evidence

`CloakedSelectionTest` creates a visible-style but DWM-hidden magenta window over
a generated blue target. It checks selection, actual overlay click, obstruction
checking, and exact native capture pixels. The stabilized fixture fails against
the extracted first-stage application DLL with:

`Window selection chose the DWM-hidden phantom instead of the displayed target.`

The same test passes with the new application DLL. It moves/closes only owned
test windows and restores the cursor; user applications are not moved or closed.

`DesktopSelectionTest` is an additional opt-in `--desktop-click-only` group. It
requires an exposed Desktop point and drives the actual selection overlay and
coordinator. It does not silently skip when other applications cover Desktop.
Temporary screen captures are deleted and never displayed or exported.

The earlier audit's one-monitor assumption was incomplete. Current metadata shows
two monitors: 1366x768 and 1920x1080, with a negative vertical origin on the second.
Backend tests retain selected-monitor dimensions on both. Backend timing alone is
not a measurement of editor preview readiness.

## Verification Status

Initial combined desktop gates stopped on an
obscured region fixture, then on a scroll fixture that stayed at one viewport.
Both fixtures were on a monitor with another application's topmost window.
Their generated windows now use the second monitor; the new cloak fixture also
explicitly positions its owned windows. These failed runs are not counted as
successful release gates.

The final `tools/test-release.ps1 -ProductFlavor Both -Desktop` run exited 0:
both Release builds had zero warnings/errors, core tests passed, all 13 isolated
native desktop groups per product passed, all 16 browser test files passed, and
all 12 Windows PowerShell 5.1 installer/deployment checks passed. This includes
covered-window safety, video recording and preview, window/scroll selection,
projects, launcher/hotkeys, editor Help, batch close, and scroll/Esc capture.
No production changes were needed for the scroll fixture; its second-monitor
rerun advanced by 230px per tile to a 961px partial output in both products.

Final backend Desktop timings were 89-152ms in Neo Snap and 90-143ms in SnapZy.
These are PNG backend timings, not cold editor preview or input-to-preview times.
Both installers were rebuilt in `dist/desktop-selection-candidates` without
overwriting the first-stage candidates. Both extracted payloads passed .NET 10
self-contained runtime checks, the cloaked-selection regression, Desktop
backend tests on both monitors, and bilingual/product-specific editor Help icon
checks. The tests loaded the actual packaged `SnapCraft.dll`; only harness files
were copied into the extracted QA directories. Packaged DLL hashes matched the
published DLLs. SnapZy's setup-specific BOM, icon A, shortcut, launch, settings,
and identity checks also passed.

Packaged backend timings: Neo Snap 83-147ms, SnapZy 71-136ms. No automatic
installation, version bump, signing change, Git push, or public upload was done.

Candidate SHA-256 values:
- Neo Snap installer: `BC23338C12B845955B8680C47609E9BE1BBFE682548CA94E030F08D78524D5F2`
- Neo Snap core DLL: `C552986415E710D50E2131AB27C8109CA661BC4451BCC16884769DA9E0AFF9CE`
- SnapZy installer: `CD111D48D266A768BBE0600E5566E0BBD97DAB278044891D4180A6D536BE72D3`
- SnapZy core DLL: `52F757A1402BEEEC68C025998E56CF7677F4472F7909CED46358372F3A47B24E`

Neo Snap's installer is 82,706,432 bytes; SnapZy's is 82,702,336 bytes.
The display versions remain Neo Snap 0.1.20
and SnapZy 1.0.0; these are new local test candidates, not the old binaries.

Actual bare-Desktop click validation remains pending while both monitors are
covered by user applications. The opt-in test was executed and exited 1 with its
explicit precondition failure (no exposed Desktop point), not a capture failure.
A request to expose Desktop was sent to the user; no user application has been
minimized to manufacture that condition.

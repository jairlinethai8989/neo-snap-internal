# Neo Snap: Annotation Shadows, Text Outlines, and Speech Bubbles

## Approved Direction

Windows application only. Change the initial stroke-tool width to 5 image
pixels. Add optional text outlines, optional shadows for every annotation
type, and editable, movable speech bubbles. Preserve the working capture,
scroll stitching, recording, video preview, and numbered-marker workflows.

The user clarified that a text outline is a border around the letters, with
its own enable checkbox, color, and adjustable thickness. It is not shadow
spread. The user approved compact contextual controls and requested that
the shadow checkbox work for all annotation items, not only text.

## Controls and Defaults

- Pen, straight line, arrow, outlined rectangle, and outlined ellipse start
  at 5px. Preserve the existing independent defaults for text, numbers,
  filled shapes, and the highlighter, and preserve solid/dashed/dotted styles.
- A shared Shadow checkbox is available for every drawing tool or selected
  annotation, including highlighter, filled shapes, numbered markers, and
  blur regions. It does not affect the captured background, crop mask, or
  selection decoration. Shadow starts disabled.
- Shadow has an independent color, blur, and horizontal/vertical offsets.
  Initial settings are black, 6px blur, and 3px offset on each axis. A compact
  settings popover keeps the main toolbar small. Use accessible controls,
  visible enabled states, Thai labels/tooltips, and the existing palette.
- When an item is selected, settings change only that item and redraw live.
  Without a selection, settings configure subsequent annotations. Retain
  the shared shadow preset across tool changes during the editor session.
  Existing items do not change merely because the user switches tools.
- Text and speech-bubble text have a separate Outline checkbox, color, and
  thickness control. Outline starts disabled, black, at 2px; allow 1-16px.
  Outline and shadow can be enabled independently or together. Text-specific
  controls are only visible when placing or selecting text or a speech bubble.
- Keep opacity, fill color, and line width separate from shadow/outline
  settings. Changing shadow color must not change the item's primary color.

## Speech Bubbles

Add a speech-bubble tool next to Text using a bundled Lucide message icon.
A click opens the existing text-entry workflow. A nonempty confirmation
creates one object containing a rounded rectangular bubble, a lower-left
speech tail, and text. Empty or canceled entry creates no annotation.

Use a white bubble fill, the current primary color for its border and text,
and a readable 24px initial font. Keep fill and border colors separately
editable. Its initial body is 260 image pixels wide, constrained to the
available image width, with padding and enough height for the text. If the
image is too narrow, clamp the body width to the image and reduce padding
without allowing a negative text area.

The selection tool moves the entire bubble and tail together. Its resize
handle changes the bubble's body width and requested height without stretching
the font. Wrap text to the inner width, including long unbroken words and
explicit newlines; grow the minimum body height to avoid hiding text. The
tail remains attached at a proportionate lower-left position. Double-click
edits text while preserving position, style, and width. Independent tail
editing and multiple bubble shapes are outside this increment.

## Rendering and State

Keep annotations in the editor's existing object list so move, delete,
undo/redo, clear, dirty-state checks, and PNG copy/save all share one model.
Store shadow, outline, and bubble properties on the relevant objects. Missing
properties on legacy objects mean effects are disabled, not new defaults.

Use one rendering path for canvas preview and PNG export. Apply each item's
shadow to its final silhouette, avoiding accumulated shadows from separate
fill/stroke/text passes. Ensure state cannot leak to later objects or selection
decorations. For blur annotations, shadow may appear outside the region's
perimeter, but must not reveal original pixels inside the protected region.
Respect existing crop coordinates and canvas clipping at image boundaries.

Put effect rendering and bubble text layout into small editor-specific helpers
if needed, instead of expanding unrelated capture/native modules. Calculate
geometry separately from decorative shadow extents so dragging and resizing
remain predictable. No full-size scratch canvas per object per pointer event;
use bounded temporary surfaces where compositing is necessary.

Live slider changes make one undo entry per interaction. Text confirmation
and bubble creation each make a single history entry. Restore every style
field through undo/redo and synchronize controls when selecting another item.
Committing pending text before copy/save/close must include all new effects.

## Verification

First add failing behavioral tests for 5px defaults, independent text outline
controls, shadows on all annotation kinds, and bubble creation/editing/moving.
Verify actual canvas/export pixels, not only object property values.

Cover shadow on/off, color/blur/offset updates, disabled legacy effects,
selection synchronization, fill/text/shadow color independence, outline width,
undo/redo, canceled entry, multiline and long-word wrapping, bubble resize,
crop offsets, and pending-text export. Verify no shadow-state leakage and no
loss of blur protection. Check long-image interaction and compact/responsive
toolbar layouts with screenshots. Rebuild the used-icon bundle for Windows
only and verify that the new tool icon renders.

Run existing editor regression suites and native build/tests before packaging
the next Windows installer. Preserve browser-extension behavior and defaults.
Use generated test images, do not modify the user's documents or install the
new build automatically. Report any unavailable verification explicitly.

## Review Gate

This document records the agreed behavior, including the expanded shadow
scope. Obtain the user's review of this written spec before implementation.

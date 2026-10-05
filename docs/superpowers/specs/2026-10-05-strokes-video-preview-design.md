# Neo Snap: Stroke Styles, Numbering, and Video Preview

## Approved Direction

Windows application only. Preserve the working scrolling-capture engine and
the existing recording backends. The user approved an in-app video preview
instead of opening an external player, and added numbering after deletion.

## Drawing Tools

- Add solid, dashed, and dotted segmented stroke controls with visual line
  samples, Thai tooltips, keyboard access, and an unmistakable active state.
- Apply patterns to freehand pen, straight line, arrow shaft, rectangular
  outline, and elliptical outline. Arrow heads remain solid.
- Default these stroke tools to 2 image pixels. Preserve usable defaults for
  text, numbered markers, and the highlighter; filled objects are not strokes.
- Store pattern on each object alongside width, color, and opacity. Changing
  a selected object's pattern redraws immediately and creates an undo entry.
  Remember each drawing tool's current style during the editor session.
- Pattern controls are disabled for tools without an outline. Exported PNGs
  use the same rendering as the editor, without selection decorations.

## Numbered Markers

Compute the next marker as one greater than the highest number among remaining
number objects; start at 1 if none remain. Do not renumber existing objects.
Deleting marker 5 from 1-5 makes the next click produce 5 again. Deleting 3
while 5 remains makes the next marker 6. Undo, redo, and clearing must also
affect the next number correctly. A new capture has its own sequence.

## Video Workflow

1. Keep the current audio choice, countdown, recording timer, and Stop action.
2. Stop and finalize the MP4, releasing the recorder's file handles first.
3. Open a compact, resizable in-app preview using the existing WebView2
   environment. Show the actual video with playback, seeking, and volume.
4. Provide Copy Clip and Save MP4, with busy, success, and failure states.
   Do not open a Save dialog automatically when recording stops.
5. Copy the MP4 as a Windows file-drop clipboard entry, not an image, text
   path, or base64 string. Keep its backing file available after preview closes.
6. Allow dragging the clip to another application as a fallback for clients
   that do not accept pasted files. Never select a recipient or send a message.
7. Save through an explicit Save As dialog. Canceled or failed saves preserve
   the clip and the preview. Avoid exposing partially written destination files.
8. Before closing an unkept clip, offer Save, Discard, and Cancel. A successful
   Copy or Save permits closing. App exit must respect pending previews.

## Storage and Compatibility

Completed clips stay local in the app's managed recording directory. Copied
clips remain there after closing the preview. Clean up retained clips older
than seven days only when not open in a preview and not referenced by the
current file-drop clipboard; provide recovery location on playback failure.
No upload, analytics, or changes to user-owned saved files.

Test LINE PC as the primary chat target. Windows file-copy functionality does
not establish that every chat accepts pasted MP4s. Official LINE documentation
explicitly supports dragging videos into chat; clipboard paste still requires
real-client verification and must be reported honestly if unverified.

References:
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.clipboard.setfiledroplist
- https://help2.line.me/line/smartphone/sp?contentId=20007005&lang=en

## Verification Plan

- First observe failing behavioral tests for 2px strokes, dash/dot rendering,
  style changes and undo, and numbering after delete/undo/redo/clear.
- Browser tests cover preview loading, actual playback of a generated MP4,
  controls and layout, bridge requests, and copy/save failure states.
- Native tests cover recording completion opening a preview, file clipboard
  contents, retained-file lifetime, safe save/cancel/failure, and close guards.
  Clipboard tests must restore the user's prior clipboard when feasible.
- Run the existing editor, launcher, native build, and installer checks. Use
  generated test content, not the user's documents, chats, or recordings.
- Package the next Windows release only after verification; do not install
  it or close the user's existing application automatically.

## Boundaries

No video trimming, codec redesign, chat integration API, automatic sending,
browser-extension changes, or scrolling-capture refactor in this increment.

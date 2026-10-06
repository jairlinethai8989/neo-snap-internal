# Editable multi-image workspaces

Approved: 2026-10-06. Keep capture engines and source tabs unchanged.

An editor can combine selected open tabs in vertical, horizontal, or free layout.
Images retain native source pixels in immutable embedded PNG assets. Image layers
have independent bounds and source crops. Imported annotations remain editable,
belong to their image, and follow image movement/resizing. Their rendering is
clipped to that image. New workspace annotations are independent.

Import files, clipboard images, or existing capture tabs into the current work.
Use the existing selection, resize, crop and annotation tools. Image crop changes
only the selected image; workspace crop remains available when no image is selected.
Undo/redo covers imports, layout, movement, resizing, cropping and deletion.

Save a versioned, self-contained .neosnap JSON document containing dimensions,
lossless PNG assets, image layers, annotations and crop. No external resource
references are allowed. Validate types, references, finite coordinates, dimension,
pixel and file-size limits before replacing any editor state. Failed imports keep
the current work. Save atomically; do not mark work kept on cancellation/failure.
Project saving and PNG export remain separate commands. Project files retain
unredacted originals; communicate this in the project-save dialog.

Use isolated editor-project model/UI files and a native hub partial for dialogs,
tab selection, project import/export. Keep the existing hub close/save protection.
New projects open as additional tabs, never mutate their selected source tabs.

Verify with generated PNG fixtures: exact pixels and native scale, editable text,
owner transforms/clipping, crop, undo/redo, save/load round-trip, invalid project
rejection, clipboard/file import, source independence, responsive toolbar, and
native atomic saves. Re-run the existing editor and native suites.

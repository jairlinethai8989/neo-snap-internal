# OCR Review Interface

Approved for Neo Snap and SnapZy: restyle the native OCR dialog to match the editor, without adding another browser controller. White surfaces, restrained borders, the existing green primary action, blue scan branding, product-specific application icons, and fully localized EN/TH controls.

Layout: compact header; friendly language names, mutually exclusive Text/Table output controls and the scan action; result heading with Copy; editable output; truthful status, character count and Close. The dialog remains resizable and supports compact and high-DPI layouts. Recognition remains local and explicitly started by the user; optional models still require consent.

Preserve edited output separately for the text and table modes. Disable recognition controls and Copy while recognition is active. Show distinct preparing, reading, complete, empty, canceled and failed states. Only show copied feedback after the clipboard write succeeds; reset it when content changes. Failures and cancellations preserve existing output.

Tests: native screenshots for both locales/products at normal and compact sizes; actual Windows OCR; preservation of edits across modes; empty/copy/reading/failure states; existing direct editor OCR tests and the full non-desktop release suite. Produce TEST4 installers, keep versions unchanged and do not push or publish.

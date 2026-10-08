# Dedicated OCR Button

Approved: add a dedicated OCR command to the editor top actions in both Neo Snap and SnapZy. Use the existing Lucide ScanText icon and localized labels, `Scan Text` and `อ่านข้อความ OCR`. Remove OCR from the Work tools menu.

Reuse the existing OCR review dialog and region export. With no selection, read the whole rendered image; with a selected object or draft crop, read that region without applying a destructive crop. Keep OCR local and manual; opening the dialog does not start recognition or download language models automatically.

Add a bilingual Help entry with the same icon. Preserve drawing state and history, ignore clicks before an image is ready, and retain native busy guards. Keep the command visible without overlapping other top actions at compact widths.

Verification: browser tests for both products, EN/TH labels and Help, meaningful icons, warm loading, native messages, and compact layouts; native WebView tests for direct dialog opening and original/selected dimensions; existing full non-desktop release suite and installer checks. No push or public release until user testing is approved.

# Neo Snap 0.1.21

Released 2026-10-08 (Asia/Bangkok).

## Highlights

- Read text from the whole image or a selected region with local OCR.
- Use English OCR through Windows when available, or install the optional verified Thai + English model files after explicit consent.
- Review and edit OCR output as plain text or a tab-separated table before copying it.
- Preserve Thai words correctly when the OCR engine returns character-sized fragments; artificial spaces are no longer inserted between Thai characters.
- Use the redesigned bilingual OCR window with clear read, copy, busy, empty and error states.

OCR processing remains on the user's PC. The optional language-model download is the only OCR network operation. Images are not uploaded.

## Verification

The release suite covers real offline Thai/English OCR, generated Thai and dark-background images, English Windows OCR, text/table output, Thai spacing, browser editor tests, native core tests, framework-dependent .NET 10 checks and packaged-installer extraction. The installer still requires manual clean-PC UAC/restart validation and live target-app capture testing.

Download [Neo Snap 0.1.21](../downloads/Neo-Snap-Windows-Setup-0.1.21.exe) and verify its [SHA-256](../checksums/Neo-Snap-Windows-Setup-0.1.21.exe.sha256).

Installer size: 11,186,176 bytes. SHA-256: `F80F494459CEBAAFD0A48C03BD627314E7FE288DE61FC27451BF3FEA59A00184`.

# Snapzy Public Release Design

## Goal

Prepare a public Windows release named Snapzy, starting at version 1.0.0, while keeping the internal Neo Snap product and its source private. Both products should continue to share the same capture and editing capabilities without sharing their installed identity or user data.

## Product and repository boundaries

- Neo Snap remains the internal/team edition. Its source repository must be private and its product name, version line, settings, installation directory, registry identity, shortcuts, startup registration, and user data remain independent.
- Snapzy is distributed through the existing public `jairlinethai8989/snapzy-releases` repository. That repository contains a bilingual product/download README, the user-approved PromptPay donation QR, release notes, checksums, and packaged installers only. It must not contain source code, credentials, recordings, or internal documents.
- Use a build flavor in the shared codebase rather than maintaining a second source fork. Product identity, version, icon, default language, and storage identifiers must be explicit build settings.

## Language behavior

- Snapzy launches in English by default, including first-run/setup screens.
- Thai is an available language in the application. The user's choice is saved and used by the launcher, capture controls/status, editor, settings, About, and user-facing dialogs. Text embedded in common HTML and native Windows forms must use the same language preference.
- The public README and release notes are provided in Thai and English. Donation QR content stays unmodified.

## Installation and data isolation

Snapzy and Neo Snap must be installable side-by-side. Their AppUserModel identity, uninstall registration, install path, shortcuts, startup entries, settings, recording cache, and logs must not collide. Updating or uninstalling one product must not modify the other product's files or preferences.

## Validation and release gate

Build and test both flavors from the same source. Verify language switching and persistence in each user-facing surface, install and update isolation, and that the public package contains no source or internal files. Exercise capture, scrolling capture, annotation/editing, video recording, and installer flows before producing `Snapzy-Setup-1.0.0.exe`. Publish the installer only after the release artifact and its SHA-256 are reviewed; do not alter the public repository's visibility or upload a source bundle.

## Decisions and open constraints

- Donation QR inclusion in the public repository is approved by the user.
- The public Snapzy releases repository is separate from the private Neo Snap source repository.
- The current source checkout has substantial uncommitted work and no configured Git remote. Preserve that work; do not push it until a private source repository is identified and the exact files to publish are reviewed.
- This design does not promise code signing or SmartScreen reputation for the first public installer.

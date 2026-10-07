# Shared .NET Desktop Runtime Installer

## Approved Direction

The user approved smaller installers for both Neo Snap and SnapZy, without a
bundled .NET runtime. Setup checks for a compatible installed runtime, asks for
explicit consent if it is missing, and installs .NET separately before modifying
the application. This document records the implementation contract for review.

No application version bump, public upload, automatic installation on the
developer's machine, or changes to application signing are included.

## Existing System

- Both products use the shared Windows Forms application, targeting
  `net10.0-windows10.0.19041.0`, `win-x64`.
- `Directory.Build.props`, `Directory.Build.targets`, and the installer builder
  currently publish self-contained applications with runtime version 10.0.12.
- The installer runs through Windows PowerShell 5.1, with separate product
  identities, installation directories, data directories, and startup entries.
- Settings, language, shortcuts, close-before-update prompts, and delayed launch
  after setup dismissal must remain intact.
- Existing WebView2 and Visual C++ dependency handling remains separate; neither
  dependency is satisfied by installing .NET.

## Distribution Options

1. Recommended and approved: framework-dependent application plus on-demand
   runtime installation. Smaller recurring downloads; first installation may
   require internet and administrator approval.
2. Manual prerequisite installation only: same small application payload, but
   users download and install .NET themselves. Retain this as the recovery path
   when network access or administrator rights prevent automatic installation.
3. Bundled runtime: independent installation without a shared runtime, but larger
   downloads. This is the existing behavior and is not the default going forward.

## Component Boundaries

### Build and Payload Contract

Publish both products as framework-dependent x64 applications. Keep native
recording libraries and application dependencies. Do not remove files simply
because their names start with `System` or `Microsoft`.

Generate a small prerequisite manifest from the published runtime configuration,
containing the required framework names, minimum versions, and architecture.
The installer must use the same requirements as the executable, rather than a
separate hardcoded version that can drift. Current minimum runtime is 10.0.12.

Use a compatible stable patch of .NET 10.0, not a preview runtime or a different
major version. Do not install the SDK. Actual runtime configuration must enforce
the same patch selection policy that setup checks.

### Runtime Detection

Use the registered x64 .NET installation and its actual runtime inventory. Do not
trust the first `dotnet` found on PATH, an x86-only installation, a stale registry
version, or an empty runtime directory. Check both `Microsoft.WindowsDesktop.App`
and the compatible `Microsoft.NETCore.App` it requires.

An SDK installation is acceptable only if its installed runtimes satisfy these
requirements. .NET Framework 4.x, ASP.NET-only runtime, and .NET 11 alone do not
satisfy the .NET 10 desktop requirement.

### Consent, Download, and Installation

After setup language/options are selected and before application files are
changed, inspect prerequisites. If a compatible runtime exists, continue without
network access or elevation.

Otherwise show a localized EN/TH consent dialog identifying Microsoft's .NET
Desktop Runtime 10 x64, the need to download, and possible administrator approval.
Rejecting consent cancels setup without changing application files or settings.

Resolve an appropriate stable release through official Microsoft release
metadata. Download only from approved Microsoft HTTPS endpoints, verify the
published digest and trusted Microsoft installer signature, and show progress.
Use a unique temporary directory with bounded network timeouts and cleanup.
Never execute a partial, corrupt, unsigned, or mismatched download.

Launch only the runtime installer with required elevation; do not elevate the
application's per-user setup or change the target user's profile. Suppress forced
restart. Handle success, restart required, UAC refusal, cancellation, and failure
explicitly. Re-detect compatible installed runtimes before continuing.

If a restart is required, inform the user and do not auto-launch the application
or falsely report it ready. A subsequent setup run repeats detection normally.

### Failure and Offline Recovery

Network failure, unsuitable release metadata, integrity failure, runtime installer
failure, or missing post-install runtime stops before application changes. Show a
clear EN/TH explanation and an official manual-install link. Users can install
the prerequisite separately and rerun setup. No .NET SDK, winget, or command-line
knowledge is required.

Setup must not force-close user applications, automatically remove older shared
runtime versions, or restart Windows. A runtime installed successfully remains a
shared system component even if later application setup is canceled.

### Upgrade and Ownership

Support existing self-contained installations. Remove only runtime files that
the old application installer owned, using an explicit reviewed inventory, not
wildcard deletion. Preserve application-owned native dependencies and unrelated
files. Stage and validate the new payload before replacing managed application
files; provide recovery if replacement fails.

Keep settings, captures, recordings, projects, and product-specific identity and
startup preferences unchanged. Recheck whether the application reopened while
the runtime was downloading before any application replacement.

Neo Snap and SnapZy share the compatible system runtime but not their application
data. Uninstalling either product does not uninstall .NET. Repeated installation
when the prerequisite is already satisfied must not download or install it again.

## Verification

Use test-first implementation, separating detection/policy tests from actual
network or elevated operations. Tests must not uninstall the developer's runtime
or elevate/install components without explicit user authorization.

Required cases:
- compatible runtime present; no network request or consent dialog;
- missing runtime, insufficient patch, wrong architecture, preview, stale
  registry, core-only runtime, and unrelated major versions;
- existing SDK with suitable desktop/core runtimes;
- consent accepted/rejected, offline, timeout, bad digest/signature, interrupted
  download, UAC refusal, installer error, restart required, and failed recheck;
- no application/settings/shortcuts changes when preflight fails;
- same-version repair and old bundled-runtime upgrade for both product identities;
- owned runtime cleanup preserves native libraries, settings, user work, and
  unrelated files, including upgrade recovery;
- uninstall preserves the shared runtime;
- framework-dependent payload contains no bundled .NET runtime, and runtime
  configuration agrees with prerequisite detection;
- both products launch with the compatible shared runtime, including editor,
  capture, recording, Help, language, hotkeys, and installer completion behavior.

Run the existing both-product release gate and verify actual extracted installer
payloads. Record new installer sizes from real builds; do not promise an estimated
size as a measured result. Test clean-machine elevation/installation in an
appropriate VM or explicitly authorized machine; report unavailable live cases.

## Acceptance

The new setup downloads less application data, reuses compatible .NET already on
the machine, asks before installing any missing runtime, and does not damage the
existing application when prerequisite installation cannot proceed. Both product
installers follow this same contract while preserving their existing identities.

## References

- Microsoft runtime types, installation, elevation, and restart status:
  https://learn.microsoft.com/en-us/dotnet/core/install/windows
- Framework-dependent runtime discovery and compatibility:
  https://learn.microsoft.com/en-us/dotnet/core/runtime-discovery/troubleshoot-app-launch
- Framework resolution policy:
  https://github.com/dotnet/runtime/blob/main/docs/design/features/framework-version-resolution.md

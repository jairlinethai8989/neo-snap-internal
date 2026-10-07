# Shared Runtime Candidate Verification

Date: 2026-10-07. Local Windows 11 x64 verification, not a public release.
Approved contract: `docs/superpowers/specs/2026-10-07-shared-dotnet-runtime-design.md`.

## Delivered Behavior

- Both installer payloads are framework-dependent, with global x64 runtime
  discovery and LatestPatch resolution for Core/Desktop 10.0.12 or newer 10.0.
- Requirements are generated from each actual published runtime configuration.
  Detection rejects wrong architectures, previews, stale/empty directories,
  unrelated majors, missing frameworks and insufficient Desktop-to-Core patches.
- Missing runtime preparation asks explicit EN/TH consent, downloads the stable
  Microsoft Desktop Runtime through official HTTPS metadata, verifies SHA-512
  and Microsoft Authenticode, and elevates only the runtime installer.
- Runtime cancellation/failure/restart stops before application mutation. A
  successful install is rechecked; a reopened app blocks replacement.
- Payloads are staged and digest-validated. Managed-file replacement has backup
  and rollback; legacy cleanup uses an explicit runtime-pack inventory including
  createdump.exe, not broad System/Microsoft wildcards. Unknown files are retained.
- Uninstall is unchanged and does not uninstall shared .NET. Product identities,
  data folders, language defaults and donation policies remain separate.
- All localized setup scripts retain UTF-8 BOM for Windows PowerShell 5.1.
- Developer/test builds remain self-contained by default. DefaultSelfContained
  carries the deployment default through SDK project-reference negotiation so
  the isolated framework-dependent test harness can build without NETSDK1151.

## Measured Installers

Files are under `dist/shared-runtime-candidates`. Versions were not increased.

| Product | New Bytes | New MB (Decimal) | Previous Bytes |
| --- | ---: | ---: | ---: |
| Neo Snap 0.1.20 | 7,938,048 | 7.94 | 82,706,432 |
| SnapZy 1.0.0 | 7,933,952 | 7.93 | 82,702,336 |

About 90.4% smaller. The separate Desktop Runtime download tested here was
60,032,984 bytes; a machine missing the prerequisite needs that additional
download/install once. App updates do not bundle it again.

SHA-256:

- Neo Snap: `CCE8C7C0EBA2425EF25A3744876BAD2B2FAB6C4D8E6AC72981E8D8B964834FDB`
- SnapZy: `8A928971AD12C373D851C8D837E34AC82FBC17E3FE011E94C4B32F1F03DADE16`

Previous desktop-selection candidates retained their original sizes and hashes.
No source push, public upload, application signing change or system .NET install
was performed. Global Core/Desktop still report 8.0.22 and 10.0.1.

## Verification Evidence

- Both-product release gate without desktop: passed both native/core suites,
  all 16 browser test files and all 16 PowerShell installer test files. Runtime
  deployment tests build and validate both real framework-dependent payloads.
- Fresh both-product CoreOnly builds after the shared-default adjustment:
  passed, zero build warnings/errors. All installer modules separately parsed
  in Windows PowerShell 5.1 with valid localized encoding.
- Policy tests cover consent, offline/failure boundaries, bad hashes/signatures,
  UAC refusal, cancellation, restart, recheck and cleanup. External installation
  outcomes are simulated; actual filesystem hashing and rollback are exercised.
- Actual consent refusal was automated in EN/TH, including text-fit checks.
- Actual production download path passed SHA-512 and trusted Microsoft signature
  validation. The downloaded executable was never run and was cleaned up.
- Both real IExpress setup executables extracted without invoking installation;
  staged scripts parse in PowerShell 5.1, prerequisite modules match payload
  hashes, and payloads contain no bundled CoreCLR/Forms/WPF/hostfxr/createdump.
- Actual prior bundled payloads were copied into isolated upgrade targets for
  both flavors. Upgrade removed owned runtime files, preserved an unknown user
  file and recording library, and validated the new managed-file digests.
- SnapZy package identity, approved icon A, English default, Help, shortcut and
  launch fixes passed the extracted-package test.
- Actual published assemblies (hash-matched) passed core tests using private
  Core/Desktop 10.0.12 packs and a copied Microsoft dotnet host. Both products'
  native editor and launcher-controls groups passed on that isolated runtime.
  SnapZy's editor initially timed out in the combined run; its separate editor
  and launcher rerun passed without code changes. Do not call that initial run
  an all-green aggregate. Successful retry emitted a WebView cleanup warning.

## Remaining Release Gates

- Clean-machine actual runtime install, UAC denial, installer failure, offline
  preparation and restart-required behavior require an authorized VM/PC. No
  elevated runtime installer was executed in this verification.
- The full desktop gate passed all Neo Snap groups but stopped at SnapZy's
  window-fast fixture: AnyDesk obscured the generated region. A repeat reproduced
  the same actual AnyDesk target handle. No user window was closed or moved;
  remaining full desktop groups must be rerun with an unobscured test desktop.
- The packaged global apphost cannot be positively launched on this PC until
  compatible global 10.0.12+ is installed; compatible-runtime assembly/UI testing
  above used an isolated CLI host, not a system runtime installation.
- Full setup completion/shortcut/startup integration and uninstall on a clean
  machine, live Lark/Excel/PDF, Windows 10 and mixed-DPI tests remain pilot checks.

These are local test candidates, not a certified release. Existing worktree
changes from preceding tasks were preserved.

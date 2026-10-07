# Shared Runtime Implementation Plan

Approved spec: `../specs/2026-10-07-shared-dotnet-runtime-design.md`.

1. Add failing Windows PowerShell 5.1 tests for runtime requirements, actual
   inventory validation, compatible patch selection, trusted release metadata,
   consent and installation outcomes. Keep network/elevation behind boundaries.
2. Implement small runtime-policy and runtime-setup modules. Use the registered
   global x64 runtime, explicit consent, verified Microsoft downloads, progress,
   bounded operations, restart handling, re-detection, and temporary cleanup.
3. Add failing payload-transaction tests for staged validation, owned-file
   replacement, bundled-runtime cleanup, unrelated-file preservation, and rollback.
   Implement safe staged replacement before settings/shortcut updates.
4. Publish both installers with `--self-contained false`, matching restore
   settings, global apphost discovery and LatestPatch policy. Generate runtime
   requirements and managed-file inventories. Keep self-contained test harnesses
   so machine-wide runtime changes are not needed to run existing tests.
5. Integrate preflight before application mutation, preserve language and product
   identity, check reopened processes after downloads, and retain existing setup
   completion and uninstall behavior. Update outdated bundled-runtime tests/docs.
6. Run new behavioral tests and the complete both-product release gate. Use only
   workspace-isolated runtime layouts for additional framework-dependent launch
   checks; never install/uninstall the machine runtime as part of automated tests.
7. Build separate local candidate setups without version bumps or public uploads.
   Extract actual payloads, check runtime policy/inventories, test packaged code,
   measure sizes, and record any unavailable clean-machine/UAC/reboot checks.

Verification records belong in `docs/audits`. Existing unrelated changes and
previous installer candidates remain untouched.

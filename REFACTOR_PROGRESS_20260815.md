# Refactor Progress - 2026-08-15

## Baseline

- Inspected the current source of truth in `ModTheSpire2Companion` and `NativeLauncher`.
- Confirmed the installed game version is `v0.111.0`.
- Confirmed the pre-refactor companion source still compiles against the current game-managed assemblies with direct Roslyn compilation.
- Confirmed launcher settings and order self-tests can run against the current installation.
- The legacy package verifier currently stops on a volatile external assertion: the subscribed QuickRestart manifest no longer matches the hard-coded historical dependency version. This is an environment fixture issue, not a launcher self-test failure.

## Refactor

- Added injectable game-side service contracts in `ModTheSpire2Companion/CompanionServices.cs`.
- Routed management UI, runtime mod discovery, load-order UI, launcher paths/actions, and logging through those services.
- Added an official runtime adapter for `AssemblyInfo.ModMap`, including multiple assemblies associated with one mod.
- Added official API capability logging during initialization.
- Added native service contracts in `NativeLauncher/LauncherServices.h`.
- Routed native discovery, sorting, grouping, settings writing, and process start through the service table.
- Added tri-state `affects_gameplay` handling so missing metadata remains unknown.
- Added current official duplicate-source behavior: a newer Workshop version can supersede an older local copy of the same mod ID.
- Removed five unused native helper functions after the service migration.

## Build And Verification

- Added `Tools/BuildModTheSpire2.ps1` for repeatable companion and native launcher builds.
- Added `Tools/VerifyRefactorArchitecture.ps1` for architecture guards, compilation, and native self-tests.
- Final result: `Status OK` against STS2 `v0.111.0`.
- Settings self-test: passed.
- Order/profile/dependency self-test: passed.
- Discovery precedence and metadata tri-state self-test: passed.

Candidate output:

`dist/build-temp/refactor-services`

This candidate has not replaced the clean Workshop package, uploader content, or live game mod folder. In-game UI behavior still needs the usual manual Steam test before packaging.

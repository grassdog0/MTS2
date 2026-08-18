# ModTheSpire2 Refactor Architecture

## Purpose

This refactor keeps the existing launcher, profile, dependency, vanilla-launch, and in-game restart behavior while making game updates less likely to require another large single-file rewrite.

The current compatibility target inspected during this refactor is STS2 `v0.111.0`.

## Companion Boundaries

`ModTheSpire2Companion/CompanionServices.cs` owns the replaceable game-side contracts:

- `ICompanionPaths`: installed mod, data, launcher, and README paths.
- `ICompanionLogSink`: diagnostics output.
- `ILauncherGateway`: launch-option text and launcher opening.
- `IGameSessionProbe`: current menu/run/save state.
- `IModCatalog`: mod discovery and classification.
- `ILoadOrderService`: current/default order, move, save, and reset behavior.
- `IRuntimeModStateProvider`: loaded mod IDs and all assemblies associated with each mod.
- `IGameCompatibilityInspector`: feature detection for changing official APIs.

The old static classes remain as compatibility facades. Existing UI and restart behavior can therefore be moved one subsystem at a time without changing every caller at once.

`CompanionServices.Configure` accepts a complete service set, so tests and future compatibility adapters can replace one subsystem without adding global conditionals to the UI code. `ResetDefaults` restores the production adapters.

The default runtime provider reads the official `AssemblyInfo.ModMap` when available. This supports the official multi-assembly model while retaining an `AppDomain` fallback for older game versions.

## Native Launcher Boundaries

`NativeLauncher/LauncherServices.h` defines the native orchestration contracts:

- discovery;
- load-order sorting and validation;
- grouping;
- settings writing;
- process launch.

The Win32 UI calls this service table instead of directly owning each subsystem. A future platform UI or a new settings backend can replace a service without rewriting the window procedure.

## Official Update Rules

The native scanner now models `affects_gameplay` as a three-state value: gameplay, utility, or unknown. Missing metadata is no longer silently treated as utility.

Duplicate IDs follow the current official source rule: a Workshop copy replaces a local copy only when the Workshop semantic version is newer. Equal, older, or unparseable Workshop versions do not override the local copy.

The launcher still controls the requested startup list. STS2 remains authoritative for runtime model/serialization ordering, including its newer `ModelId` and non-gameplay sorting rules.

## Build

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\BuildModTheSpire2.ps1
```

Build products go to `dist/build-temp/refactor-services` by default. The script does not overwrite the live game mod or the clean Workshop upload package.

## Extension Guidance

New integrations should be added behind an existing contract where possible. Examples:

- official mod metadata changes belong in `IRuntimeModStateProvider`;
- future settings formats belong behind the native settings service and a companion settings adapter;
- RitsuLib or Better Mod Menu order import belongs in load-order providers;
- Linux/macOS native frontends can reuse discovery/profile formats without taking a dependency on Win32 UI code.

Do not make runtime model ordering compete with the official game serializer. ModTheSpire2 should report requested versus effective order when that information becomes available.

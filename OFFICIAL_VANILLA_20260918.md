# Official Vanilla startup

HISTORICAL / SUPERSEDED: The user requested normal startup with all mods disabled to retain the game's Mod Settings UI. The current build writes a complete all-disabled list, keeps mod-loading consent enabled, and removes --nomods in both modes. The implementation and acceptance criteria below describe the withdrawn experimental approach.

Supersedes the all-disabled configuration approach in V111_VANILLA_COMPAT_TEST.md.

## Game evidence

Read-only inspection of installed v0.111.0 sts2.dll:
- CommandLineHelper trims leading dashes and recognizes the nomods argument.
- ModManager.Initialize checks nomods before discovery, Steam Workshop scanning, disabled-mod filtering, or TryLoadMod. It logs `'nomods' passed as executable argument, skipping mod initialization` and returns.
- IsRunningModded tests loaded mods. Skipping initialization leaves none, including MTS2.

## Behavior

- Windows Vanilla retains its selection/order recovery snapshot, does not write game settings, and starts the original command with --nomods.
- Launch Selected removes nomods arguments and writes the selected mod configuration. Other game arguments are retained. The flag precedes Godot's -- separator if present.
- Native Linux/macOS Vanilla also uses --nomods without writing settings. It requires the original command supplied through Steam's -- %command% launch option.
- No telemetry consent, upload gating, or server decision is modified. Official acceptance of run data is not guaranteed by the launcher.

## Verification

- Settings self-test exercises Vanilla twice then Launch Selected using stub process/settings services: zero Vanilla writes and exactly one start request per click.
- Argument self-test covers renderer flags, repeated nomods, the Godot separator, quoted paths, empty arguments, overflow rejection, and switching back to Selected.
- Package verification covers the Windows regressions and seven-file package. No real game run or official metrics upload is performed by these automated tests.

## Manual test

1. Start Vanilla from Steam's launcher flow; confirm the game log contains the skip-initialization message above.
2. Confirm unmodded saves/profile and no mod-loaded indicators; MTS2's in-game button must be absent.
3. Exit normally, reopen the launcher, and restore the pre-Vanilla preset.
4. Launch Selected; confirm only selected mods load, in saved order, and no nomods flag remains.
5. Repeat with --rendering-driver opengl3 and verify it survives both modes.

Native Linux/macOS remain unverified on real systems. This candidate is not uploaded.

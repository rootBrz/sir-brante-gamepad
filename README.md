# Sir Brante Gamepad

Controller support for **The Life and Suffering of Sir Brante** (Windows Unity Mono version), as a [BepInEx](https://github.com/BepInEx/BepInEx) 5 plugin. It works on Windows and on Linux through Proton, including Steam Deck. The game's own files are not modified.

> Unofficial fan project, not affiliated with the game's developers or publisher. No game files are included in this repository or its releases.

## Features

- D-pad navigation between on-screen controls, left-stick pointer, right-stick scrolling, click and drag.
- Escape, Space and dialogue-page shortcuts on the face and shoulder buttons.
- Controller keyboard for text fields, with layouts for the game's languages.
- In-game settings panel, rebindable buttons, optional rumble and click sound.
- Optional start-up speed-ups: skip the logo screens, skip the loading wait, skip the story intro.

## Controls

| Control | Action |
| --- | --- |
| D-pad | Move the pointer between visible UI controls, including hover-only information and disabled choices; a soft highlight marks the control under the pointer (`FocusRing = false` turns it off) |
| Left stick | Move the pointer freely. Hold LT to slow it down, RT to speed it up |
| A | Click; hold A while moving the left stick to drag sliders and scrollbars |
| Right stick up/down | Scroll under the pointer (RT scrolls faster) |
| B | Send the game's Escape key: back, pause, or close where the game supports Escape |
| Start (Options) | Open the mod's settings panel; D-pad up/down selects, left/right changes, A toggles, B or Start closes. Changes apply immediately and are saved when the panel closes |
| LB / RB | Previous / next dialogue page, using the game's arrow-key handling |
| X | Send Space, including skipping cutscenes that accept Space |
| Y, or A over a text field | Open the built-in controller keyboard |
| Share/View | Show or hide the control hints |

Every action except the D-pad and sticks can be rebound in the `[Buttons]` section of the config. The hint bar shows only what applies under the pointer and follows your bindings.

The controller keyboard follows the game's language: Latin by default, Cyrillic for Russian, and the accented letters of German, French, Spanish, Italian, Portuguese, Polish and Turkish. Chinese, Japanese and Korean fall back to Latin; those scripts still need the game's normal keyboard input. Spaces, hyphens, periods and apostrophes are always available. D-pad selects a key; A types; X deletes; Y changes case; Start accepts; B cancels without changing the field.

The plugin uses the game's own cursor, rendered in Unity's software-cursor mode so gamescope does not hide it after gamepad input. Moving onto a choice uses the game's original hover handlers and tooltips, disabled choices stay disabled, and directional navigation respects the UI raycast stack and modal blockers. Use the left stick for unusual layouts, and visible close/back buttons for windows without Escape handling.

Use Steam Input's **Gamepad** template, not a keyboard/mouse template. XInput-compatible controllers work directly; Steam Input can expose other controller types as XInput.

## Install

Requirements: the Windows build of the game (Steam app `1272160`), tested with Unity `2018.3.0` / Mono, and [BepInEx 5.4.x Windows x64](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5).

1. Close the game.
2. Install BepInEx Windows x64 into the game folder if no loader is installed yet (do not replace another mod's loader blindly). It adds `winhttp.dll`, `doorstop_config.ini` and `BepInEx/`.
3. Download `SirBrante.Gamepad-<version>.zip` from the [Releases](../../releases) page and extract it into the game folder, or copy `Brante.Gamepad.dll` into `BepInEx/plugins/`.
4. **Proton / Linux only:** make Proton load BepInEx's `winhttp.dll`, using either this Steam launch option:

   ```text
   WINEDLLOVERRIDES="winhttp=n,b" %command%
   ```

   or a per-game registry override, which needs no launch option (adjust the Proton directory to the version you use):

   ```bash
   export STEAM_COMPAT_DATA_PATH="$HOME/.steam/steam/steamapps/compatdata/1272160"
   export STEAM_COMPAT_CLIENT_INSTALL_PATH="$HOME/.steam/steam"
   "$HOME/.steam/steam/steamapps/common/<Proton version>/proton" run reg.exe add \
     'HKCU\Software\Wine\AppDefaults\The Life and Suffering of Sir Brante.exe\DllOverrides' \
     /v winhttp /t REG_SZ /d native,builtin /f
   ```

The game folder is usually `~/.steam/steam/steamapps/common/The Life and Suffering of Sir Brante` on Linux. Start the game; `BepInEx/LogOutput.log` should contain `Gamepad ready`.

## Configuration

The config file is `BepInEx/config/local.brante.gamepad.cfg`, created on first launch. Most options can also be changed from the in-game settings panel (Start). Defaults:

```ini
[Controller]
Deadzone = 0.22
CursorSpeed = 900
ScrollSpeed = 8
PointerCurve = 1
PrecisionFactor = 0.3
BoostFactor = 2

[Buttons]
Click = A
Back = B
ModMenu = Start
Skip = X
Keyboard = Y
PagePrevious = LB
PageNext = RB
ToggleHints = Back
Precision = LT
Boost = RT

[Startup]
SkipLogos = true
SkipLoadingWait = true
SkipIntro = false

[Input]
Backend = Win32

[Navigation]
Wrap = false

[Display]
ShowHints = true
FocusRing = true
HintScale = 1

[Feedback]
Rumble = false
ClickSound = false
ClickVolume = 0.25

[Debug]
LogActions = false
```

- `Deadzone`: raise it if the stick drifts. `CursorSpeed` is pixels per second. `ScrollSpeed` is scroll notches per second at full deflection. `PointerCurve` above 1 gives finer control near the stick center.
- `[Buttons]`: names are `A B X Y LB RB LT RT Start Back LS RS`; list several with commas (`Back = B, Start`); `None` disables an action. An unknown name is logged and the default is used. `Back` sends Escape; on the controller keyboard `Back` cancels and `ModMenu` accepts. `ToggleHints = Back` is the XInput Back button (Share/View).
- `Backend = Win32` sends operating-system mouse and keyboard input. `Backend = Direct` keeps everything inside the game: key presses are answered through `Input.GetKey*`, and clicks, drags and scrolling go through the Unity EventSystem, so nothing is injected into other windows. The pointer position is still the real cursor. If `Direct` cannot be installed the plugin logs a warning and uses Win32. `Direct` is experimental; see [docs/TESTING.md](docs/TESTING.md).
- `SkipLogos` skips Unity's start-up logo sequence (three logos, two seconds each). Unity 2018.3 times the logos in native code with no skip call, so the plugin finds the "keep waiting" branch in `UnityPlayer.dll` by byte pattern and replaces it with no-ops in memory; the file on disk is never changed. If the pattern is not found exactly once (a different Unity build) nothing is patched and the log says `Startup logo skip unavailable`.
- `SkipLoadingWait` removes the fixed waits (1 s and 0.5 s) in the game's start-up loading scene (`GamePreparationScript.LoadBootScene`) by rewriting their `WaitForSeconds` arguments to zero. The scene still takes as long as loading really takes. If the coroutine cannot be found the log says `Loading wait removal unavailable`.
- `SkipIntro` skips the story intro scene (title logo with voice-over) by answering its "any key" check on its first frame. If the intro class cannot be patched, the plugin logs `Intro skip unavailable` and the intro plays as normal.
- `Wrap` makes D-pad navigation jump to the opposite edge when nothing lies ahead.
- `LogActions` records controller focus, clicks, shortcuts and keyboard state in `BepInEx/LogOutput.log`.

Settings marked `*` in the panel (start-up options, input backend) apply after a restart.

## Disable or uninstall

With the game closed, move `BepInEx/plugins/Brante.Gamepad.dll` out of `plugins/`. This disables all of the plugin's behavior without touching saves or other mods.

For complete loader removal, delete `winhttp.dll`, `doorstop_config.ini` and `BepInEx/` from the game folder, but only if no other mod uses them. On Proton, also remove the launch option or the registry override:

```bash
"$HOME/.steam/steam/steamapps/common/<Proton version>/proton" run reg.exe delete \
  'HKCU\Software\Wine\AppDefaults\The Life and Suffering of Sir Brante.exe\DllOverrides' \
  /v winhttp /f
```

## Building

Requires the .NET SDK (10 recommended). Releases are built by GitHub Actions. To build locally:

```bash
tools/build.sh check     # logic checks (no game files needed)
tools/build.sh build     # dist/Brante.Gamepad.dll
tools/build.sh package   # release/SirBrante.Gamepad-<version>.zip
```

`build` compiles against public NuGet packages plus the small stand-ins in `ci/stubs`, so nothing from the game is needed. To compile against the game's own assemblies instead, copy its `<game>_Data/Managed` folder to `references/` and unzip BepInEx 5.4.23.5 into `loader/` (so `loader/BepInEx/core/BepInEx.dll` exists); `tools/build.sh` then uses them automatically. Both builds reference the same assemblies, types and members. Do not distribute `references/`.

The version lives in one place: `<Version>` in `Brante.Gamepad.csproj` (override with `VERSION=x.y.z`). Releases are automatic: every push to `main` runs the `ci` workflow, and if no GitHub release exists yet for the `<Version>` in the csproj it publishes `v<Version>` with the zip and a checksum file attached. To cut a new release, bump `<Version>` and push to `main`.

Source layout: `src/Pure` holds the logic with no Unity dependency (button names and bindings, navigation scoring, keyboard layouts, settings menu model, virtual key state, hint text); `checks/` runs it on plain .NET, and CI runs the same checks. The rest of `src` is the plugin: XInput polling, input delivery (`InputSinks.cs`), UI scanning (`UiScanner.cs`), the controller keyboard and settings panel, overlay and feedback, native start-up patch (`SplashPatch.cs`), all tied together in `GamepadPlugin.cs`.

## Testing

The pure logic is covered by `tools/build.sh check`. Behavior in the game is verified by hand with the checklist in [docs/TESTING.md](docs/TESTING.md). `tools/deck_smoke.py` is a helper for testing on a remote Linux handheld over SSH (set `DECK_HOST=user@host`): it can press keys, inject gamepad events and take screenshots.

```bash
python3 tools/deck_smoke.py self-check
python3 tools/deck_smoke.py screenshot /tmp/brante.png
python3 tools/deck_smoke.py gamepad dpad_down a
```

## License

Copyright (C) 2026 rootBrz. This program is free software: you can redistribute it and/or modify it under the terms of the [GNU General Public License](LICENSE) as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. It is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.

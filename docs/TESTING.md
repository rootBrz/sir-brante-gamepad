# Manual test checklist

The logic in `src/Pure` is covered by `tools/build.sh check`. Everything below needs the game running with a
controller. Run it once with `Backend = Win32` and once with `Backend = Direct`, and tick what you actually saw.
Set `[Debug] LogActions = true` to see focus, click and keyboard events in `BepInEx/LogOutput.log`.

Startup: `LogOutput.log` says `Gamepad ready (Win32 input)` or `(Direct input)`. With `Direct`, a warning
`Direct input could not be installed` means the Harmony patch failed and Win32 is in use instead.

## Startup
- [ ] `SkipLogos = true`: the three startup logos pass in a fraction of a second (no stall, no black screen); log says `Startup logo skip enabled.` and `Startup logos finished at N s.`
- [ ] `SkipLogos = false`: the logos play for about 6 s.
- [ ] `SkipLoadingWait = true`: the loading-bar screen at launch no longer pauses before and after loading; log says `Loading wait removed (2 waits).`
- [ ] `SkipIntro = true`: the story intro scene (title logo with voice-over) is skipped; log says `Intro skip enabled.`
- [ ] `SkipIntro = false`: that intro plays; any controller button ends it.

## Navigation and pointer
- [ ] D-pad moves between buttons on the main menu, settings, save/load and a choice screen.
- [ ] D-pad ignores controls hidden behind a popup or modal blocker.
- [ ] A choice with a hover-only tooltip or a disabled choice can still be focused.
- [ ] Focus ring follows the control under the pointer; it disappears when the real mouse is used.
- [ ] `Wrap = true`: pressing right on the right-most control jumps to the left-most in that row.
- [ ] Left stick moves the pointer; `LT` slows it, `RT` speeds it up; `PointerCurve = 2` gives finer control near center.
- [ ] Moving the real mouse and then the stick does not leave the pointer stuck.

## Click, drag, scroll
- [ ] `A` clicks buttons; hovering shows the game's cursor and tooltips.
- [ ] Hold `A` and move the left stick on a settings slider: the value follows. Release ends the drag.
- [ ] Hold `A` on a scrollbar and drag it.
- [ ] Right stick scrolls a long list (save list, journal); `RT` scrolls faster.
- [ ] Clicking on empty space does nothing and does not get stuck "held".
- [ ] `Direct` only: the Book page-turn (drag) behaves the same as with Win32.

## Keys
- [ ] `B` opens the pause menu and closes windows that handle Escape. `Start` does not.
- [ ] `LB`/`RB` change dialogue pages.
- [ ] `X` skips a cutscene that accepts Space; with `Direct`, "any key" intro screens also advance.
- [ ] Alt-tab away while holding a button: nothing keeps firing in the other window.

## Controller keyboard
- [ ] `Y`, or `A` on the name field, opens the keyboard; the prompt shows the field's placeholder text.
- [ ] D-pad moves by row and column, including the short last row and the action row.
- [ ] Letters, case (`Aa` / `Y`), space, hyphen, period, apostrophe, delete (`X`), done (`Start`), cancel (`B`).
- [ ] The character limit stops further input.
- [ ] Game language Russian: the keyboard offers Cyrillic and the accepted name displays correctly.
- [ ] Game language German/French/Spanish/Polish/Turkish: accented letters are offered and display correctly.
- [ ] Chinese/Japanese/Korean: the Latin keyboard opens (no native script entry).

## Settings panel
- [ ] `Start` opens the panel over the game; the game does not react to clicks while it is open.
- [ ] D-pad up/down moves (wraps); left/right changes values; holding left/right on a number repeats.
- [ ] `A` flips toggles and cycles the backend. Hint size, pointer speed, hints on/off take effect immediately.
- [ ] `B` or `Start` closes it; reopening the game keeps the changes (check the .cfg).
- [ ] Items marked `*` show the restart note.
- [ ] With hints off in the panel, restart the game: hints come back only if turned on.

## Feedback and display
- [ ] First run shows the "Controller ready" notice once; it does not return after a restart.
- [ ] Hints are visible as soon as the controller is connected, before the first button press.
- [ ] Hints change with context: scroll hint over lists, keyboard hint over a text field, drag hint over sliders.
- [ ] `HintScale = 2` enlarges the bar and notice.
- [ ] `Rumble = true` and `ClickSound = true` react to clicks.
- [ ] `[Buttons] Click = X` (and so on) remaps and the hints follow.

## Robustness
- [ ] Unplug the controller mid-game: log says `disconnected`, no stuck cursor or key; replugging resumes.
- [ ] Open the Steam overlay: controller input is ignored until it closes.
- [ ] Two controllers connected: the one being used drives the game.

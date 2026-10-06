# 0.1.5 → 0.1.6: reasoning trail (tracking mode)

## Change

- `[HeadTracking] Mode`: an int with an accepted-value list (0, 1, 2), default 0.
- `[HeadTracking] ModeKey`: a KeyboardShortcut, default None.
- `ModConfig.CycleMode()`: the ModeKey handler, called from Update. It uses the
  same write path as `SetEnabled`, so the value is persisted, pushed live and
  shown in Apocasetter.

## Why it is built this way

**The mode only changes smoother targets.**

- Rotation-only sets the translation target to zero.
- Lean-only sets the rotation target to zero.
- The applied offsets then ease to zero, or back again, through the existing
  smoother.
- `LateUpdate`, the pre-multiplied rotation, the additive position and every
  strip path are untouched. A switched-off part becomes a zero offset that is
  still written and stripped exactly as before. That path is round-1/round-2
  verified, so the mode cannot create a new hand-back path that could leave an
  offset behind.

**It doesn't snap.** A hard cut would jump the camera by the full lean or turn.
Easing behaves like recenter. With Smoothing = 0 the switch is instant, which is
what that setting means.

**Rejected: a separate `TranslationEnabled` bool.** It is a subset of Mode.
"Lean only" is the third mode the comparable mods ship.

## Load-bearing facts

| Fact | How it holds |
|---|---|
| **F1/F2** (child camera, first-person only) | No camera code changed. |
| **F3** (no per-frame allocation) | The additions are one `GetKeyDown` plus int compares in Update. The HUD suffix is a string literal built at 10 Hz, as before. |
| **F4** (no game writes) | No game input or FSM writes. |
| **F5** (config continuity) | Two new keys whose defaults reproduce 0.1.5 exactly, so existing files need nothing. `AcceptableValueList` maps a bad `Mode` to 0 = full. `ConfigVersion` stays 2: no stored value changes meaning. |
| **F6** (hidden runner) | Unchanged. |

**Why ModeKey defaults to None:** comparable mods use PageUp, but Apocalypter's
own key bindings have not been checked, so whether PageUp is free is unknown.
- The only binding established in this bundle is C, the vehicle camera switch
  (`docs/camera-facts.md`).
- C/V/Tab in `gamecode/InputManagerSceneInputProvider.cs` are the NWH vehicle
  package's fallback keys, not a list of the game's bindings, and that NWH path
  is partly inert.

Taking an unchecked key by default could steal a game binding, so it defaults to
None, matching `ToggleKey`. To change the default, press PageUp on foot, while
driving and in menus. If nothing happens, set the default to `KeyCode.PageUp` in
`ModConfig`.

## Checked (off-tree)

The runtime's real `Update` was driven through reflection, with simulated head
input of 20° yaw and 10 cm right lean at Smoothing 0.5. All 6 checks pass:

- **Full:** yaw 20°, camera lean +10 cm to the right. This also confirms the
  0.1.5 X sign end to end.
- **Rotation only:** the lean eases out (8.19 cm after 3 frames, not a snap) and
  reaches 0; yaw is kept.
- **Lean only:** yaw goes to 0 and the lean returns to 10 cm.
- **`CycleMode`:** 2 → 0 wraps, and 0 → 1.

The round-1 reader harness (26), the round-2 migration tests (8) and the position
simulation still pass. Compile: Mono `mcs` against API stubs, the same caveat as
before (`dotnet build` against the game DLLs is the authority).

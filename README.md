# Apocalypter Head Tracking

BepInEx 5 mod for **Apocalypter** that adds **FreeTrack 2.x / OpenTrack** headtracking:
turn your head to look around in first-person — on foot and while driving — using
[OpenTrack](https://github.com/opentrack/opentrack) with a webcam, a phone tracker,
or IR clips. No mouse emulation, no game input hacks: the mod reads the tracker's
pose directly and rotates the first-person camera additively.

**Problems? Contact @OddlyTugs on Discord.**

## Features

- **FreeTrack 2.0** input (OpenTrack → Output → *FreeTrack 2.0*): the standard
  shared-memory protocol, zero configuration on the mod side.
- **OpenTrack UDP** input as an alternative (Output → *UDP over network*).
- **First-person only**: applied on foot and while driving in 1st person. The
  vehicle 3rd-person camera (C key) is never touched.
- **Apocasetter-ready**: every setting is a ranged, described BepInEx config entry —
  the [Apocasetter](https://github.com/DeonUrist/Apocasetter) Mods menu (F6) lists
  the mod with live editors for free.
- Light smoothing, per-axis sensitivity, invert, pitch clamp, **recenter key** (F8),
  optional toggle key, small status HUD.

## Quick start

1. Install [OpenTrack](https://github.com/opentrack/opentrack/releases) and pick a
   tracker (webcam face-tracking is built in).
2. In OpenTrack: **Output → FreeTrack 2.0** (or *UDP over network* — then set the
   mod's Input to OpenTrack UDP). Start tracking; center OpenTrack.
3. In-game, first person: press **F8** while looking straight ahead (mod recenter).
4. Turn your head — the camera follows. Open the Apocasetter Mods menu (**F6**) to
   tune sensitivity, smoothing, invert, and keys live.

If the axis feels swapped or signs are wrong, enable `[Debug] LogPose = true`, move
your head, and check `BepInEx\LogOutput.log` — report the logged raw values.

## Configuration

All settings live in `BepInEx\config\dev.apocalypter.headtracking.cfg` (created on
first run) and in the Apocasetter Mods menu:

| Key | Default | Range | Meaning |
|---|---|---|---|
| `[HeadTracking] Enabled` | true | bool | Master switch. |
| `Input` | 0 | 0 / 1 | Tracker input source: 0 = FreeTrack 2.0, 1 = OpenTrack UDP. |
| `UdpPort` | 4242 | 1–65535 | UDP listen port (OpenTrack UDP mode). |
| `SensitivityYaw` / `SensitivityPitch` | 0.5 | 0–3 | Camera degrees per head degree (0.5× default: real head movement is small). |
| `SensitivityRoll` | 0 | 0–3 | Roll response (0 = off). |
| `SensitivityX` / `SensitivityY` / `SensitivityZ` | 1 | 0–3 | Head translation, camera cm per head cm (1 = 1:1, 0 = off). X = sideways lean, Y = up/down, Z = forward/back. |
| `MaxLeanX` / `MaxLeanY` / `MaxLeanZ` | 45 / 30 / 30 | 0–150 | Hard cap per axis: how far the camera can leave the character, cm (defaults = human reach). |
| `MaxLeanRadius` | 50 | 0–150 | Hard cap: total camera distance from the character across all axes, cm. |
| `InvertX` / `InvertY` / `InvertZ` | false | bool | Flip the translation direction per axis. |
| `InvertYaw` / `InvertPitch` | false | bool | Flip direction. |
| `Smoothing` | 0.5 | 0–0.95 | Higher = the camera follows more slowly and smoothly (0 = instant). |
| `MaxPitch` | 80 | 0–180 | Head-pitch clamp, degrees. Mouse + head pitch is also kept within ±89°. |
| `RecenterKey` | F8 | key | Hold-this-pose neutral (main key only; modifiers ignored). |
| `ToggleKey` | None | key | In-game on/off switch. |
| `Mode` | 0 | 0 / 1 / 2 | What the head moves: 0 = rotation + lean, 1 = rotation only, 2 = lean only. |
| `ModeKey` | None | key | Cycles `Mode` in-game (comparable mods use PageUp). |
| `ShowHud` | true | bool | Status line with live yaw/pitch. |
| `[Debug] LogPose` | false | bool | Raw tracker values once per second. |
| `[Debug] SimulateInput` | false | bool | Numpad fake head (4/6 yaw, 8/2 pitch, 7/9 roll, 1/3 lean, 5 zero). No tracker needed. |
| `[General] Apocasetter` | true | bool | Opt-in for the Apocasetter Mods menu. |
| `[General] ConfigVersion` | 2 | 0–2 | Internal file-format marker for one-time upgrades. Do not edit. |

## Load-bearing game facts

- The game's mouse look is PlayMaker `MouseLook` actions that **overwrite
  `PlayerCameraHolder`'s rotation every frame** (and force roll to 0). The mod
  therefore rotates the **child `PlayerCamera`** (the MainCamera), which has no
  per-frame *absolute* rotation writer. Its offset is applied additively in
  `LateUpdate`, so mouse look, head-bob (CameraMovementPro), and aiming all keep
  working.
- CameraMovementPro (cinemachine mode) does `localRotation = localRotation * shake`
  every LateUpdate — it multiplies on the right and never strips its previous
  shake. The mod therefore **pre-multiplies** its offset (`H * vanilla`), which
  makes stripping it exact whatever order the two LateUpdates run in (0.1.1).
- CameraMovementPro also *adds* its position shake to `localPosition` each frame.
  The mod's head translation is additive too, and vector additions commute, so its
  strip is exact. `localPosition` is in the holder's (mouse-pitched) space, in
  Unity metres. The mod converts head cm to metres and removes the mouse pitch
  (0.1.5).
- The vehicle camera switch (C key) deactivates `PlayerCamera` for 3rd person and
  re-activates it for 1st — the mod applies its offset only while `PlayerCamera` is
  active, which is the entire "first-person only" rule. When the camera goes
  inactive the mod strips its offset immediately, so it re-activates exactly as
  the game left it (0.1.1).
- No Harmony patches, no game-code references: the camera is resolved by
  `GameObject.Find("PlayerCameraHolder")` + child `PlayerCamera`, cached, and
  re-found (throttled, ≤ 2×/s; immediately on scene load) when destroyed, moved
  out from under the holder, or inactive (FloatingOrigin / respawn).
- The mod reads OpenTrack's already-filtered pose; it does not do tracking itself
  (filtering/curves/deadzone/recenter stay in OpenTrack).
- Protocols (verified against OpenTrack's source in 0.1.1, see
  `docs/audit-0.1.1.md`): FreeTrack 2.0 carries **radians** with yaw and pitch
  negated (FreeTrack convention: +yaw left, +pitch up) and a DataID that ticks
  every pose; UDP carries OpenTrack's internal pose as 6 doubles
  (x, y, z, yaw, pitch, roll) in degrees. Both readers normalize to OpenTrack's
  convention (+yaw right, +pitch down, +roll left), which maps onto the Unity
  camera with no sign flips.

## Compatibility

- Runs alongside [Apocalypter Vehicle Tuning](https://github.com/CoffeeKills/ApocalypterVehicleTuning)
  and Vehicle Tuning Lite — it touches no vehicle code.
- **0.1.9:** the head offset lives on a dedicated rig object of its own
  (`HeadTrackingOffset` between the holder and the camera), so other mods that
  write the first-person camera (e.g. "Head bob and visible legs for
  Apocalypter", issue #2) can no longer corrupt the hand-back — the camera is
  re-parented under the rig (world-preserving) and the mod never writes the
  camera transform itself.

## Building

```
cd plugin && dotnet build -c Release
```

Install: copy `plugin\bin\Release\netstandard2.0\ApocalypterHeadTracking.dll` and
`icon.png` (as `ApocalypterHeadTracking.png`) into `BepInEx\plugins\` **while the
game is closed** (the game locks the DLL while running).

## Changes

### Changes in 0.1.11-alpha

- **Human lean bounds.** The camera can no longer leave the character by more
  than a human physically could: per-axis hard caps (X ±45 cm for leaning out a
  window, Y/Z ±30) plus a 50 cm total-distance cap, applied after sensitivity so
  sliders or tracker spikes can't exceed them. All four are config keys
  (`MaxLeanX/Y/Z/Radius`, 0–150) for setups that want different bounds.

### Changes in 0.1.10-alpha

- **Mod-landscape resilience.** If another mod re-parents `PlayerCamera` away
  from the holder, the mod now finds it by its MainCamera tag and puts it back
  under the rig automatically. New `UseIsolationRig` toggle (default true):
  turn it off only if a mod requires `PlayerCamera` to stay a direct child of
  the holder — the mod then falls back to the pre-0.1.9 direct-write mode with
  exact hand-back. The switch is live in the Apocasetter menu.
- Surveyed the Apocasetter index (19 mods): the camera-relevant ones are
  Apocaplayer (third-person camera — compatible: the mod only applies while
  `PlayerCamera` is active), ApocaHUD43 (screen-edge HUD — no conflict), and
  Part Adjuster Tools (uses numpad 4/6/2/8 — only overlaps the opt-in
  `[Debug] SimulateInput` keys, which are off by default).

### Changes in 0.1.9-alpha

- **Mod-conflict fix (issue #2).** The offset now lives on a dedicated
  `HeadTrackingOffset` rig between the holder and the camera, and the mod never
  writes the camera transform itself. Other camera-writing mods (head-bob mods,
  etc.) can no longer corrupt the hand-back and leave the camera rolled after
  driving. The camera is re-parented under the rig once, world-preserving;
  verified safe against the game's own camera lookups (name/tag resolution,
  GetChild usage, CameraMovementPro's component reference).

### Changes in 0.1.8-alpha

- New icon (user-drawn), repo description carries the Discord contact.

### Changes in 0.1.7-alpha

- **Display name shortened to "Head Tracking"** in the Apocasetter Mods list (and
  the BepInEx log). Every mod in the list starts with "Apocalypter", so the prefix
  only truncated the part that tells mods apart. GUID, config file and DLL file
  name are unchanged — nothing to migrate.

### Changes in 0.1.6-alpha

Follows the 0.1.5 research recommendation (`docs/headtracking-expectations.md`):
switching translation off without opening a menu is the most common extra in
comparable headtracking mods. Reasoning trail: `docs/audit-0.1.6.md`.

- **New `Mode` key:** 0 = full (rotation + lean), 1 = rotation only, 2 = lean
  only. It also shows in the Apocasetter menu. Invalid values fall back to 0.
- **New `ModeKey`:** cycles full → rotation only → lean only. The choice is saved
  like the toggle key's.
  - Default is **None**, so no key is taken until you choose one, the same as
    `ToggleKey`. Comparable mods use PageUp.
- **Smooth switching:** a part you switch off eases out over the Smoothing time,
  and eases back in when you switch it on. Nothing snaps.
- **HUD:** shows `[rotation only]` or `[lean only]` when you're not in full mode.

Config migration: none. Both keys are new, and their defaults reproduce 0.1.5
exactly. `ConfigVersion` stays 2.

### Changes in 0.1.5-alpha

Round-2 audit plus research.

- Research report: `docs/headtracking-expectations.md`.
- Reasoning trail: `docs/audit-0.1.5.md`.

**Bugs fixed (translation, 0.1.2–0.1.4)**

1. **Head centimetres were applied as Unity metres (×100).** This is the real
   cause of "the camera rolled over the world, 100s of meters away". The 0.1.2
   default of 0.5 meant a 10 cm lean moved the camera 5 m, and the 0–3 range
   allowed 150 m. The 0.1.3/0.1.4 "tiny defaults" hid the bug instead of fixing
   it. Sensitivity is now true camera cm per head cm.
2. **X and Z were reversed.** OpenTrack's convention is +X left, +Y up, +Z back.
   The evidence is OpenTrack's own SimConnect output, plus the tester having to
   invert exactly X and Z. The mapping is now correct out of the box.
3. **Leaning while looking up or down moved along the mouse-pitched axis.** At 60°
   down, a forward lean dropped the camera 8.7 cm (into the ground or dashboard)
   and "up" moved it forward. Translation now runs in the body frame: forward is
   level and up is up, matching the rotation offset.
4. **The ±50 cm clamp applied to the head input, not the camera.** The camera
   could reach 50 cm × sensitivity. It's now capped at ±50 cm per axis after
   sensitivity.
5. **Scaled rigs.** The translation offset is divided by the holder's scale, so
   a cm stays a cm.
6. **SimulateInput Numpad 1/3** moved the wrong way under the corrected mapping.
   Fixed.

**Design changes**

- **Config model decision (owner's open question), option (a).** Translation
  sensitivity is a plain ratio, 1 = 1:1, on a 0–3 slider: the same shape as the
  rotation sliders and the convention every surveyed headtracking mod uses.
  - Meta-config range sliders and free-text fields were rejected. Reasons are in
    the research report.
  - The 0.001–0.05 "bottoming out" was the units bug, not a slider problem.
- `tools/test-headpose.py` encodes translation in OpenTrack's convention. The
  0.1.4 rig mirrored the mod's sign bug, the same trap as 0.1.0's FreeTrack bug.
- No Harmony patches, NuGet packages or uGUI. The per-frame paths still allocate
  nothing.

**Config keys and migration**

- **New:** `[General] ConfigVersion` (int, internal; default 2).
- **Changed default:** `SensitivityX/Y/Z` 0.01 → **1.0**. The on-screen behaviour
  is identical; only the unit is corrected. The range changes from 0–0.05 to 0–3.
- **Automatic one-time migration.** It applies to files written by 0.1.2–0.1.4,
  detected as "has `SensitivityX`, no `ConfigVersion`":
  - X/Y/Z values are multiplied by 100 and clamped to 3 (0.01 → 1, 0.005 → 0.5).
  - InvertX and InvertZ are flipped.
  - Your setup looks and moves exactly as before, whether you compensated in
    OpenTrack or with the mod's Invert keys.
  - The log line `Config upgraded to format 2` shows the before and after values.
- **If you inverted X/Z in OpenTrack only because of this mod:** you can now undo
  that in OpenTrack *and* set the mod's InvertX/InvertZ back to false, for a
  clean setup.
- Files from 0.1.0/0.1.1 and fresh installs need nothing. No key was renamed or
  removed, and the GUID is unchanged.

### Changes in 0.1.4-alpha

- **Translation sensitivity slider rescale**: the X/Y/Z range is now 0–0.05 (was
  0–3). Useful translation values live between 0.001 and 0.05, so the slider now
  spans exactly that band with fine steps — no more bottoming out at 0.001 on a
  0–3 scale. Existing configs keep their values (values above 0.05 are clamped to
  0.05, which is still calmer than the old 0.5).

### Changes in 0.1.3-alpha

- **Translation defaults are now tiny** (0.01, was 0.5). First tester feedback:
  with default values, small head movements threw the camera around (noisy webcam
  translation × 0.5). 0.01 is a safe starting point — raise it only as far as it
  stays pleasant. Existing config files keep their old values (BepInEx writes
  defaults only on first bind); if you already tuned yours, it is untouched.
- **InvertX / InvertY / InvertZ** config keys: flip each translation axis in the
  mod menu instead of in OpenTrack's mapping.

### Changes in 0.1.2-alpha

- **Head translation (pan/lean)**: the tracker's X/Y/Z (cm) now move the camera in
  its local frame — X sideways (pan), Y up/down, Z forward/back — smoothed like the
  rotation and stripped exactly on every hand-back path (CameraMovementPro adds its
  shake delta each frame, and additions commute). New config keys `SensitivityX/Y/Z`
  (default 0.5 cm per cm, 0 = off), clamped to ±50 cm.
- Numpad simulation gained roll (7/9) and pan (1/3); the test rig gained Z/X, R/V,
  T/G translation keys and now encodes translations like OpenTrack.
- LogPose also prints the applied translation offset.

### Changes in 0.1.1-alpha

Audit release. Full reasoning trail per item (failure mode, fix, why the
load-bearing facts are untouched): `docs/audit-0.1.1.md`.

**Bugs fixed**

1. **FreeTrack read radians as degrees, with yaw/pitch signs inverted.** OpenTrack
   writes radians with yaw and pitch negated; 0.1.0 used the values directly, so a
   30° head turn moved the camera about 0.26° the wrong way. FreeTrack and UDP now
   behave the same.
2. **Holding your head still blanked tracking.** With FreeTrack, liveness was "pose
   bytes changed in the last 0.5 s". A settled OpenTrack filter or deadzone gives
   identical bytes, so after 0.5 s of looking sideways the view eased back to
   center. Liveness now follows FreeTrack's DataID frame counter. Writers that
   never advance DataID fall back to the old byte check. A stale mapping
   (OpenTrack closed) is no longer live for its first 0.5 s.
3. **The offset got baked into the camera on 1st→3rd→1st person (C key).** 0.1.0
   forgot its offset when `PlayerCamera` went inactive without removing it, so
   the next application doubled it. The same happened after a scene load
   (new camera, old offset stripped from it) and when the runner was destroyed.
   It's now stripped on every hand-back path.
4. **Disabling the mod (`Enabled = false` or the toggle key) left the camera
   rotated.** LateUpdate returned early before removing the offset. It's now
   removed at once, and re-enabling eases in from zero.
5. **Head-bob/shake was distorted by the strip order.** See the load-bearing facts
   above. The offset is now pre-multiplied, which makes the strip exact.
6. **The horizon rolled when you turned your head while looking up or down.**
   Head yaw rotated about the mouse-pitched camera axis: at 45° mouse pitch, a 30°
   head turn tilted the horizon by about 21°. Yaw now turns about the body or
   vehicle up axis. Pure pitch and roll are unchanged.
7. **The view could flip past vertical.** Mouse pitch (±80°) plus head pitch could
   go beyond 90°. The total is now held within ±89°.
8. **NaN/Inf from a tracker froze the camera for good.** FreeTrack had no check,
   and UDP checked yaw/pitch only. Non-finite or implausible values are now
   rejected in both readers, and the smoother resets if it ever goes non-finite.
9. **A busy UDP port threw an exception every frame.** The constructor threw
   inside Update 60×/s. It's now caught and logged once, the HUD shows
   "UDP port N unavailable", and binding is retried every 2 s.
   Exclusive-address bind means a port already in use is reported as busy. Under
   Mono's default `SO_REUSEADDR`, the port was shared silently and each listener
   saw only part of the packets.
10. **The UDP listener could die permanently.** Any `SocketException` ended the
    thread while the HUD said "waiting". Only Dispose ends the thread now. Packets
    must be exactly 48 bytes, and they're received into a reusable buffer.
11. **The UDP clock could jump.** `DateTime.UtcNow` was replaced by the monotonic
    `Stopwatch`.
12. **Per-frame allocations.** FreeTrack allocated a `byte[]` every frame and read
    the memory twice (torn values against the liveness bytes). It now takes one
    memcpy snapshot into a reused buffer. The HUD rebuilt its string on every
    OnGUI call; it now rebuilds at 10 Hz and draws on Repaint only.
13. **The recenter neutral leaked across input sources.** A FreeTrack neutral was
    applied to UDP or simulated poses. It's now dropped on a source or port change,
    and recentering uses `DeltaAngle`, so there's no 360° jump near ±180°.
14. **SimulateInput Numpad 8 looked down.** It now looks up.
15. **The toggle key wrote the config file twice.** It now writes once.

**Design changes**

- The runtime polls `HeadTrackingSettings` for source and port changes instead of
  reacting to `SettingsChanged`. A config edit no longer drops the camera
  references or disposes the input in the middle of a frame.
  `ModConfig.SettingsChanged` is kept as a public event.
- The camera reference is cached. `GameObject.Find` is throttled and never
  runs every frame (0.1.0 called it every frame in menus).
- The HUD shows the source (`FreeTrack` / `UDP :port` / `simulated`) and
  "idle: no 1st-person camera" in 3rd person.
- `[Debug] LogPose` logs normalized degrees with the DataID or packet counter and
  the applied offset.
- `tools/test-headpose.py` now encodes like OpenTrack. The 0.1.0 rig mirrored the
  reader's own bug. It gains H (hold still), F (freeze tracker) and N (NaN frame).
- Still no Harmony patches, NuGet packages or uGUI.

**Config keys and migration**

- No keys added, renamed, removed or re-defaulted. The GUID is unchanged, and
  existing `.cfg` files load as-is.
- `[HeadTracking] Input` now has an accepted-value list (0, 1). An out-of-range
  value falls back to 0 (FreeTrack), the same meaning it had in 0.1.0.
- The `MaxPitch` and `RecenterKey` descriptions are clarified. Values are unaffected.
- Behaviour change: sensitivities tuned around 0.1.0's FreeTrack bug (for example
  3.0 to get any movement at all) will now be about 57× stronger. Go back to the
  0.5 default.

### 0.1.0-alpha

Initial release: FreeTrack 2.0 + OpenTrack UDP input, additive first-person camera
rotation with smoothing, sensitivity/invert/pitch clamp, recenter + toggle keys,
status HUD, full Apocasetter config integration.

## Credits

Developed with **Claude Code (DeepSeek API)** and **Claude Chat**, with a
third-party AI audit pass. Original request and testing: the Apocalypter modding
community (issue #1 by runcajsz).

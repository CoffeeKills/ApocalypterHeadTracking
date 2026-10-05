# Apocalypter Head Tracking

BepInEx 5 mod for **Apocalypter** that adds **FreeTrack 2.x / OpenTrack** headtracking:
turn your head to look around in first-person — on foot and while driving — using
[OpenTrack](https://github.com/opentrack/opentrack) with a webcam, a phone tracker,
or IR clips. No mouse emulation, no game input hacks: the mod reads the tracker's
pose directly and rotates the first-person camera additively.

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
| `SensitivityX` / `SensitivityY` / `SensitivityZ` | 0.01 | 0–3 | Camera cm per tracked cm of head translation: X = pan/lean sideways, Y = height, Z = forward/back. Keep tiny — webcam translation is noisy. |
| `InvertX` / `InvertY` / `InvertZ` | false | bool | Flip the translation direction per axis. |
| `InvertYaw` / `InvertPitch` | false | bool | Flip direction. |
| `Smoothing` | 0.5 | 0–0.95 | Higher = the camera follows more slowly and smoothly (0 = instant). |
| `MaxPitch` | 80 | 0–180 | Head-pitch clamp, degrees. Mouse + head pitch is also kept within ±89°. |
| `RecenterKey` | F8 | key | Hold-this-pose neutral (main key only; modifiers ignored). |
| `ToggleKey` | None | key | In-game on/off switch. |
| `ShowHud` | true | bool | Status line with live yaw/pitch. |
| `[Debug] LogPose` | false | bool | Raw tracker values once per second. |
| `[Debug] SimulateInput` | false | bool | Numpad fake head (4/6 yaw, 8/2 pitch, 5 zero) — no tracker needed. |
| `[General] Apocasetter` | true | bool | Opt-in for the Apocasetter Mods menu. |

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
- Do not run it alongside another mod that rotates the first-person camera.

## Building

```
cd plugin && dotnet build -c Release
```

Install: copy `plugin\bin\Release\netstandard2.0\ApocalypterHeadTracking.dll` and
`icon.png` (as `ApocalypterHeadTracking.png`) into `BepInEx\plugins\` **while the
game is closed** (the game locks the DLL while running).

## Changes

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

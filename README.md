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
| `Input` | FreeTrack | FreeTrack / OpenTrackUdp | Tracker input source. |
| `UdpPort` | 4242 | 1–65535 | UDP listen port (OpenTrack UDP mode). |
| `SensitivityYaw` / `SensitivityPitch` | 0.5 | 0–3 | Camera degrees per head degree (0.5× default: real head movement is small). |
| `SensitivityRoll` | 0 | 0–3 | Roll response (0 = off). |
| `InvertYaw` / `InvertPitch` | false | bool | Flip direction. |
| `Smoothing` | 0.5 | 0–0.95 | Higher = the camera follows more slowly and smoothly (0 = instant). |
| `MaxPitch` | 80 | 0–180 | Pitch clamp, degrees. |
| `RecenterKey` | F8 | key | Hold-this-pose neutral. |
| `ToggleKey` | None | key | In-game on/off switch. |
| `ShowHud` | true | bool | Status line with live yaw/pitch. |
| `[Debug] LogPose` | false | bool | Raw tracker values once per second. |
| `[General] Apocasetter` | true | bool | Opt-in for the Apocasetter Mods menu. |

## Load-bearing game facts

- The game's mouse look is PlayMaker `MouseLook` actions that **overwrite
  `PlayerCameraHolder`'s rotation every frame** (and force roll to 0). The mod
  therefore rotates the **child `PlayerCamera`** (the MainCamera), which has no
  per-frame rotation writer. Its offset is applied additively in `LateUpdate`, so
  mouse look, head-bob (CameraMovementPro), and aiming all keep working.
- The vehicle camera switch (C key) deactivates `PlayerCamera` for 3rd person and
  re-activates it for 1st — the mod applies its offset only while `PlayerCamera` is
  active, which is the entire "first-person only" rule.
- No Harmony patches, no game-code references: the camera is resolved by
  `GameObject.Find("PlayerCameraHolder")` each frame with re-find fallback
  (the game's FloatingOrigin can reparent scene objects).
- The mod reads OpenTrack's already-filtered pose; it does not do tracking itself
  (filtering/curves/deadzone/recenter stay in OpenTrack).

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

### 0.1.0-alpha

Initial release: FreeTrack 2.0 + OpenTrack UDP input, additive first-person camera
rotation with smoothing, sensitivity/invert/pitch clamp, recenter + toggle keys,
status HUD, full Apocasetter config integration.

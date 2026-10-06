# Headtracking expectations: what users of other games and mods expect

Research for 0.1.5 (round-2 audit). Short by design. It answers three questions:
what players are used to, what they complain about, and how other mods handle
setup. Sources are listed at the end. A claim from a single source is marked
*(one source)*.

## 1. Where tuning lives

- **Sims leave the tuning to the tracker software.** ETS2/ATS, DCS, IL-2 and
  MSFS take TrackIR/FreeTrack input and expose little or nothing in-game. ETS2
  even hides roll behind a `controls.sii` constant, off by default. Users shape
  the response with curves in TrackIR or OpenTrack: a large maximum deflection,
  linear or S-curves, a small deadzone at the start, and asymmetric curves
  (more sensitive looking down, to reach low gauges).
- **The usual workflow is to start linear and trim.** Guides recommend a linear
  curve with a large output range, then reduce it until the view stops
  overshooting. OpenTrack's own guides warn that smoothing above about 0.95 lags.
  Its default Accela filter smooths translation harder than rotation.
- **Consequence for an in-game mod:** the mod's own knobs should be simple
  multipliers on top of OpenTrack's output, where 1.0 means "pass through".
  They should not be a second curve editor.

## 2. Unity/BepInEx headtracking mods

There is one widespread family of OpenTrack-UDP mods with the same design across
games: Valheim, PEAK, Easy Delivery Co, Return of the Obra Dinn, Lens Island and
Luma Island. Outer Wilds has a similar mod. Their common choices:

| Topic | What they do |
|---|---|
| Input | OpenTrack UDP on port 4242 (phone apps can send to it directly). |
| Rotation sensitivity | Per-axis multipliers, **default 1.0** (Valheim, Outer Wilds). |
| Invert | Per axis, including roll (Valheim: `InvertYaw/Pitch/Roll`). |
| Translation | Full 6DOF; "lean, peek, duck". Per-axis multipliers. Outer Wilds uses lateral/vertical/depth multipliers (default 4.0 *(one source)*, in that game's own scale). |
| Translation limits | Explicit displacement limits, often asymmetric: Valheim `PositionLimitY = 0.15` up, `0.05` down (metres). |
| Smoothing | Built-in, with a note that jitter is a tracker problem. Recommended 0.2–0.4 for jittery trackers; separate defaults for local and remote (phone) trackers. |
| Hotkeys | Toggle (End), **mode cycle full → rotation-only → position-only** (Page Up), recenter (Home), with Ctrl+Shift chord alternatives for keyboards without a navigation cluster. Outer Wilds leaves centering to the tracker. |
| Aim | "Decoupled look + aim": the head moves the view and the mouse keeps aiming. |

## 3. What users expect

1. **1.0 = 1:1.** Every surveyed mod expresses sensitivity as a plain
   multiplier, and the sims' curves map degrees to degrees. Nobody tunes in
   thousandths. If the useful range is 0.001–0.05, a units bug is the more likely
   explanation, and that is exactly what 0.1.5 found (section 6).
2. **Translation is the axis people turn off.** It is noisier than rotation
   (OpenTrack smooths it harder by default), it clips into geometry, and in some
   games Z feels like zoom. Mods ship a position-only/rotation-only toggle, and
   sim guides suggest disabling or limiting individual translation axes (IL-2:
   "disable for Y", "limit Z").
3. **Translation must be bounded.** Limits exist to stop the camera clipping
   through the body, the floor, the roof or the cockpit wall.
4. **Easy recenter and toggle keys.** Guides "strongly recommend" binding
   center and toggle. A common complaint is losing center (MSFS) or a
   drifting eyepoint (IL-2), usually caused by conflicting camera bindings.
5. **Axis directions differ per game.** Players expect per-axis invert in the
   mod, and say so. Our first tester asked for exactly this.
6. **Roll matters to some players.** ETS2 users complained when it was off by
   default.
7. **Jitter is the top complaint** with webcams and phones. The fix is filtering
   in OpenTrack plus mild in-mod smoothing.
8. **Per-profile needs are handled in OpenTrack,** with one profile per game.
   None of the surveyed in-game mods keep profiles of their own.

## 4. Implications for Apocalypter Head Tracking

| Expectation | Status in 0.1.5 |
|---|---|
| Multipliers where 1 = 1:1 | **Fixed.** Translation is now camera cm per head cm (the units bug made 1:1 read as 0.01). Rotation was already degrees per degree. |
| Bounded translation | **Fixed.** ±50 cm per axis on the camera output (0.1.4 clamped the head input, so the camera could travel 50 cm × sensitivity). |
| Translation axes matching the head | **Fixed.** OpenTrack's sign convention is now applied (X and Z were reversed), and translation runs in the body frame (no sinking when looking down). |
| Per-axis invert | Present for X, Y, Z, yaw and pitch. No InvertRoll (roll is off by default; OpenTrack can invert it). |
| Recenter and toggle keys | Present (F8, optional toggle). |
| Translation on/off without menus | **Added in 0.1.6:** `Mode` + `ModeKey` (full / rotation only / lean only). |
| Asymmetric Y limit (Valheim) | Not added. The symmetric 50 cm cap is the lightweight choice; revisit if playtests show clipping through the seat or roof. |
| Rotation default 1.0 elsewhere vs 0.5 here | Kept at 0.5. Not harmful, and changing it would break config continuity. |

## 5. Config-model decision (owner's open question)

Options from `docs/tester-feedback.md`:

- (a) rescale to human-friendly units
- (b) per-axis range sliders (meta-config)
- (c) free-text fields without sliders

**Decision: (a), in the form the community already uses.** Translation becomes a
dimensionless **ratio, camera cm per head cm, 1.0 = 1:1, range 0–3, default 1.0**.
That is the same shape as the rotation sliders, so all six axes read the same way.

Why not the alternatives:

- **(b) Meta-config** doubles the keys, and Apocasetter users would need to edit
  a range before they can edit a value. No surveyed mod does this.
- **(c) Free text** loses the slider, which is the whole point of Apocasetter's
  live editing. It also loses BepInEx's range clamping, which is what keeps a
  typo from launching the camera.
- **Integer mm per cm (0–100, default 10)** is equivalent to the ratio, but it
  reads differently from the rotation sliders and would be the only integer
  sensitivity.

**The finding that settles it:** the "bottoming out" was never a scaling taste
problem. 0.1.2–0.1.4 wrote the tracker's **centimetres into `localPosition` as
Unity metres**, a factor of 100. Tester values 0.01/0.005 were really 1:1 and
0.5:1. The 0.1.2 default of 0.5 was really 50:1: a 10 cm lean moved the camera
5 m, and the 0–3 range allowed 150 m. That matches "100s of meters away".
Fixing the unit makes the honest unit (a) the natural one, and a 0–3 slider with
1.0 in the middle gives fine control where people actually tune (0.5–1.5).

Existing configs are migrated once: values ×100, so 0.01 → 1.0 and 0.005 → 0.5,
with InvertX/InvertZ flipped. Every existing setup therefore looks exactly the
same on screen. See README, "Changes in 0.1.5-alpha".

## Sources

- Valheim Head Tracking (Nexus): https://www.nexusmods.com/valheim/mods/3356
- PEAK Head Tracking (Nexus): https://www.nexusmods.com/peak/mods/167
- Easy Delivery Co Head Tracking (Nexus): https://www.nexusmods.com/easydeliveryco/mods/18
- Return of the Obra Dinn Head Tracking (Nexus): https://www.nexusmods.com/returnoftheobradinn/mods/9
- Outer Wilds Head Tracking: https://outerwildsmods.com/mods/headtracking
- ETS2 roll disabled by default (Steam): https://steamcommunity.com/app/227300/discussions/0/1742229167205219440
- ETS2/ATS webcam head tracking guide (Steam): https://steamcommunity.com/sharedfiles/filedetails/?id=3454103684
- IL-2 OpenTrack setup guide: https://forum.il2sturmovik.com/topic/34403-a-complete-guide-to-set-up-head-tracking-opentrack/page/2
- IL-2 eyepoint drift / limit Z: https://forum.il2sturmovik.com/topic/54800-eye-point-moves-of-its-own-accord/page/2/
- DCS OpenTrack settings: https://forum.dcs.world/topic/302098-opentrack-settings-and-help
- MSFS TrackIR losing center: https://forums.flightsimulator.com/t/trackir-loosing-centre-needs-resetting-constantly-with-crl-space/670914
- TrackIR 5 default Speed/Smooth: https://forums.naturalpoint.com/viewtopic.php?p=46719
- OpenTrack filters guide: https://mintlify.wiki/opentrack/opentrack/guides/filters
- OpenTrack source (translation signs): https://github.com/opentrack/opentrack/blob/master/proto-simconnect/ftnoir_protocol_sc.cpp

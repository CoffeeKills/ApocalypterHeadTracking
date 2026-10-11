# FEATURES.md — Apocalypter Head Tracking spec

## Status

Version 0.1.11-alpha. **[0.1.x]** marks items changed in a release; older tags are kept.

- [x] FreeTrack 2.0 shared-memory input (`FT_SharedMem`). **[0.1.1]** Layout and
      encoding are source-verified against OpenTrack: radians, yaw/pitch negated,
      normalized to OpenTrack's convention. Liveness comes from DataID, so a still
      head stays live. One-memcpy snapshot, no allocation. NaN/Inf/degrees-writer
      values are rejected.
- [x] OpenTrack UDP input (background listener, latest-packet cache). **[0.1.1]**
      Exact 48-byte packets, reusable buffer, the thread survives socket errors,
      exclusive bind, a busy port is reported and retried, monotonic clock.
- [x] Additive first-person camera rotation (PlayerCamera child, LateUpdate).
      **[0.1.1]** The offset is pre-multiplied, which makes the strip exact against
      CameraMovementPro. It's stripped on every hand-back path (3rd person,
      disable, scene load, runner destroyed, tracker decayed).
      **[0.1.9]** The offset lives on a dedicated `HeadTrackingOffset` rig
      between the holder and the camera (world-preserving re-parent); the mod
      never writes the camera transform, so other camera-writing mods can't
      corrupt the hand-back (issue #2).
- [x] Per-axis sensitivity, invert, pitch clamp, roll (off by default).
      **[0.1.1]** Yaw turns about the body/vehicle up axis (no horizon roll),
      mouse + head pitch held within ±89°. **[0.1.2]** Head translation: X/Y/Z cm
      move the camera in its local frame (pan/height/forward-back), smoothed and
      stripped exactly, clamped ±50 cm, new SensitivityX/Y/Z keys.
      **[0.1.3]** Translation defaults dropped to 0.01 (0.5 threw the camera on
      noisy webcam translation); new InvertX/InvertY/InvertZ keys.
      **[0.1.4]** Translation slider rescale: 0–0.05 range spans the whole
      realistic band with fine steps.
      **[0.1.5]** Translation fixed:
      - cm → m unit bug (×100)
      - OpenTrack axis signs (X/Z were reversed)
      - body frame (no sinking when looking down)
      - ±50 cm cap on the camera output
      - holder-scale compensation
      - sensitivity is now camera cm per head cm (1 = 1:1, 0–3, default 1)
      - one-time migration of 0.1.2–0.1.4 configs (×100, InvertX/Z flipped,
        `[General] ConfigVersion`)
- [x] Smoothing (exponential, configurable). **[0.1.1]** Non-finite reset, eases
      in from zero after re-enable. **[0.1.2]** Smooths translation too.
- [x] Recenter key (F8) + optional toggle key. **[0.1.1]** The neutral resets on a
      source change, DeltaAngle recenter, the toggle saves once.
- [x] Status HUD (IMGUI, source/state/live yaw-pitch). **[0.1.1]** 10 Hz rebuild,
      Repaint-only drawing, shows the source/port and "no 1st-person camera".
- [x] Apocasetter integration (`[General] Apocasetter = true`, ranged entries,
      SettingChanged live-apply)
- [x] `[Debug] LogPose` raw-value logging for protocol verification. **[0.1.1]**
      Normalized degrees + DataID/packet counter + applied offset.
- [x] `[Debug] SimulateInput` numpad fake head. **[0.1.1]** Numpad 8 = up.
      **[0.1.2]** Numpad 7/9 roll, 1/3 pan. **[0.1.5]** 1/3 sign fixed.
- [x] `tools/test-headpose.py` simulated tracker. **[0.1.1]** Encodes exactly like
      OpenTrack; H/F/N keys for the still-head, tracker-stop and NaN cases.
      **[0.1.2]** Z/X, R/V, T/G translation keys; translations encoded like
      OpenTrack. **[0.1.5]** Translation now uses OpenTrack's real signs (+X left,
      +Z back); the 0.1.4 rig mirrored the mod's sign bug.
- [x] **[0.1.5]** Research report `docs/headtracking-expectations.md` and the
      config-model decision (option (a): ratio units, 1 = 1:1).
- [x] **[0.1.6]** Tracking mode: `Mode` (full / rotation only / lean only) and
      `ModeKey` to cycle it (default None). Switching eases through the
      smoother, and the HUD shows the mode. This is the most-shipped extra in
      comparable mods (research 0.1.5).
- [x] **[0.1.10]** Mod-landscape resilience: MainCamera-tag fallback repairs a
      camera moved by another mod; `UseIsolationRig` toggle (default true) falls
      back to direct-write mode for mods that need PlayerCamera as a direct
      child. Landscape surveyed (Apocasetter index): Apocaplayer / ApocaHUD43 /
      Part Adjuster Tools are compatible or non-overlapping.
- [x] **[0.1.11]** Human lean bounds: per-axis hard caps (X 45 / Y 30 / Z 30 cm)
      plus a 50 cm radial cap, applied after sensitivity; all four configurable
      (`MaxLeanX/Y/Z/Radius`, 0–150) for setups that want different bounds.
- [ ] Head translation (x/y/z lean) — **[0.1.2] shipped: rotation + translation.
      Removed from the not-planned list.**
- [ ] Verify harness: not planned for v1 (lite-mod precedent). The 0.1.1 audit
      checked the readers and camera math off-tree (Mono + API stubs, a quaternion
      simulation; see docs/audit-0.1.1.md). Live confirmation with a real OpenTrack
      is still open: LogPose plus the "Expected in game" lines in the test rig.

## Non-goals

- No tracking/filtering of the head pose itself (OpenTrack's Kalman/Accela/curves
  remain the user's setup).
- No 3rd-person / vehicle 3rdCamera rotation.
- No Harmony patches, no reference to Assembly-CSharp or Apocasetter DLLs.

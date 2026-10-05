# FEATURES.md — Apocalypter Head Tracking spec

## Status

Version 0.1.1-alpha (audit). **[0.1.1]** marks items changed in this release.

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
- [x] Per-axis sensitivity, invert, pitch clamp, roll (off by default).
      **[0.1.1]** Yaw turns about the body/vehicle up axis (no horizon roll),
      mouse + head pitch held within ±89°. **[0.1.2]** Head translation: X/Y/Z cm
      move the camera in its local frame (pan/height/forward-back), smoothed and
      stripped exactly, clamped ±50 cm, new SensitivityX/Y/Z keys (default 0.5).
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
      **[0.1.2]** Numpad 7/9 roll, 1/3 pan.
- [x] `tools/test-headpose.py` simulated tracker. **[0.1.1]** Encodes exactly like
      OpenTrack; H/F/N keys for the still-head, tracker-stop and NaN cases.
      **[0.1.2]** Z/X, R/V, T/G translation keys; translations encoded like
      OpenTrack.
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

# FEATURES.md — Apocalypter Head Tracking spec

## Status

- [x] FreeTrack 2.0 shared-memory input (`FT_SharedMem`)
- [x] OpenTrack UDP input (background listener, latest-packet cache)
- [x] Additive first-person camera rotation (PlayerCamera child, LateUpdate)
- [x] Per-axis sensitivity, invert, pitch clamp, roll (off by default)
- [x] Smoothing (exponential, configurable)
- [x] Recenter key (F8) + optional toggle key
- [x] Status HUD (IMGUI, source/state/live yaw-pitch)
- [x] Apocasetter integration (`[General] Apocasetter = true`, ranged entries,
      SettingChanged live-apply)
- [x] `[Debug] LogPose` raw-value logging for protocol verification
- [ ] Head translation (x/y/z lean) — not planned for v1 (rotation only)
- [ ] Verify harness — not planned for v1 (lite-mod precedent; FreeTrack parsing is
      verified live via LogPose against a real OpenTrack)

## Non-goals

- No tracking/filtering of the head pose itself (OpenTrack's Kalman/Accela/curves
  remain the user's setup).
- No 3rd-person / vehicle 3rdCamera rotation.
- No Harmony patches, no reference to Assembly-CSharp or Apocasetter DLLs.

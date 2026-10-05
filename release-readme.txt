Apocalypter Head Tracking v0.1.0-alpha
======================================

Headtracking for Apocalypter's first-person view via OpenTrack.

- FreeTrack 2.0 shared-memory input (OpenTrack -> Output -> FreeTrack 2.0)
- OpenTrack UDP input (Output -> UDP over network, default port 4242)
- First-person only: on foot and while driving in 1st person; the vehicle
  3rd-person camera is never touched.
- Smoothed additive camera rotation: sensitivity per axis, invert, pitch clamp,
  roll (off by default).
- Recenter key F8 (hold-this-pose neutral), optional toggle key.
- Status HUD with live yaw/pitch.
- All settings in the Apocasetter Mods menu (F6) and in
  BepInEx\config\dev.apocalypter.headtracking.cfg.

Setup
-----
1. Install OpenTrack and start tracking (webcam face-tracking works out of the box).
2. OpenTrack: Output -> FreeTrack 2.0 (or UDP over network; then set the mod's
   Input setting to OpenTrack UDP).
3. In game, first person: press F8 while looking straight ahead.
4. Tune in the Apocasetter Mods menu (F6).

Install
-------
Copy ApocalypterHeadTracking.dll and ApocalypterHeadTracking.png into
BepInEx\plugins\ while the game is closed.

Requires BepInEx 5 (as shipped with the game's mod setup).
If the camera turns the wrong way or the axes seem swapped, enable
[Debug] LogPose = true and check BepInEx\LogOutput.log.

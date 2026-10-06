Apocalypter Head Tracking v0.1.6-alpha
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

What's new in 0.1.6
-------------------
- Tracking mode: full, rotation only, or lean only. Set Mode in the mod menu, or
  bind ModeKey (e.g. PageUp) to cycle it in-game. Switching eases smoothly, and
  the HUD shows the mode. No key is bound by default.

What's new in 0.1.5
-------------------
- Head translation (lean) fixed at the root. The mod treated centimetres as
  metres, which is why it flew the camera across the map and why the sliders sat
  at 0.005-0.05. Translation sensitivity is now simply "camera cm per head cm":
  1 = 1:1, slider 0-3, default 1.
- Lean directions now match OpenTrack: no more inverting X and Z.
- Leaning while looking up or down stays level instead of sinking into the
  ground.
- The camera never moves more than 50 cm per axis.
- Your existing settings are upgraded automatically and behave exactly as
  before (values x100, InvertX/InvertZ flipped). If you inverted X/Z in
  OpenTrack for this mod, you can now undo that there and set InvertX/InvertZ
  back to false.

What's new in 0.1.4
-------------------
- The X/Y/Z sensitivity slider now spans 0-0.05 instead of 0-3: the useful range
  fills the whole slider with fine steps (no more bottoming out at 0.001).

What's new in 0.1.3
-------------------
- Translation sensitivity defaults dropped from 0.5 to 0.01 (0.5 threw the camera
  around on noisy webcam translation). Your existing values are kept.
- New InvertX / InvertY / InvertZ keys: flip each translation axis in the mod menu
  instead of in OpenTrack.

What's new in 0.1.2
-------------------
- Head translation: sideways pan, height and forward/back lean now move the camera
  (SensitivityX/Y/Z, default 0.5, 0 = off).
- Numpad simulation: 7/9 roll, 1/3 pan.

What's new in 0.1.1
-------------------
- FreeTrack input fixed. It was ~57x too weak and turned the wrong way. If you
  raised the sensitivity to compensate, set it back to 0.5.
- Holding your head still no longer snaps the view back to center.
- Switching 1st/3rd person (C), toggling the mod, or loading a save no longer
  leaves the camera permanently twisted.
- Turning your head while looking up or down no longer tilts the horizon. The view
  can't flip over the top.
- A busy UDP port is shown on the HUD instead of failing silently.

Requires BepInEx 5 (as shipped with the game's mod setup).
If the camera turns the wrong way or the axes seem swapped, enable
[Debug] LogPose = true and check BepInEx\LogOutput.log.

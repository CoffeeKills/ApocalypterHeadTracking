# Tester feedback (anonymized), round 2 — context for the config-model research

## Round 1 (0.1.0 pre-fix build)

- "Camera yaw and pitch [move] too little... set sensitivity to max 3 both, but
  still barely moves." → explained by the 0.1.0 radians-as-degrees bug, fixed in
  0.1.1. (They also noted a non-US decimal separator locale, worried it was the
  cause; the plugin does no culture-sensitive parsing.)

## Round 2 (0.1.2, translation shipped)

- "X, Y, Z sensitivity is too high by default! With even a small head movement the
  ingame camera simply rolled over the world (under the map, above the sky, 100s
  of meters away). Setting X sensitivity to 0.01, Y and Z to 0.005 fixed this."
  → 0.1.3 dropped the default 0.5 → 0.01; 0.1.4 rescaled the range 0–0.05.
- "I had to invert X and Z in Opentrack options to work properly, if an option
  would be inside for them in mod menu, then it would make it even better."
  → 0.1.3 added InvertX/InvertY/InvertZ.
- "With good values it is simple unbelievable how amazing to drive in this way!
  I can stick my head out the car door window and shoot my gun from there... move
  head and camera to left a bit (X axis) and see what the turbo covered... moving
  my head ahead (Z axis) and turn my head... For wheel joystick users this mod is
  a must have thing." (Attached teaser screenshots.)

## Owner's open design question (config model)

The translation sliders currently run 0–0.05 with default 0.01 — users tune in the
0.001–0.05 band, which reads as "bottoming out". Options under discussion:
(a) rescale to human-friendly units (e.g. mm camera per cm head → 0–100 integers,
default 10), (b) per-axis range sliders (meta-config), (c) unconstrained free-text
fields without sliders. Owner leans against (c) and against meta-config; wants the
audit's community research to inform the final call.

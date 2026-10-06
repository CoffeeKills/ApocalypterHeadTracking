# Audit 0.1.4 → 0.1.5: reasoning trail (round 2, translation)

This uses the same format as `audit-0.1.1.md`: failure mode, fix, and why the
load-bearing facts still hold. Those facts are:

- **F1**: offsets go only on the child `PlayerCamera`.
- **F2**: first-person only.
- **F3**: per-frame code is allocation-free.
- **F4**: no game input, FSM or `ES3` writes.
- **F5**: config continuity and the Apocasetter contract.
- **F6**: the hidden runner.

Round 1's rotation work was re-read and is unchanged apart from passing the
holder pitch in (`ComputeOffset(hp)`).

## How it was checked (off-tree, as in round 1)

- **Compile.** `mcs` against UnityEngine/BepInEx stubs, extended for
  `lossyScale`, `localPosition`, vector operators and `ConfigFilePath`. It
  compiles. The one warning predates this round.
- **Round-1 reader harness.** All 26 checks still pass.
- **Migration test** (8 checks, all pass):
  - Legacy detection on realistic `.cfg` texts: a 0.1.4 file migrates; 0.1.1,
    0.1.5 and fresh files don't.
  - A comment mentioning `SensitivityX`, and a `SensitivityXtra` key, are not
    mistaken for the real key.
  - Value transform: 0.01 → 1, 0.005 → 0.5, 0.05 → 3 (clamped).
  - InvertX/InvertZ flip, InvertY is untouched.
- **Position math simulation** (Unity Euler order, all pass):
  - The strip is exact against CMP's additive shake in both LateUpdate orders:
    2000 frames with random mouse pitch, lean and shake, error 2e-15 m.
  - Units.
  - Body frame while looking down.
  - End-to-end signs.
  - Output clamp.

## 1. Translation units ×100 (the "rolled over the world" bug)

- **Failure:**
  - The readers deliver centimetres: UDP natively, FreeTrack as mm/10.
  - 0.1.2 wrote `raw cm × sensitivity` straight into `localPosition`, which is
    in Unity units (metres).
  - Default 0.5 therefore meant 50 camera cm per head cm: a 10 cm lean moved
    the camera 5 m.
  - With the 0–3 range and the ±50 cm input clamp, the camera could go up to
    150 m. The tester saw it go "under the map, above the sky, 100s of meters
    away".
  - 0.1.3/0.1.4 lowered the numbers (0.01, range 0–0.05) instead of fixing the
    unit, which is why tuning lived in the 0.001–0.05 band.
- **Fix:** `ComputePositionOffset` multiplies by 0.01 (cm → m). Sensitivity is now
  exactly "camera cm per head cm": 1 = 1:1, range 0–3, default 1.
- **Facts:** same transform and same strip path. F1–F6 untouched.

## 2. Translation axis signs (X and Z reversed)

- **Failure:** OpenTrack's translation convention is +X = left, +Y = up,
  +Z = back. Evidence:
  - OpenTrack's own SimConnect output sends `-TX` and `-TZ` (×0.01) to MSFS,
    whose eyepoint axes are +right/+forward.
  - The first tester had to invert exactly X and Z in OpenTrack.
  - (The X-Plane plugin passes values through unflipped, but its head-axis
    convention is not documented in the source, so it doesn't decide this.)

  0.1.2 mapped TX → +right and TZ → +forward.
- **Fix:** camera body vector = (−X, +Y, −Z). Documented in `HeadPose`, applied
  once in `Update`. The simulated input (Numpad 1/3) and the test rig now encode
  the OpenTrack convention.
- **Facts:** pure input mapping.
- **Migration:** see item 6. Existing setups keep their on-screen direction.

## 3. Translation frame (sinking when looking down)

- **Failure:** `localPosition` is expressed in the holder's space, and MouseLook
  rotates the holder by mouse yaw **and pitch**. At 60° mouse pitch down:
  - A 10 cm forward lean dropped the camera 8.7 cm and moved it only 5 cm
    forward.
  - A 10 cm rise went 5 cm up and 8.7 cm forward.

  Looking down at the ground or a dashboard while leaning drove the view into
  it. (The 0.1.2 comment called this "the camera's head-rotated local frame";
  it was the mouse-pitched holder frame.)
- **Fix:** the same split as the rotation offset.
  - Body frame = holder yaw, no pitch.
  - Holder = Ry(hy)·Rx(hp), so holder-local = Rx(−hp)·bodyVector.
  - Lean forward stays level; rise stays vertical (simulated: dy = 1e-17).
  - In a vehicle the body frame is the holder's parent: the cabin.
- **Facts:**
  - **F1/F4:** the holder pitch is read, never written. It is read in
    LateUpdate, after MouseLook's Update write.

## 4. Clamp on the wrong side

- **Failure:** 0.1.2–0.1.4 clamped the **head input** to ±50 cm before
  sensitivity, so the camera could reach 50 cm × sensitivity. In metres (bug 1)
  that was up to 150 m.
- **Fix:** the clamp now applies to the **camera output**: ±50 cm per axis after
  sensitivity and invert. The input is not clamped; the output bound makes that
  unnecessary.
- **Facts:** none affected. Note that 50 cm is enough to "stick my head out the
  car door window", the tester's favourite use.

## 5. Scaled rigs

- **Failure:** `localPosition` is scaled by the holder's lossyScale. On a scaled
  player or seat rig, "cm" would not be cm.
- **Fix:** the offset is divided per axis by `holder.lossyScale`, guarded against
  near-zero values. This is a no-op at scale 1. It uses a cheap property read and
  no allocation.
- **Facts:** **F3:** `Vector3` and `Quaternion` are structs.

## 6. Migration (config continuity, F5)

- **Problem:** fixing items 1 and 2 changes what a stored number means.
  Unmigrated, a tuned 0.01 would become 0.01:1 (translation effectively off),
  and every compensated setup would flip direction.
- **Fix:**
  - A new key, `[General] ConfigVersion` (int, default 2), marks the file format.
    It is internal, and its description says so.
  - Before any Bind, the file text is scanned. A file that has a `SensitivityX`
    key line but no `ConfigVersion` line was written by 0.1.2–0.1.4 and is
    migrated once:
    - X/Y/Z ×100, clamped to 0–3.
    - InvertX and InvertZ flipped.
  - That preserves on-screen behaviour exactly, both for users who compensated in
    OpenTrack and for those who used the 0.1.3 Invert keys.
  - Files without translation keys (0.1.0/0.1.1) and fresh installs just get the
    new defaults.
  - `ConfigVersion` is then written, so the migration never runs twice.
  - It runs at load only (`File.ReadAllLines` once), never per frame.
- **Default change (F5 rule):** SensitivityX/Y/Z default 0.01 → 1.0. This is the
  same on-screen behaviour expressed in the corrected unit, so it is not a
  behaviour change. The changelog calls it out as required. BepInEx keeps stored
  values, and the migration converts them.
- **Unreadable file:** treated as not legacy. Values are left alone rather than
  guessed at.
- **Residual risk:** if a user deletes the `ConfigVersion` line by hand, the
  next start multiplies their values by 100 again (clamped to 3). The key's
  description says "Do not edit".

## 7. Position hand-back (re-checked, unchanged)

- **Every hand-back path strips the exact vector last written.** RemoveOffset
  subtracts `_appliedPos` on:
  - disable
  - camera inactive
  - camera replaced
  - decay to zero
  - runner OnDisable
- **Vector addition commutes.** CMP (cinemachineMode) does
  `localPosition = localPosition + shake` without stripping its previous shake.
  Subtracting our last vector is therefore exact in either LateUpdate order, even
  though the vector changes every frame with mouse pitch (simulated: 2e-15 m
  after 2000 frames).
- **The decay path already includes position.** The early-out
  (`!_tracking && |rot| and |pos| small`) handles it.

## 8. Simulated input

- **Failure:** Numpad 1/3 wrote +X for "pan right". Under the corrected mapping
  that would pan left.
- **Fix:** Numpad 1 = head left (+X), Numpad 3 = head right (−X), which is
  OpenTrack's convention. The `SimulateInput` description now lists 7/9 roll
  and 1/3 lean.

## Residual risks (stated, not fixed)

1. **Absolute `localPosition` writers on `PlayerCamera`.** Examples would be a
   crouch transition, a seat snap, or `CameraMovementPro.ResetCamera()`. The
   analysed C# has none besides CMP's reset (no caller found), but the FSM data
   was only checked for rotation writers. If one exists, the next strip leaves
   −offset in the camera until that writer runs again. **Action:** check the
   FSM data for `SetPosition`/iTween targets on `PlayerCamera`, or watch for
   drift after crouching or entering a vehicle with your head leaned.
2. **Translation sign evidence:** OpenTrack's SimConnect output plus the tester
   report, with no live LogPose run yet. InvertX/Y/Z remain the escape hatch.
3. **Clipping:** a 50 cm cap can still put the near plane inside a car door or
   wall. This is inherent to camera-only translation; every surveyed mod limits
   rather than collides.
4. **Round-1 residual risks** (OpenTrack signs not checked live,
   `MemoryMappedFile` behaviour on Windows Mono, PlayerCamera reparenting):
   unchanged.

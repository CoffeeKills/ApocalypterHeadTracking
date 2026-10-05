# Audit 0.1.0 → 0.1.1: reasoning trail

Each item gives the failure mode, the fix, and why the fix leaves the
load-bearing facts intact. Those facts are:

- **F1**: the offset goes only on the child `PlayerCamera`, never on the holder.
- **F2**: first-person only, decided by `PlayerCamera.activeInHierarchy`.
- **F3**: Update and LateUpdate allocate nothing.
- **F4**: no game input, FSM or `ES3` writes.
- **F5**: BepInEx config is the only persistence, keys are continuous, and the
  Apocasetter contract holds.
- **F6**: the hidden runner.

## How it was checked

There is no in-tree harness (lite-mod pattern). For this audit only, off-tree:

- The whole `plugin/` was compiled with Mono `mcs` against hand-written stubs of
  the UnityEngine and BepInEx 5 signatures it uses. It compiles. The one warning
  (`_apocasetter` unused) predates 0.1.1. This proves the C# is well-formed. It
  does not prove the real API signatures match; `dotnet build` against the game
  DLLs is still the authority.
- The readers were run in that harness (26 checks, all pass):
  - FreeTrack bytes were produced with OpenTrack's own encoding expressions.
  - DataID liveness was tested at 250 Hz with a still head, a frozen mapping and
    a restart.
  - Non-finite, degree and garbage values were fed in.
  - Real-socket UDP was tested: busy port, 47/49/2000-byte junk, staleness,
    50× immediate rebind after Dispose.
  - Allocation was measured: 0 bytes over 100k FreeTrack frames.
- The camera math was simulated with quaternions in Unity's Euler order
  (Ry·Rx·Rz):
  - strip exactness against CameraMovementPro in both LateUpdate orders
  - the 0.1.0 C-key bake-in reproduced (30° head yaw → 60° camera)
  - horizon roll before and after
  - the ±89° total-pitch clamp
- The test rig's new encoding was cross-checked byte-for-byte against both
  decoders: they agree, with right = +yaw and up = −pitch.

## Protocol facts (verified against OpenTrack source, not memory)

| Source file | Fact |
|---|---|
| `freetrackclient/fttypes.h` | `FTData { uint32 DataID; int32 CamWidth, CamHeight; float Yaw /*+left*/, Pitch /*+up*/, Roll /*+left*/, X, Y, Z; …raw…; }`, map `FT_SharedMem` |
| `proto-ft/ftnoir_protocol_ft.cpp` `pose()` | `Yaw = -headpose[Yaw]*d2r`, `Pitch = -headpose[Pitch]*d2r`, `Roll = +headpose[Roll]*d2r`, `X/Y/Z = headpose*10`; `InterlockedAdd(DataID, 1)` per call, zeroed on game-ID change |
| `proto-udp/ftnoir_protocol_ftn.cpp` | `writeDatagram((const char*)headpose, sizeof(double[6]))` |
| `api/plugin-api.hpp` | `enum Axis { TX=0, TY=1, TZ=2, Yaw=3, Pitch=4, Roll=5 }` |

From these, OpenTrack's internal convention is +yaw = right, +pitch = down,
+roll = left. FreeTrack's documented +left/+up is reached by negating yaw and
pitch, so the internal convention is the opposite of those. That convention maps
directly onto Unity's `Quaternion.Euler(x = pitch, y = yaw, z = roll)`:

- +x tilts forward down
- +y turns right
- +z tilts the camera's up vector toward −x (rolls left)

**Still open:** the sign of each axis is derived from source, not observed on a
live OpenTrack. `InvertYaw`/`InvertPitch` remain, and LogPose prints
normalized values for the check.

---

## 1. FreeTrack units and signs

- **Failure:** 0.1.0 read the floats as degrees. They are radians, so the
  response was 1/57.3 of intended, and yaw and pitch also turned the wrong way.
  At sensitivity 0.5, a 30° head turn moved the camera 0.26° to the left. The
  old rig wrote degrees with no sign flips, mirroring the reader, so the bug
  could not show up in testing.
- **Fix:** `FreeTrackPipe.Decode` produces yaw = −Yaw·r2d, pitch = −Pitch·r2d,
  roll = +Roll·r2d, translations in mm/10. Offsets are named constants, and every
  conversion is one line. Values beyond 6.3 rad are rejected with a one-time
  warning (a degrees writer).
- **Facts:** reader-only change. F1–F6 untouched.

## 2. Still head reported as stale

- **Failure:** liveness meant "the 24 pose bytes changed within 0.5 s". A
  settled filter or deadzone gives bit-identical output, so the view decayed to
  center while the user was holding a turned head.
- **Fix:**
  - DataID (incremented on every OpenTrack pose, about 250 Hz) is the liveness
    signal once it has been seen ticking.
  - If it never ticks, liveness falls back to the byte change.
  - The first read of a mapping is never live. The mapping outlives the tracker
    because our handle keeps it open, so a stale pose would otherwise show for
    0.5 s.
  - Window: 0.5 s. That is more than 100 DataID ticks, so it can't false-trigger.
- **Facts:** reader-only. F3 checked by measurement (0 bytes).

## 3. Offset baked in on camera hand-back

- **Failure:** when the camera went inactive (C key → 3rd person), 0.1.0 set
  `_lastApplied = identity` but left H in the transform. On reactivation it
  stripped identity and applied H again, so the camera ended at 2H, permanently.
  Simulation: 30° → 60°. The same pattern hit two other cases:
  - a scene load, where a new camera was found while the old `_lastApplied`
    was kept, putting H⁻¹ on a fresh camera
  - runner destruction, which never stripped
- **Fix:** one invariant, documented in `HeadTrackingRuntime`: H is on
  `_appliedCam` exactly when `_hasOffset`. `RemoveOffset()` restores
  `H⁻¹ · local` and runs on every hand-back path:
  - camera inactive
  - `Enabled` off
  - camera identity changed
  - tracker decayed to zero
  - `OnDisable` (runner destroyed or re-created)
  A destroyed camera (Unity null) is just forgotten.
- **Facts:**
  - **F2:** stripping writes the *inactive* `PlayerCamera` once, and only to
    restore the game's own value. That is the opposite of applying an offset. The
    vehicle `3rdCamera` is a different object and is never referenced.
  - **F1:** the holder is never written.

## 4. Disable left the camera rotated

- **Failure:** `LateUpdate` returned early when `!Enabled`, so the "identity next
  LateUpdate" in the comment never happened. The README promise ("Off = exactly
  vanilla") was false, and re-enabling baked H in as in item 3.
- **Fix:** `!Enabled` → `RemoveOffset()`. Update zeroes the smoother, so
  re-enable eases in from zero.
- **Facts:** as item 3.

## 5. Strip order against CameraMovementPro

- **Failure:** CMP (cinemachineMode, shipped default) writes
  `local = local · C` every LateUpdate and never strips its last C. Its
  execution order against our LateUpdate is undefined. With 0.1.0's
  `local · lastApplied⁻¹ · H`, CMP's C lands between H and H⁻¹, so the game's own
  camera state accumulates `H·C·H⁻¹` (the shake about head-rotated axes) instead
  of C. With an exaggerated ±1°/frame shake, the simulation drifted 7–10° from
  vanilla in 600 frames. Real shakes are small and transient, so in practice
  this is smaller, but it is non-zero and it accumulates.
- **Fix:** pre-multiply: `local = H · vanilla`, strip `H⁻¹ · local`. CMP's
  right-multiplied C commutes past our left factor, so the strip is exact in
  both orders. Simulated error: 0.00000° and 0.00001° (float noise).
- **Facts:**
  - **F1:** still the child camera only.
  - Additivity: mouse look is untouched (it sits on the holder), and CMP's shake
    now applies in the final view frame, which is what a view shake should do.
- **Residual risk:** if something calls `CMP.ResetCamera()` (an absolute write;
  no caller found), the next strip leaves H⁻¹ in the transform until the next
  reset.

## 6. Horizon roll when turning the head while looking up or down

- **Failure:** the offset `Euler(pitch, yaw, roll)` on the child turns about the
  holder's up axis, and the holder is pitched by the mouse. At mouse pitch 45°,
  a 30° head yaw rolls the horizon 20.7°; at 70° pitch, 28°.
- **Fix:**
  - The holder is `Ry(hy)·Rx(hp)`: MouseLook writes `(x, y, 0)`, and we only
    read it.
  - The wanted camera is `Ry(hy+yaw)·Rx(hp+pitch)·Rz(roll)`.
  - So the child offset is `Rx(−hp)·Ry(yaw)·Rx(hp+pitch)·Rz(roll)`.
  - With yaw = 0 this is exactly 0.1.0's `Rx(pitch)·Rz(roll)` (checked).
  - Yaw now turns about the holder's parent up axis: body on foot, vehicle
    cabin when driving.
- **Facts:**
  - **F1/F4:** the holder rotation is only read, in LateUpdate, after
    MouseLook's Update write.
  - Body yaw (`Direction`/Rigidbody from the holder) is unaffected: head look
    stays free-look.

## 7. View flipping past vertical

- **Failure:** the holder is clamped to ±80° and head pitch up to `MaxPitch`
  (80°), so up to 160° in total, past the pole. The view turned upside down.
- **Fix:** head pitch is clamped to `[−89−hp, 89−hp]` on top of `MaxPitch`.
- **Facts:** `MaxPitch` keeps its key, default and meaning (it is still the
  head-offset clamp). The extra clamp only applies where the old behaviour was
  broken.

## 8. NaN/Inf poisoning

- **Failure:** FreeTrack had no finite check, and UDP checked only yaw and pitch
  for NaN (not roll, not Inf). One NaN made `_smooth` NaN forever. `Lerp` keeps
  NaN, `MoveTowardZero` cannot recover it, and the camera rotation turns into NaN.
- **Fix:** both readers reject non-finite angles and anything beyond ±360°. The
  runtime resets `_smooth` if it is ever non-finite.
- **Facts:** none affected.

## 9. Busy UDP port

- **Failure:** `new OpenTrackUdp(port)` threw inside `Update`, every frame:
  exception spam, and the source was re-constructed 60×/s.
  Separately, Mono enables `SO_REUSEADDR` on UDP sockets by default. Confirmed
  on Mono: the second bind succeeds. Two listeners would then share the port
  silently, each getting only some datagrams, so "waiting" showed with no error.
- **Fix:**
  - Construction is wrapped in a try/catch: one warning in the log, the HUD shows
    `UDP port N unavailable`, retry every 2 s.
  - The half-built socket is closed.
  - `ExclusiveAddressUse = true` (wrapped in its own try) makes a busy port fail
    at bind.
  - Verified: a second bind throws `AddressAlreadyInUse`, and the same port
    re-binds immediately after Dispose 50/50 times. That matters for a port
    change A→B→A.
- **Facts:** none affected.

## 10. UDP listener lifecycle

- **Failure:** any `SocketException` returned from the thread, not only the
  close-time one. The listener died for good while the HUD said "waiting".
  Packets longer than 48 bytes were accepted. `UdpClient.Receive` allocated per
  packet.
- **Fix:**
  - The raw `Socket` receives into a reused 512-byte buffer.
  - Only `n == 48` is accepted (junk of 47, 49 and 2000 bytes is ignored, and
    the thread survives).
  - Every exception leads to `continue` unless `_stopping`.
  - `ObjectDisposedException` leads to return.
  - A 250 ms receive timeout makes the stop flag cooperative.
- **Facts:** background thread only; the main thread reads under a lock.

## 11. UDP clock

- **Failure:** `DateTime.UtcNow` is wall-clock time. An NTP or DST adjustment
  could mark a live stream stale, or a stale one live, for that long.
- **Fix:** `Stopwatch.GetTimestamp()` is monotonic, allocation-free and
  thread-safe. FreeTrack uses the same clock.
- **Facts:** none affected.

## 12. Per-frame allocation and torn reads

- **Failure:**
  - FreeTrack did `new byte[24]` every frame (an F3 violation).
  - It read the floats with six `ReadSingle` calls and then the bytes again with
    `ReadArray`. The liveness bytes and the returned floats could come from
    different writer frames.
  - On older Mono, `ReadSingle` goes through generic `SafeBuffer.Read<T>`, which
    can box.
  - The HUD concatenated its line on every OnGUI call, several times a frame.
- **Fix:**
  - One `Marshal.Copy` of 36 bytes from the view handle into a reused buffer.
    The view is mapped at offset 0, so the handle is the struct start on both
    .NET and Mono. All decoding uses `BitConverter` on that snapshot. Measured:
    0 bytes over 100k frames.
  - The HUD line is rebuilt at 10 Hz and drawn on `Repaint` only.
- **Facts:** F3 restored.

## 13. Recenter across sources and ±180 wrap

- **Failure:** the neutral captured on FreeTrack was applied to UDP or simulated
  poses after a source switch. The subtraction `raw − center` jumped 360° when it
  crossed ±180.
- **Fix:** the neutral is dropped on a source or port change. Subtraction uses
  `Mathf.DeltaAngle`.
- **Facts:** none affected.

## 14. SimulateInput pitch

- **Failure:** Numpad 8 made pitch positive, which in the normalized convention
  means looking down.
- **Fix:** Numpad 8 now makes pitch negative (look up).
- **Facts:** debug only.

## 15. Toggle key wrote the config twice

- **Failure:** `SetEnabled` set the value (`SaveOnConfigSet` already saves) and
  then called `Save()` again.
- **Fix:** save explicitly only when `SaveOnConfigSet` is off.
- **Facts:** **F5:** BepInEx config is still the only persistence.

## Design: settings are polled, not pushed

`OnSettingsChanged` in 0.1.0 had two side effects:

- It nulled the camera references on every config edit. That was harmless only
  because `_lastApplied` happened to survive.
- It disposed the input mid-frame.

The runtime now compares `SimulateInput`/`InputMode`/`UdpPort` with its current
source once per Update. That is a few int compares and no allocation. The
Apocasetter contract is unchanged:

- `SettingChanged` still pushes into `HeadTrackingSettings` live (**F5**).
- `ModConfig.SettingsChanged` is still raised.

## Design: camera resolution

The camera is cached. `GameObject.Find` runs at most every 0.5 s, and only when:

- the cached camera is destroyed,
- the camera is no longer a child of the holder, or
- the camera is inactive (a respawn could leave an old, inactive player).

`sceneLoaded` resets the throttle so the new player is found at once. In 0.1.0,
`Find` ran every frame whenever no holder existed (menus).

## Config continuity (F5)

- No key added, renamed, removed or re-defaulted. The GUID is unchanged.
- `Input` gains `AcceptableValueList<int>(0, 1)`. BepInEx maps an invalid value
  to the list's first entry, 0 = FreeTrack. In 0.1.0 the runtime also treated
  "not 1" as FreeTrack, so the meaning is unchanged.

## Residual risks (not fixed, stated)

1. **Live sign check.** The axis signs follow from OpenTrack's source but are
   not yet observed on a live OpenTrack. Use the rig's "Expected in game" lines
   or LogPose.
2. **Mono `MemoryMappedFile.OpenExisting` on Windows.** It is the same API 0.1.0
   used and cannot be exercised here.
3. **`CameraMovementPro.ResetCamera()`.** See item 5.
4. **`PlayerCamera` reparented away from the holder** with a different parent
   rotation. The strip happens in the new local space, which is inexact. The
   camera-facts hierarchy says the camera stays under the holder.
5. **Weapons and hands turn with the head.** They are children of
   `PlayerCamera`, so aim follows the head. Unchanged from 0.1.0; it is a
   design property, not a regression.
6. **`RecenterKey`/`ToggleKey` use only `MainKey`.** Modifiers in the shortcut
   are ignored on purpose: BepInEx's `IsDown()` refuses when other modifiers
   are held, which would break recenter while sprinting with Shift.

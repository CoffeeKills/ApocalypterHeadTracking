# Camera facts — how Apocalypter's first-person camera actually works

All of this is decoded from the game's serialized scene/FSM data (PlayMaker drives
the camera; there is no C# first-person controller). The relevant decompiled C# is
in `../gamecode/`.

## Hierarchy (level1 scene)

- `PlayerCameraHolder` (path_id 75) — rotation pivot, receives mouse look.
- `PlayerCameraHolder/PlayerCamera` (path_id 1842) — the actual Camera, tagged
  `MainCamera`; children: weapons arms, quick items, hands, flashlight.
- `Direction` (2210) — body yaw holder; `Player` (2311) — Rigidbody, owns the Look FSM.

## Mouse look (FSM `Look` on `Player`, state `Look`)

- `MouseLook` "Mouse Look PlayerCamera X": target **PlayerCameraHolder**, axes
  MouseX, sensitivity = FSM var `X_sensi`, no clamp, everyFrame.
- `MouseLook` "Mouse Look PlayerCamera Y": target **PlayerCameraHolder**, axes
  MouseY, sensitivity = `Y_sensi`, pitch clamped −80°..+80°, everyFrame.
- Then `GetRotation`(holder) → `SetRotation`(`Direction`, yaw only) →
  `RigidBodyMoveRotation`(Player body) + cursor lock.

`MouseLook` (gamecode/MouseLook.cs) assigns `localEulerAngles` absolutely each
frame and **forces z (roll) to 0**. Consequences:

1. Anything written to the holder's rotation is erased the same frame → the
   headtracking offset lives on the **child PlayerCamera** instead.
2. `PlayerCamera` itself has **no per-frame rotation writer** anywhere in the FSM
   data (zoom touches FOV, weapons iTween weapon GOs, nothing writes the camera
   GO's localRotation).

## Head-bob / shake (CameraMovementPro)

`CameraMovementController` applies position/rotation/FOV offsets in **LateUpdate**.
Its serialized default `cinemachineMode = true` reads the current localRotation
once per frame and multiplies its offset — additive-friendly. **Correction
(0.1.1 audit):** it multiplies on the RIGHT (`local = local * shake`) and does not
strip the previous frame's shake, so its increments accumulate in the transform. A
mod offset that is post-multiplied and stripped from the right gets interleaved
with those increments. The mod therefore pre-multiplies (`H * vanilla`), which
strips exactly in either LateUpdate order. `ResetCamera()` writes an absolute
`originalRotation` when it has no layers. No caller was found in the analysed
data. If one exists, the mod's next strip would leave the inverse of the previous
head offset in the transform until the next reset. That is a residual risk, listed
in docs/audit-0.1.1.md.

Position (0.1.5): in cinemachine mode CMP likewise does
`localPosition = cachedLocalPosition + shake`. It adds onto whatever is there and
does not strip its previous shake, so the mod's additive translation strips
exactly in either LateUpdate order. `PlayerCamera.localPosition` is in the
**holder's** space: Unity metres, rotated by mouse yaw AND pitch. The mod
converts head cm to metres and removes the mouse pitch (body frame). Absolute
position writers on PlayerCamera (crouch, seat snap) were NOT checked in the FSM
data; see docs/audit-0.1.5.md, residual risk 1. The non-cinemachine
mode snaps to a cached originalRotation; if that flag is ever off, a hard conflict
would exist (it is on in the shipped scene).

## Vehicle camera switch (C key, `DriveTrigger/Camera` FSM)

- 3rd person: activates the vehicle's `3rdCamera` (own Camera + own MouseLook FSM,
  pitch clamped ±70°) and **deactivates `PlayerCamera`**.
- 1st person: re-activates `PlayerCamera`, deactivates `3rdCamera`.
- No SetMainCamera/GetMainCamera anywhere — switching is pure GO activate/deactivate.

→ checking `PlayerCamera.activeInHierarchy` is the entire "first-person only" rule,
including 1st-person driving.

## NWH cameras (inert in this game)

`CameraMouseDrag` overwrites transform in LateUpdate when active; its input provider
reads axes `CameraRotationX/Y/Zoom` which **do not exist** in the game's
InputManager — the NWH mouse-drag path never receives input. The FSM path above is
the live one.

## Other hazards

- **FloatingOrigin** (Assembly-CSharp) may reparent/rebase scene GOs — resolve the
  camera per-frame with re-find fallback, never cache blindly across scene changes.
- Death/menu states (`LookStop`/`Reset`) don't snap the camera transform.
- Input axes: legacy InputManager; `Mouse X`/`Mouse Y` sens 0.1, no deadzone.

## 0.1.9: isolation rig (mod-conflict fix)

The mod re-parents `PlayerCamera` (world-preserving) under its own
`HeadTrackingOffset` child of the holder and writes ONLY that rig — the camera
transform itself is never touched. Verified safe against the game data:

- `setGlobalGO` FSM resolves holder + camera by name/tag (`FindGameObject`).
- Every `GetChild`+camera co-occurrence in the FSM dump targets the vehicle
  `3rdCamera` hierarchy (zoom dolly + camera-switch), never the holder's children.
- `CameraMovementController` obtains its transform via `GetComponent<Camera>()`
  on the camera GO — unaffected by re-parenting.

Consequence: any other writer of the camera transform (CMP shake, third-party
head-bob mods) composes BELOW the rig, so the hand-back (rig at identity) is
exact by construction — the 0.1.1–0.1.8 CMP-interleave invariant is obsolete.
Residual risk: a mod that re-parents or destroys `PlayerCamera` itself (the rig
repairs on the next resolve; resolve = throttled find, name-based).

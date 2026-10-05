# Task: audit and rework "Apocalypter Head Tracking" (BepInEx mod for the Unity game Apocalypter)

Read **README.md first** — it contains the verified game-architecture facts that drive
every unusual design decision in this codebase. Treat those facts as load-bearing: a
rework must preserve them. The decompiled game code you need is in `gamecode/`,
`docs/camera-facts.md` records the FSM-level camera analysis (the Look FSM, the
vehicle camera switch), and **CONTEXT.md** carries the current project state and
workflow — skim it once. `tools/test-headpose.py` is the test rig (a simulated
tracker: FreeTrack shared memory + OpenTrack UDP); you cannot run it, but it is the
only existing test tooling — reason through it carefully.

## What is open today

- The FreeTrack field order (yaw,pitch,roll,x,y,z after the 3-word header) and the
  OpenTrack UDP field order (x,y,z then yaw,pitch,roll as 6 LE doubles) are written
  per protocol memory and **not yet verified against a real OpenTrack** — review the
  readers for correctness and defensiveness (endianness, bounds, malformed packets,
  NaN), and make field-order mistakes impossible or trivially fixable.
- The stale-pose staleness window (0.5 s of unchanged bytes) is a first cut.
- The additive math (`baseLocal = localRotation * Inverse(lastApplied)` in LateUpdate)
  and the smoothing/recenter semantics deserve adversarial review: camera
  deactivation mid-offset, scene loads, FloatingOrigin reparents, tracker loss,
  toggle/recenter key races, and the UDP listener thread's lifecycle.

## Your job

1. **Audit** `plugin/` against the game code and the README. Find real bugs —
   correctness, camera-fight edge cases, input-provider lifecycle/threading,
   config-live-edit races, allocation hazards in the per-frame paths — not style
   nits.
2. **Rework** what you find. Keep the mod lightweight: no Harmony patches unless a
   fix genuinely cannot work without one (state it in the changelog), no NuGet
   dependencies, no uGUI panels — the IMGUI HUD is the only on-screen element.
   Preserve the architecture: hidden runner object, additive child-camera rotation,
   first-person-only active-check, BepInEx-config-only persistence.
3. **Test by reasoning.** There is no runnable verify harness (this mod follows the
   lite-mod pattern): every change must include a written reasoning trail — the
   failure mode it fixes and why the fix cannot regress the load-bearing facts.
4. **Update the README**: add a "Changes in \<new version\>" section listing bugs
   fixed and design changes, any new config keys, and migration rules for existing
   config files. Version below 1.0 (e.g. `0.1.1-alpha`; the BepInEx version string
   itself must stay numeric-only, e.g. `0.1.1`).

## Hard constraints

- **The offset goes on the child `PlayerCamera` localRotation, never on
  `PlayerCameraHolder`** — the game's PlayMaker MouseLook actions overwrite the
  holder's rotation every frame (roll forced to 0). See `gamecode/` and
  `docs/camera-facts.md`.
- **First-person only**: the offset is applied only while `PlayerCamera` is
  activeInHierarchy. The vehicle 3rdCamera and menus must never be touched.
- Allocation-free `Update`/`LateUpdate` (they run every frame, and
  `CameraMovementPro` also runs in LateUpdate).
- Never call `ES3.Save` — the game's save data is read-only for mods.
- Never write game input, PlayMaker FSM variables, or `vc.input.*` state.
- BepInEx config is the only persistence. Config continuity: the plugin GUID
  (`dev.apocalypter.headtracking`) must not change; no existing key may be renamed,
  removed or default-changed (the config file ships to real users).
- Keep the Apocasetter contract: `[General] Apocasetter = true`, ranged/described
  entries, `SettingChanged` → live runtime push. Never reference Apocasetter's DLL.
- The hidden-runner pattern (`HideAndDontSave` + `DontDestroyOnLoad`, re-created on
  scene load) exists because the game destroys plugin-created objects on scene load.

## Deliver

The reworked `plugin/`, the updated `README.md` and `FEATURES.md` (Status section
refreshed, each change marked), in the same bundle layout, with a short list of what
you changed and why — each item tied to the failure mode it addresses.

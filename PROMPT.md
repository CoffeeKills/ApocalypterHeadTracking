# Task: audit and rework "Apocalypter Head Tracking" (BepInEx mod for the Unity game Apocalypter) — round 2

Read **README.md first** — it contains the verified game-architecture facts that drive
every unusual design decision in this codebase. Treat those facts as load-bearing: a
rework must preserve them. The decompiled game code you need is in `gamecode/`,
`docs/camera-facts.md` records the FSM-level camera analysis, `docs/audit-0.1.1.md` is
round 1's reasoning trail (all items landed), `docs/tester-feedback.md` holds the
anonymized tester feedback that motivated 0.1.2–0.1.4, and **CONTEXT.md** carries the
current project state and workflow — skim all once. `tools/test-headpose.py` is the
test rig (a simulated tracker that encodes exactly like OpenTrack); you cannot run
it, but it is the only existing test tooling — reason through it carefully.

## What is open today

1. **Research task (new, do this first): headtracking-mod expectations.** Research
   how headtracking is configured and experienced in other games and mods — TrackIR
   and OpenTrack/FaceTrackNoIR in sims (ETS2/ATS, Assetto Corsa, flight sims, Arma,
   Farming Sim, other Unity/BepInEx headtracking mods). Deliver a short report:
   what users expect and want — typical default sensitivity/curves/smoothing,
   recenter and toggle UX, whether translation (lean) is commonly used or disabled,
   per-profile needs, common complaints and how other mods solve the setup problem.
2. **Config-model decision.** Use that research to answer the owner's open design
   question (docs/tester-feedback.md): the translation sliders currently run
   0–0.05 with default 0.01, and the owner dislikes that users tune in the
   0.001–0.05 band ("bottoming out"). Evaluate: (a) rescale to human-friendly units
   (e.g. mm camera per cm head → 0–100 integers, default 10), (b) per-axis range
   sliders (meta-config), (c) unconstrained free-text fields without sliders.
   Pick one (or argue a better option) and implement it. Keep Apocasetter's live
   slider editing; keep the mod lightweight.
3. **Audit** `plugin/` against the game code and the README. Find real bugs —
   correctness, camera-fight edge cases, input-provider lifecycle/threading,
   config-live-edit races, allocation hazards in the per-frame paths — not style
   nits. Round 1 was thorough; 0.1.2–0.1.4 (translation) landed after it and have
   had no adversarial review: the position-offset hand-back, the translation
   clamps, the Invert keys and the 0–0.05 rescale deserve the same scrutiny.
4. **Rework** what you find. Keep the mod lightweight: no Harmony patches unless a
   fix genuinely cannot work without one (state it in the changelog), no NuGet
   dependencies, no uGUI panels — the IMGUI HUD is the only on-screen element.
   Preserve the architecture: hidden runner object, additive child-camera rotation
   and translation, first-person-only active-check, BepInEx-config-only persistence.
5. **Test by reasoning.** There is no runnable verify harness (lite-mod pattern):
   every change must include a written reasoning trail — the failure mode it fixes
   and why the fix cannot regress the load-bearing facts. Extend the camera-math
   reasoning to the position offset where it interacts with CameraMovementPro.
6. **Update the README**: add a "Changes in \<new version\>" section listing bugs
   fixed and design changes, any new config keys, and migration rules for existing
   config files. Version below 1.0 (e.g. `0.1.5-alpha`; the BepInEx version string
   itself must stay numeric-only, e.g. `0.1.5`).

## Hard constraints

- **The offset goes on the child `PlayerCamera` localRotation/localPosition, never
  on `PlayerCameraHolder`** — the game's PlayMaker MouseLook actions overwrite the
  holder's rotation every frame (roll forced to 0). See `gamecode/` and
  `docs/camera-facts.md`. CameraMovementPro (head-bob/shake) multiplies its shake
  on the RIGHT of localRotation each LateUpdate and ADDS its position delta — the
  mod's pre-multiplied rotation and additive position follow from that; preserve
  the exact-strip property.
- **First-person only**: offsets are applied only while `PlayerCamera` is
  activeInHierarchy. The vehicle 3rdCamera and menus must never be touched.
- Allocation-free `Update`/`LateUpdate` (they run every frame).
- Never call `ES3.Save` — the game's save data is read-only for mods.
- Never write game input, PlayMaker FSM variables, or `vc.input.*` state.
- BepInEx config is the only persistence. Config continuity: the plugin GUID
  (`dev.apocalypter.headtracking`) must not change; no existing key may be renamed
  or removed; changing an existing key's DEFAULT is allowed only where the old
  default was harmful, must be called out in the changelog, and must keep loading
  existing config files with their stored values (BepInEx writes defaults only on
  first bind).
- Keep the Apocasetter contract: `[General] Apocasetter = true`, ranged/described
  entries, `SettingChanged` → live runtime push. Never reference Apocasetter's DLL.
- The hidden-runner pattern (`HideAndDontSave` + `DontDestroyOnLoad`, re-created on
  scene load) exists because the game destroys plugin-created objects on scene load.

## Deliver

1. The **expectations report** (a markdown file in `docs/`).
2. The reworked `plugin/`, the updated `README.md` and `FEATURES.md` (Status
   section refreshed, each change marked), in the same bundle layout, with a short
   list of what you changed and why — each item tied to the failure mode or
   research finding it addresses.

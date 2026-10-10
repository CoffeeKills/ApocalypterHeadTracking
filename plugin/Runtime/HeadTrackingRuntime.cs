using System;
using ApocalypterHeadTracking.Input;
using ApocalypterHeadTracking.Persistence;
using ApocalypterHeadTracking.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ApocalypterHeadTracking.Runtime
{
    /// <summary>
    /// Reads the tracker pose in Update, applies a smoothed additive rotation to the
    /// first-person camera in LateUpdate.
    ///
    /// Camera facts (README, load-bearing): PlayMaker MouseLook overwrites
    /// PlayerCameraHolder's localEulerAngles every frame (roll forced to 0), so the
    /// offset goes on the CHILD "PlayerCamera", which has no absolute per-frame
    /// rotation writer. The vehicle camera switch deactivates PlayerCamera for 3rd
    /// person — activeInHierarchy is the whole "first-person only" rule.
    ///
    /// Offset invariant (0.1.1): our offset H is on the camera transform iff
    /// _hasOffset, and then localRotation == H * vanilla, with H the exact value we
    /// wrote last. Every path that stops applying (disabled, camera inactive,
    /// camera replaced, tracker decayed to zero, runner disabled/destroyed) strips
    /// H first — so the camera is always handed back exactly as the game left it.
    ///
    /// Why H is PRE-multiplied (0.1.1): CameraMovementPro (cinemachineMode) does
    /// localRotation = localRotation * shake every LateUpdate, i.e. it multiplies
    /// on the RIGHT of whatever is there. With H on the left, stripping H^-1 from
    /// the left is exact no matter whether CMP's LateUpdate runs before or after
    /// ours (their execution order is undefined). The old post-multiply strip
    /// (local * Inverse(lastApplied)) left CMP's shake conjugated by H every frame.
    /// </summary>
    public class HeadTrackingRuntime : MonoBehaviour
    {
        // ------------------------------------------------------------- HUD read-outs
        public string Status = "off";
        public bool CameraActive;
        public float DisplayYaw;
        public float DisplayPitch;

        private const string HolderName = "PlayerCameraHolder";
        private const string CameraName = "PlayerCamera";
        private const string OffsetGoName = "HeadTrackingOffset";
        private const float ResolveInterval = 0.5f;      // throttled GameObject.Find
        private const float InputRetryInterval = 2f;     // e.g. UDP port busy
        private const float TotalPitchLimit = 89f;       // holder pitch + head pitch

        private const int SourceNone = -1;
        private const int SourceSimulated = 100;

        // ------------------------------------------------------------- input state
        private ITrackerInput _input;
        private int _source = SourceNone;
        private int _sourcePort;
        private float _inputRetryAt;
        private bool _inputErrorLogged;
        private string _inputError;

        private HeadPose _center;
        private bool _haveCenter;
        private Vector3 _smooth;        // smoothed (pitch, yaw, roll) offset, degrees
        private Vector3 _smoothPos;     // smoothed camera translation, cm, body frame (+x right, +y up, +z forward)
        private bool _tracking;
        private float _nextLog;

        // ------------------------------------------------------------- camera state
        private Transform _holder;
        private Transform _cam;
        private Transform _offsetGo;    // our dedicated rig child of the holder
        private float _nextResolve;
        private bool _rigActive = true; // which mode the camera state is in

        // Legacy direct-write mode (UseIsolationRig = false) only:
        private Transform _appliedCam;  // the transform that currently carries H
        private Quaternion _applied = Quaternion.identity;
        private Vector3 _appliedPos;
        private bool _hasOffset;
        private bool _hasPos;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            // Runner disabled/destroyed (scene sweep, re-creation, quit): hand the
            // camera back and release the socket/mapping.
            RemoveOffset();
            DisposeInput();
            _source = SourceNone;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // New scene, likely a new player hierarchy: look for it right away
            // instead of waiting out the resolve throttle.
            _nextResolve = 0f;
        }

        // =================================================================== Update

        private void Update()
        {
            if (HeadTrackingSettings.ToggleKey != KeyCode.None
                && UnityEngine.Input.GetKeyDown(HeadTrackingSettings.ToggleKey))
            {
                ModConfig.SetEnabled(!HeadTrackingSettings.Enabled);
            }
            if (HeadTrackingSettings.ModeKey != KeyCode.None
                && UnityEngine.Input.GetKeyDown(HeadTrackingSettings.ModeKey))
            {
                ModConfig.CycleMode();
            }

            if (!HeadTrackingSettings.Enabled)
            {
                // LateUpdate strips the offset; re-enabling eases in from zero.
                _smooth = Vector3.zero;
                _smoothPos = Vector3.zero;
                _tracking = false;
                Status = "off";
                return;
            }

            SyncSource();

            HeadPose raw;
            if (!TryReadPose(out raw))
            {
                _tracking = false;
                Status = _inputError ?? "waiting for tracker";
                MoveTowardZero();
                return;
            }

            if (HeadTrackingSettings.RecenterKey != KeyCode.None
                && UnityEngine.Input.GetKeyDown(HeadTrackingSettings.RecenterKey))
            {
                _center = raw;
                _haveCenter = true;
            }

            // DeltaAngle: a center near ±180 must not produce a 360° jump.
            float yaw = _haveCenter ? Mathf.DeltaAngle(_center.Yaw, raw.Yaw) : raw.Yaw;
            float pitch = _haveCenter ? Mathf.DeltaAngle(_center.Pitch, raw.Pitch) : raw.Pitch;
            float roll = _haveCenter ? Mathf.DeltaAngle(_center.Roll, raw.Roll) : raw.Roll;
            // Head translation relative to the neutral, OpenTrack convention
            // (+X left, +Y up, +Z back — see HeadPose) → camera body frame
            // (+x right, +y up, +z forward). 0.1.2–0.1.4 used X and Z unflipped,
            // which is why testers had to invert both.
            float dx = -(_haveCenter ? raw.X - _center.X : raw.X);
            float dy = _haveCenter ? raw.Y - _center.Y : raw.Y;
            float dz = -(_haveCenter ? raw.Z - _center.Z : raw.Z);

            float targetYaw = yaw * HeadTrackingSettings.SensitivityYaw * (HeadTrackingSettings.InvertYaw ? -1f : 1f);
            float targetPitch = pitch * HeadTrackingSettings.SensitivityPitch * (HeadTrackingSettings.InvertPitch ? -1f : 1f);
            float targetRoll = roll * HeadTrackingSettings.SensitivityRoll;
            float maxPitch = HeadTrackingSettings.MaxPitch;
            targetPitch = Mathf.Clamp(targetPitch, -maxPitch, maxPitch);

            Vector3 target = new Vector3(targetPitch, targetYaw, targetRoll);
            // Camera cm, clamped AFTER sensitivity with HUMAN bounds by default:
            // per-axis limits plus a radial cap, so the camera cannot leave the
            // character by more than a person physically could — whatever the
            // sliders or a spiking tracker say. All four caps are config keys
            // (MaxLeanX/Y/Z/Radius) for setups that want something else (0.1.11).
            Vector3 targetPos = new Vector3(
                Mathf.Clamp(dx * HeadTrackingSettings.SensitivityX * (HeadTrackingSettings.InvertX ? -1f : 1f), -HeadTrackingSettings.MaxLeanX, HeadTrackingSettings.MaxLeanX),
                Mathf.Clamp(dy * HeadTrackingSettings.SensitivityY * (HeadTrackingSettings.InvertY ? -1f : 1f), -HeadTrackingSettings.MaxLeanY, HeadTrackingSettings.MaxLeanY),
                Mathf.Clamp(dz * HeadTrackingSettings.SensitivityZ * (HeadTrackingSettings.InvertZ ? -1f : 1f), -HeadTrackingSettings.MaxLeanZ, HeadTrackingSettings.MaxLeanZ));
            float leanMag = targetPos.magnitude;
            if (leanMag > HeadTrackingSettings.MaxLeanRadius)
            {
                targetPos *= HeadTrackingSettings.MaxLeanRadius / leanMag;
            }
            // Mode (0.1.6) zeroes the TARGET of the switched-off part, not the applied
            // offset: the smoother eases it out (or back in) like a recenter, and the
            // camera write/strip path is untouched — a switched-off part simply
            // decays to a zero offset that is still stripped exactly.
            int mode = HeadTrackingSettings.Mode;
            if (mode == HeadTrackingSettings.ModeRotationOnly)
            {
                targetPos = Vector3.zero;
            }
            else if (mode == HeadTrackingSettings.ModePositionOnly)
            {
                target = Vector3.zero;
            }

            // Exponential approach, time constant Smoothing * 0.5 s (0 = instant).
            float tau = HeadTrackingSettings.Smoothing * 0.5f;
            float k = tau <= 0.0001f ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime / tau);
            _smooth = Vector3.Lerp(_smooth, target, k);
            _smoothPos = Vector3.Lerp(_smoothPos, targetPos, k);
            if (!IsFinite(_smooth) || !IsFinite(_smoothPos))
            {
                // Belt and braces: readers reject NaN/Inf, but a NaN here would
                // poison the smoother — and the camera — forever.
                _smooth = Vector3.zero;
                _smoothPos = Vector3.zero;
            }

            _tracking = true;
            DisplayYaw = _smooth.y;
            DisplayPitch = _smooth.x;
            Status = "tracking";

            if (HeadTrackingSettings.LogPose && Time.unscaledTime >= _nextLog)
            {
                _nextLog = Time.unscaledTime + 1f;
                Plugin.Log.LogInfo("Pose [" + (_input != null ? _input.ModeName : "simulated")
                    + " frame " + raw.Frame + "] normalized deg (+yaw right, +pitch down, +roll left): yaw="
                    + raw.Yaw.ToString("0.0") + " pitch=" + raw.Pitch.ToString("0.0")
                    + " roll=" + raw.Roll.ToString("0.0")
                    + "  cm x=" + raw.X.ToString("0.0") + " y=" + raw.Y.ToString("0.0") + " z=" + raw.Z.ToString("0.0")
                    + "  -> offset pitch=" + _smooth.x.ToString("0.0") + " yaw=" + _smooth.y.ToString("0.0")
                    + " roll=" + _smooth.z.ToString("0.0")
                    + " pos x=" + _smoothPos.x.ToString("0.0") + " y=" + _smoothPos.y.ToString("0.0")
                    + " z=" + _smoothPos.z.ToString("0.0"));
            }
        }

        /// <summary>Input source follows the settings by polling (cheap int compares)
        /// rather than by SettingsChanged callbacks, so a live config edit can never
        /// interleave with a half-updated runtime. A source change drops the
        /// recenter neutral: FreeTrack, UDP and simulated poses do not share one.</summary>
        private void SyncSource()
        {
            int wanted = HeadTrackingSettings.SimulateInput ? SourceSimulated
                : (HeadTrackingSettings.InputMode == HeadTrackingSettings.InputOpenTrackUdp
                    ? HeadTrackingSettings.InputOpenTrackUdp : HeadTrackingSettings.InputFreeTrack);
            int port = HeadTrackingSettings.UdpPort;
            bool portChanged = wanted == HeadTrackingSettings.InputOpenTrackUdp && port != _sourcePort;
            if (wanted == _source && !portChanged)
            {
                return;
            }
            DisposeInput();
            _source = wanted;
            _sourcePort = port;
            _haveCenter = false;
            _inputError = null;
            _inputErrorLogged = false;
            _inputRetryAt = 0f;
        }

        private void MoveTowardZero()
        {
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 5f);
            _smooth = Vector3.Lerp(_smooth, Vector3.zero, k);
            _smoothPos = Vector3.Lerp(_smoothPos, Vector3.zero, k);
            DisplayYaw = _smooth.y;
            DisplayPitch = _smooth.x;
        }

        private bool TryReadPose(out HeadPose pose)
        {
            if (_source == SourceSimulated)
            {
                return TryReadSimulated(out pose);
            }
            pose = default(HeadPose);
            if (_input == null)
            {
                if (Time.unscaledTime < _inputRetryAt)
                {
                    return false;
                }
                try
                {
                    _input = _source == HeadTrackingSettings.InputOpenTrackUdp
                        ? (ITrackerInput)new OpenTrackUdp(_sourcePort)
                        : new FreeTrackPipe();
                    _inputError = null;
                }
                catch (Exception e)
                {
                    // Typically "port already in use". Without this catch the
                    // exception escaped Update every frame and the source was
                    // re-constructed (and failed) 60 times a second.
                    _inputRetryAt = Time.unscaledTime + InputRetryInterval;
                    _inputError = "UDP port " + _sourcePort + " unavailable";
                    if (!_inputErrorLogged)
                    {
                        _inputErrorLogged = true;
                        Plugin.Log.LogWarning("Cannot listen on UDP port " + _sourcePort + ": " + e.Message
                            + " (another program uses it?). Retrying every " + InputRetryInterval + " s.");
                    }
                    return false;
                }
            }
            return _input.TryGetPose(out pose);
        }

        // [Debug] SimulateInput: numpad-driven fake head in the HeadPose (OpenTrack)
        // convention: +yaw right, +pitch down, +roll left, +X LEFT, +Y up, +Z back.
        private float _simYaw;
        private float _simPitch;
        private float _simRoll;
        private float _simX;

        private bool TryReadSimulated(out HeadPose pose)
        {
            const float DegPerSec = 30f;
            const float CmPerSec = 10f;
            float dt = Time.unscaledDeltaTime;
            if (UnityEngine.Input.GetKey(KeyCode.Keypad4))
            {
                _simYaw -= DegPerSec * dt;
            }
            if (UnityEngine.Input.GetKey(KeyCode.Keypad6))
            {
                _simYaw += DegPerSec * dt;
            }
            if (UnityEngine.Input.GetKey(KeyCode.Keypad8))
            {
                _simPitch -= DegPerSec * dt;   // up
            }
            if (UnityEngine.Input.GetKey(KeyCode.Keypad2))
            {
                _simPitch += DegPerSec * dt;   // down
            }
            if (UnityEngine.Input.GetKey(KeyCode.Keypad7))
            {
                _simRoll += DegPerSec * dt;    // left
            }
            if (UnityEngine.Input.GetKey(KeyCode.Keypad9))
            {
                _simRoll -= DegPerSec * dt;    // right
            }
            if (UnityEngine.Input.GetKey(KeyCode.Keypad1))
            {
                _simX += CmPerSec * dt;        // head left  (OpenTrack +X = left)
            }
            if (UnityEngine.Input.GetKey(KeyCode.Keypad3))
            {
                _simX -= CmPerSec * dt;        // head right
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Keypad5))
            {
                _simYaw = 0f;
                _simPitch = 0f;
                _simRoll = 0f;
                _simX = 0f;
            }
            _simYaw = Mathf.Clamp(_simYaw, -180f, 180f);
            _simPitch = Mathf.Clamp(_simPitch, -90f, 90f);
            _simRoll = Mathf.Clamp(_simRoll, -90f, 90f);
            _simX = Mathf.Clamp(_simX, -50f, 50f);
            pose = new HeadPose
            {
                Yaw = _simYaw,
                Pitch = _simPitch,
                Roll = _simRoll,
                X = _simX,
                Valid = true
            };
            return true;
        }

        private void DisposeInput()
        {
            if (_input != null)
            {
                _input.Dispose();
                _input = null;
            }
        }

        // =============================================================== LateUpdate

        private void LateUpdate()
        {
            if (!HeadTrackingSettings.Enabled)
            {
                RemoveOffset();
                CameraActive = false;
                return;
            }

            // 0.1.10: UseIsolationRig can be switched live (Apocasetter). A mode
            // change converts the camera state so each path starts from a clean base.
            bool wantRig = HeadTrackingSettings.UseIsolationRig;
            if (wantRig != _rigActive)
            {
                SwitchRigMode(wantRig);
            }

            Transform cam = ResolveCamera();
            if (cam == null || !cam.gameObject.activeInHierarchy)
            {
                // 3rd person / menu / no player: hand the camera back NOW, so it
                // re-activates exactly as the game left it.
                RemoveOffset();
                CameraActive = false;
                return;
            }
            CameraActive = true;

            if (wantRig)
            {
                if (!EnsureOffsetRig(cam))
                {
                    // Rig unusable this frame (holder destroyed, reparent refused):
                    // nothing to write; retry next frame.
                    RemoveOffset();
                    return;
                }
            }

            if (!_tracking && _smooth.sqrMagnitude < 1e-4f && _smoothPos.sqrMagnitude < 1e-4f)
            {
                // Tracker gone and the offset has decayed: stop touching the rig.
                _smooth = Vector3.zero;
                _smoothPos = Vector3.zero;
                RemoveOffset();
                return;
            }

            float hp = HolderPitch();
            if (wantRig)
            {
                // 0.1.9: the offset lives on OUR dedicated rig child of the holder —
                // never on PlayerCamera. The hand-back is the rig at identity, which no
                // other mod can corrupt: whatever writes PlayerCamera (CameraMovementPro,
                // third-party head-bob mods) composes BELOW our offset instead of
                // interleaving with it. The CMP pre-multiply invariant of 0.1.1–0.1.8 is
                // no longer needed, because the rig transform is ours alone.
                _offsetGo.localRotation = ComputeOffset(hp);
                _offsetGo.localPosition = ComputePositionOffset(hp);
            }
            else
            {
                // Legacy direct-write mode (UseIsolationRig = false): pre-multiply on
                // the camera, strip with the inverse. Kept for mods that require
                // PlayerCamera to stay a direct child of the holder.
                Quaternion h = ComputeOffset(hp);
                Quaternion vanilla = _hasOffset
                    ? Quaternion.Inverse(_applied) * cam.localRotation
                    : cam.localRotation;
                cam.localRotation = h * vanilla;
                Vector3 pos = ComputePositionOffset(hp);
                Vector3 vanillaPos = _hasPos ? cam.localPosition - _appliedPos : cam.localPosition;
                cam.localPosition = vanillaPos + pos;
                _applied = h;
                _appliedPos = pos;
                _appliedCam = cam;
                _hasOffset = true;
                _hasPos = true;
            }
        }

        /// <summary>Convert between rig and direct-write mode with the camera state
        /// kept exact: rig → identity + camera back under the holder; direct → strip
        /// from the camera, then the rig takes over (camera re-parented under it).</summary>
        private void SwitchRigMode(bool wantRig)
        {
            if (!wantRig)
            {
                // Rig → legacy: zero the rig, then put the camera back under the
                // holder (world-preserving) so the holder-space local math is exact.
                RemoveOffset();
                if (_offsetGo != null && _cam != null && _cam.parent == _offsetGo)
                {
                    _cam.SetParent(_holder, true);
                }
                _offsetGo = null;
            }
            else
            {
                // Legacy → rig: strip from the camera, forget the camera-state
                // tracking; the rig is built (and the camera re-parented) on the
                // next EnsureOffsetRig.
                RemoveOffset();
            }
            _rigActive = wantRig;
        }

        /// <summary>
        /// Head offset in the holder's frame, built so yaw turns about the holder's
        /// PARENT up axis (body/vehicle up), not about the mouse-pitched camera up.
        /// Holder local = Ry(hy)·Rx(hp) (MouseLook writes (x, y, 0)); the wanted
        /// camera = Ry(hy+yaw)·Rx(hp+pitch)·Rz(roll); so the child offset is
        ///   holder⁻¹ · wanted = Rx(−hp) · Ry(yaw) · Rx(hp+pitch) · Rz(roll).
        /// For yaw = 0 this is exactly the old Rx(pitch)·Rz(roll). Without it, a head
        /// turn while looking down with the mouse rolled the horizon. Total pitch
        /// (mouse + head) is clamped to ±89° so the view never flips over the pole.
        /// Reads the holder rotation only; MouseLook has written it in Update.
        /// </summary>
        private Quaternion ComputeOffset(float hp)
        {
            float pitch = Mathf.Clamp(_smooth.x, -TotalPitchLimit - hp, TotalPitchLimit - hp);
            return Quaternion.Euler(-hp, 0f, 0f) * Quaternion.Euler(hp + pitch, _smooth.y, _smooth.z);
        }

        /// <summary>Signed mouse pitch of the holder (+ = down). Read only.</summary>
        private float HolderPitch()
        {
            return _holder != null ? Mathf.DeltaAngle(0f, _holder.localEulerAngles.x) : 0f;
        }

        /// <summary>
        /// Translation offset as written to PlayerCamera.localPosition (0.1.5).
        ///  - Units: _smoothPos is camera CENTIMETRES; localPosition is in the
        ///    holder's space, i.e. Unity units = metres (× holder scale). 0.1.2–0.1.4
        ///    wrote the centimetres as metres (×100): a 10 cm lean moved the camera
        ///    5 m at the 0.1.2 default.
        ///  - Frame: body frame = the holder with its mouse PITCH removed (yaw kept),
        ///    the same split as the rotation offset. localPosition lives in the
        ///    holder's mouse-pitched space, so in 0.1.4 a forward lean while looking
        ///    down drove the camera into the floor and "up" moved it backwards.
        ///    holder = Ry(hy)·Rx(hp) ⇒ holder-local = Rx(−hp) · bodyVector.
        ///  - Scale: divided by the holder's lossyScale so "cm" stays cm on a scaled
        ///    rig (no-op at scale 1).
        /// </summary>
        private Vector3 ComputePositionOffset(float hp)
        {
            if (_smoothPos.x == 0f && _smoothPos.y == 0f && _smoothPos.z == 0f)
            {
                return Vector3.zero;
            }
            Vector3 v = Quaternion.Euler(-hp, 0f, 0f) * (_smoothPos * 0.01f);
            if (_holder != null)
            {
                Vector3 s = _holder.lossyScale;
                if (Mathf.Abs(s.x) > 1e-4f) { v.x /= s.x; }
                if (Mathf.Abs(s.y) > 1e-4f) { v.y /= s.y; }
                if (Mathf.Abs(s.z) > 1e-4f) { v.z /= s.z; }
            }
            return v;
        }

        /// <summary>Hand the camera back: the rig at identity (rig mode) or the
        /// stored inverse (legacy mode). Writing an INACTIVE rig/camera is harmless:
        /// identity changes nothing, and the vehicle 3rdCamera is a different
        /// object, never touched.</summary>
        private void RemoveOffset()
        {
            if (_offsetGo != null)
            {
                _offsetGo.localRotation = Quaternion.identity;
                _offsetGo.localPosition = Vector3.zero;
            }
            if (_hasOffset && _appliedCam != null)
            {
                _appliedCam.localRotation = Quaternion.Inverse(_applied) * _appliedCam.localRotation;
            }
            if (_hasPos && _appliedCam != null)
            {
                _appliedCam.localPosition = _appliedCam.localPosition - _appliedPos;
            }
            _hasOffset = false;
            _hasPos = false;
            _applied = Quaternion.identity;
            _appliedPos = Vector3.zero;
            _appliedCam = null;
        }

        /// <summary>
        /// 0.1.9 isolation rig: PlayerCamera is re-parented (world-preserving) under
        /// a dedicated "HeadTrackingOffset" child of the holder, and the mod writes
        /// ONLY that rig. Verified safe against the game data: the game resolves
        /// PlayerCamera by name/tag (FindGameObject), its GetChild calls target the
        /// vehicle 3rdCamera hierarchy, and CameraMovementPro grabs its transform via
        /// GetComponent on the camera GO — none of them depend on PlayerCamera being
        /// a direct child of the holder.
        /// The rig is (re)built when missing (scene sweeps, FloatingOrigin rebuilds)
        /// or when the camera is no longer under it.
        /// </summary>
        private bool EnsureOffsetRig(Transform cam)
        {
            if (_offsetGo == null)
            {
                Transform existing = _holder != null ? _holder.Find(OffsetGoName) : null;
                if (existing != null)
                {
                    _offsetGo = existing;
                }
                else if (_holder != null)
                {
                    GameObject go = new GameObject(OffsetGoName);
                    go.transform.SetParent(_holder, false);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one;
                    _offsetGo = go.transform;
                }
            }
            if (_offsetGo == null || _offsetGo.parent != _holder)
            {
                return false;
            }
            if (cam.parent != _offsetGo)
            {
                // World-preserving reparent: the camera keeps its exact world pose,
                // so nothing in the scene notices except the hierarchy.
                cam.SetParent(_offsetGo, true);
            }
            return cam.parent == _offsetGo;
        }

        /// <summary>Cached PlayerCamera; re-found (throttled) when destroyed, when
        /// no longer under the holder/rig, or while inactive (a respawn may build a
        /// new player while the old camera lingers inactive). If another mod has
        /// re-parented PlayerCamera elsewhere, the tag fallback finds it by its
        /// MainCamera tag and EnsureOffsetRig puts it back under the rig. Never a
        /// per-frame GameObject.Find.</summary>
        private Transform ResolveCamera()
        {
            bool valid = _cam != null && _holder != null
                && (_cam.parent == _holder || (_offsetGo != null && _cam.parent == _offsetGo));
            if (valid && _cam.gameObject.activeInHierarchy)
            {
                return _cam;
            }
            float now = Time.unscaledTime;
            if (now >= _nextResolve)
            {
                _nextResolve = now + ResolveInterval;
                GameObject h = GameObject.Find(HolderName);
                if (h != null)
                {
                    // The camera starts as a direct child of the holder; after the
                    // rig exists it lives under HeadTrackingOffset. Try both.
                    Transform c = h.transform.Find(CameraName);
                    if (c == null)
                    {
                        Transform rig = h.transform.Find(OffsetGoName);
                        if (rig != null)
                        {
                            c = rig.Find(CameraName);
                        }
                    }
                    if (c == null)
                    {
                        // 0.1.10: another mod may have moved PlayerCamera out of the
                        // holder entirely. Find it by its MainCamera tag (it is the
                        // only GO with that name+tag) — the rig re-parents it.
                        GameObject tagged = GameObject.FindGameObjectWithTag("MainCamera");
                        if (tagged != null && tagged.name == CameraName)
                        {
                            c = tagged.transform;
                        }
                    }
                    if (c != null)
                    {
                        _holder = h.transform;
                        _cam = c;
                        valid = true;
                    }
                }
            }
            return valid ? _cam : null;
        }

        private static bool IsFinite(Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
                || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }
    }
}

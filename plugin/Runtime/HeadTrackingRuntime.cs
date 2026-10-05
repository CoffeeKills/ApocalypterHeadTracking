using ApocalypterHeadTracking.Input;
using ApocalypterHeadTracking.Persistence;
using ApocalypterHeadTracking.Settings;
using UnityEngine;

namespace ApocalypterHeadTracking.Runtime
{
    /// <summary>
    /// Reads the tracker pose in Update, applies a smoothed additive rotation to the
    /// first-person camera in LateUpdate.
    ///
    /// Camera facts (see README, load-bearing): the game's mouse look is PlayMaker
    /// MouseLook actions that overwrite PlayerCameraHolder's localEulerAngles every
    /// frame (roll forced to 0), so the offset goes on the CHILD "PlayerCamera" GO
    /// (tag MainCamera), which has no per-frame rotation writer. The vehicle camera
    /// switch deactivates PlayerCamera for 3rd person and re-activates it for 1st —
    /// checking activeInHierarchy is the whole "first-person only" rule.
    /// </summary>
    public class HeadTrackingRuntime : MonoBehaviour
    {
        public string Status = "off";          // for the HUD
        public float DisplayYaw;
        public float DisplayPitch;

        private ITrackerInput _input;
        private int _inputMode = -1;
        private HeadPose _center;
        private bool _haveCenter;
        private Vector3 _smooth;               // smoothed effective yaw/pitch/roll (deg)
        private Quaternion _lastApplied = Quaternion.identity;
        private bool _offsetActive;

        private GameObject _holder;
        private Transform _cam;
        private float _nextLog;

        private void OnEnable()
        {
            ModConfig.SettingsChanged += OnSettingsChanged;
            OnSettingsChanged();
        }

        private void OnDisable()
        {
            ModConfig.SettingsChanged -= OnSettingsChanged;
            DisposeInput();
        }

        private void OnSettingsChanged()
        {
            // The input provider lives and dies with the mode (and UDP port); the
            // camera refs are dropped because scene loads reparent/rebuild the
            // player hierarchy.
            _holder = null;
            _cam = null;
            if (_inputMode != HeadTrackingSettings.InputMode)
            {
                DisposeInput();
                _inputMode = HeadTrackingSettings.InputMode;
            }
            else if (_input is OpenTrackUdp udp && udp.Port != HeadTrackingSettings.UdpPort)
            {
                DisposeInput();
            }
        }

        private void Update()
        {
            // Toggle key switches the master switch (persisted via config).
            if (HeadTrackingSettings.ToggleKey != KeyCode.None
                && UnityEngine.Input.GetKeyDown(HeadTrackingSettings.ToggleKey))
            {
                ModConfig.SetEnabled(!HeadTrackingSettings.Enabled);
            }

            if (!HeadTrackingSettings.Enabled)
            {
                if (_offsetActive)
                {
                    // Hand the camera back: identity offset next LateUpdate.
                    _lastApplied = Quaternion.identity;
                    _offsetActive = false;
                }
                Status = "off";
                return;
            }

            HeadPose raw;
            if (!TryReadPose(out raw))
            {
                Status = "waiting";
                // Decay the offset back to zero so the camera returns gently.
                MoveTowardZero();
                return;
            }

            // Recenter: hold-this-pose neutral.
            if (HeadTrackingSettings.RecenterKey != KeyCode.None
                && UnityEngine.Input.GetKeyDown(HeadTrackingSettings.RecenterKey))
            {
                _center = raw;
                _haveCenter = true;
            }

            float yaw = raw.Yaw - (_haveCenter ? _center.Yaw : 0f);
            float pitch = raw.Pitch - (_haveCenter ? _center.Pitch : 0f);
            float roll = raw.Roll - (_haveCenter ? _center.Roll : 0f);

            float targetYaw = yaw * HeadTrackingSettings.SensitivityYaw * (HeadTrackingSettings.InvertYaw ? -1f : 1f);
            float targetPitch = pitch * HeadTrackingSettings.SensitivityPitch * (HeadTrackingSettings.InvertPitch ? -1f : 1f);
            float targetRoll = roll * HeadTrackingSettings.SensitivityRoll;
            float maxPitch = HeadTrackingSettings.MaxPitch;
            targetPitch = Mathf.Clamp(targetPitch, -maxPitch, maxPitch);

            Vector3 target = new Vector3(targetPitch, targetYaw, targetRoll);
            // Smoothing 0 = instant, 1 = slowest: exponential approach with a
            // time constant of Smoothing * 0.5 s.
            float tau = HeadTrackingSettings.Smoothing * 0.5f;
            float k = tau <= 0.0001f ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime / tau);
            _smooth = Vector3.Lerp(_smooth, target, k);

            DisplayYaw = _smooth.y;
            DisplayPitch = _smooth.x;
            Status = "tracking";

            if (HeadTrackingSettings.LogPose && Time.unscaledTime >= _nextLog)
            {
                _nextLog = Time.unscaledTime + 1f;
                Plugin.Log.LogInfo("Pose raw yaw=" + raw.Yaw.ToString("0.0")
                    + " pitch=" + raw.Pitch.ToString("0.0")
                    + " roll=" + raw.Roll.ToString("0.0")
                    + " x=" + raw.X.ToString("0.0") + " y=" + raw.Y.ToString("0.0") + " z=" + raw.Z.ToString("0.0"));
            }
        }

        private void MoveTowardZero()
        {
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 5f);
            _smooth = Vector3.Lerp(_smooth, Vector3.zero, k);
        }

        private bool TryReadPose(out HeadPose pose)
        {
            if (HeadTrackingSettings.SimulateInput)
            {
                return TryReadSimulated(out pose);
            }
            pose = default(HeadPose);
            if (_input == null)
            {
                _input = HeadTrackingSettings.InputMode == HeadTrackingSettings.InputOpenTrackUdp
                    ? (ITrackerInput)new OpenTrackUdp(HeadTrackingSettings.UdpPort)
                    : new FreeTrackPipe();
            }
            return _input.TryGetPose(out pose);
        }

        // [Debug] SimulateInput: numpad-driven fake head (no tracker hardware).
        private float _simYaw;
        private float _simPitch;

        private bool TryReadSimulated(out HeadPose pose)
        {
            const float DegPerSec = 30f;
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
                _simPitch += DegPerSec * dt;
            }
            if (UnityEngine.Input.GetKey(KeyCode.Keypad2))
            {
                _simPitch -= DegPerSec * dt;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Keypad5))
            {
                _simYaw = 0f;
                _simPitch = 0f;
            }
            pose = new HeadPose
            {
                Yaw = _simYaw,
                Pitch = _simPitch,
                Roll = 0f,
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

        private void LateUpdate()
        {
            if (!HeadTrackingSettings.Enabled)
            {
                return;
            }
            ResolveCamera();
            if (_cam == null || !_cam.gameObject.activeInHierarchy)
            {
                // 3rd person / menu / no camera: nothing to rotate, and next time
                // the camera reappears the base is recomputed from scratch.
                _lastApplied = Quaternion.identity;
                _offsetActive = false;
                return;
            }

            Quaternion offset = Quaternion.Euler(_smooth.x, _smooth.y, _smooth.z);
            if (!_offsetActive && offset == Quaternion.identity)
            {
                return;
            }
            // Additive: strip our previous offset to get the base the rest of the
            // game produced this frame (mouse look, head bob, ...), then re-apply.
            Quaternion baseLocal = _cam.localRotation * Quaternion.Inverse(_lastApplied);
            _cam.localRotation = baseLocal * offset;
            _lastApplied = offset;
            _offsetActive = true;
        }

        private void ResolveCamera()
        {
            if (_cam != null && _cam.gameObject.activeInHierarchy)
            {
                return;
            }
            if (_holder == null)
            {
                _holder = GameObject.Find("PlayerCameraHolder");
                if (_holder == null)
                {
                    return;
                }
            }
            Transform t = _holder.transform.Find("PlayerCamera");
            if (t == null)
            {
                // Not found under the holder (FloatingOrigin reparent, early scene
                // state): drop the ref and resolve again next frame.
                _holder = null;
                return;
            }
            _cam = t;
        }

        private void OnDestroy()
        {
            DisposeInput();
        }
    }
}

using UnityEngine;

namespace ApocalypterHeadTracking.Settings
{
    /// <summary>
    /// Runtime holder for the config values. ModConfig pushes into this on load and
    /// on every SettingChanged (live Apocasetter edits); the runtime reads only from
    /// here, never from config entries.
    /// </summary>
    public static class HeadTrackingSettings
    {
        public const int InputFreeTrack = 0;
        public const int InputOpenTrackUdp = 1;

        // 0.1.6: which offsets are applied. Cycled by ModeKey, persisted in [HeadTracking] Mode.
        public const int ModeFull = 0;
        public const int ModeRotationOnly = 1;
        public const int ModePositionOnly = 2;
        public const int ModeCount = 3;

        public static bool Enabled = true;
        public static int InputMode = InputFreeTrack;
        public static int UdpPort = Limits.PortDefault;
        // Pre-load values mirror the config defaults (Limits) — 0.1.0 had 1f here.
        public static float SensitivityYaw = Limits.SensDefault;
        public static float SensitivityPitch = Limits.SensDefault;
        public static float SensitivityRoll = 0f;   // roll off by default
        public static float SensitivityX = Limits.TransDefault;   // pan (sideways)
        public static float SensitivityY = Limits.TransDefault;   // height
        public static float SensitivityZ = Limits.TransDefault;   // forward/back
        public static bool InvertX = false;
        public static bool InvertY = false;
        public static bool InvertZ = false;
        public static bool InvertYaw = false;
        public static bool InvertPitch = false;
        public static float Smoothing = Limits.SmoothDefault;
        public static float MaxPitch = Limits.MaxPitchDefault;
        public static KeyCode RecenterKey = KeyCode.F8;
        public static KeyCode ToggleKey = KeyCode.None;
        public static int Mode = ModeFull;
        public static KeyCode ModeKey = KeyCode.None;
        public static bool UseIsolationRig = true;
        public static float MaxLeanX = Limits.MaxLeanXDefault;
        public static float MaxLeanY = Limits.MaxLeanYDefault;
        public static float MaxLeanZ = Limits.MaxLeanZDefault;
        public static float MaxLeanRadius = Limits.MaxLeanRadiusDefault;
        public static bool ThirdPerson = false;   // accessibility look-around
        public static KeyCode ThirdPersonKey = KeyCode.None;
        public static bool ShowHud = true;
        public static bool LogPose = false;
        public static bool SimulateInput = false;
    }
}

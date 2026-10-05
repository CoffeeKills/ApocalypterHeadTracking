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

        public static bool Enabled = true;
        public static int InputMode = InputFreeTrack;
        public static int UdpPort = Limits.PortDefault;
        // Pre-load values mirror the config defaults (Limits) — 0.1.0 had 1f here.
        public static float SensitivityYaw = Limits.SensDefault;
        public static float SensitivityPitch = Limits.SensDefault;
        public static float SensitivityRoll = 0f;   // roll off by default
        public static bool InvertYaw = false;
        public static bool InvertPitch = false;
        public static float Smoothing = Limits.SmoothDefault;
        public static float MaxPitch = Limits.MaxPitchDefault;
        public static KeyCode RecenterKey = KeyCode.F8;
        public static KeyCode ToggleKey = KeyCode.None;
        public static bool ShowHud = true;
        public static bool LogPose = false;
        public static bool SimulateInput = false;
    }
}

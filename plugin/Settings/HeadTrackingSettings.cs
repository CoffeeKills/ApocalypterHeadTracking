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
        public static int UdpPort = 4242;
        public static float SensitivityYaw = 1f;
        public static float SensitivityPitch = 1f;
        public static float SensitivityRoll = 0f;   // roll off by default
        public static bool InvertYaw = false;
        public static bool InvertPitch = false;
        public static float Smoothing = 0.5f;
        public static float MaxPitch = 80f;
        public static KeyCode RecenterKey = KeyCode.F8;
        public static KeyCode ToggleKey = KeyCode.None;
        public static bool ShowHud = true;
        public static bool LogPose = false;
        public static bool SimulateInput = false;
    }
}

using System;
using ApocalypterHeadTracking.Settings;
using BepInEx.Configuration;
using UnityEngine;

namespace ApocalypterHeadTracking.Persistence
{
    /// <summary>
    /// The BepInEx config bridge: every key lives here, bound with ranges and
    /// descriptions so the Apocasetter Mods menu renders live editors for free
    /// (SettingChanged → runtime push). No migration machinery: this mod's config
    /// file is its own (fresh GUID), so there are no legacy files to migrate from.
    /// </summary>
    public static class ModConfig
    {
        public static event Action SettingsChanged;

        private static ConfigFile _config;
        private static bool _syncing;

        private static ConfigEntry<bool> _enabled;
        private static ConfigEntry<int> _inputMode;
        private static ConfigEntry<int> _udpPort;
        private static ConfigEntry<float> _sensYaw, _sensPitch, _sensRoll;
        private static ConfigEntry<bool> _invertYaw, _invertPitch;
        private static ConfigEntry<float> _smoothing;
        private static ConfigEntry<float> _maxPitch;
        private static ConfigEntry<KeyboardShortcut> _recenterKey, _toggleKey;
        private static ConfigEntry<bool> _showHud;
        private static ConfigEntry<bool> _logPose;
        private static ConfigEntry<bool> _simulateInput;
        // Read by Apocasetter via Chainloader (not wired to SettingsChanged — we never read it).
        private static ConfigEntry<bool> _apocasetter;

        /// <summary>Write the master switch (the runtime toggle key mirrors this) and save.</summary>
        public static void SetEnabled(bool value)
        {
            if (_enabled == null)
            {
                return;
            }
            _enabled.Value = value;
            // SaveOnConfigSet (BepInEx default: on) already wrote the file; 0.1.0
            // saved a second time on every toggle-key press.
            if (!_config.SaveOnConfigSet)
            {
                _config.Save();
            }
        }

        public static void Load(ConfigFile config)
        {
            _config = config;

            // One file write at the end instead of one per Bind.
            bool autoSave = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;

            _enabled = config.Bind("HeadTracking", "Enabled", true,
                "Master switch for headtracking. Off = the camera behaves exactly like vanilla.");
            // 0.1.1: accepted-value list (0, 1). AcceptableValueList falls back to its
            // FIRST value (0 = FreeTrack) for anything else — the same meaning an
            // out-of-range value had in 0.1.0 (runtime treated "not 1" as FreeTrack).
            _inputMode = config.Bind("HeadTracking", "Input", 0,
                new ConfigDescription("Tracker input source: 0 = FreeTrack 2.0 shared memory (OpenTrack's 'FreeTrack 2.0' output), 1 = OpenTrack UDP.",
                    new AcceptableValueList<int>(0, 1)));
            _udpPort = config.Bind("HeadTracking", "UdpPort", Limits.PortDefault,
                new ConfigDescription("UDP port to listen on when Input = OpenTrack UDP (OpenTrack: Output → UDP over network).",
                    new AcceptableValueRange<int>(Limits.PortMin, Limits.PortMax)));
            _sensYaw = BindRange("HeadTracking", "SensitivityYaw", Limits.SensDefault, Limits.SensMin, Limits.SensMax,
                "How many degrees the camera turns per degree of head yaw.");
            _sensPitch = BindRange("HeadTracking", "SensitivityPitch", Limits.SensDefault, Limits.SensMin, Limits.SensMax,
                "How many degrees the camera turns per degree of head pitch.");
            _sensRoll = BindRange("HeadTracking", "SensitivityRoll", 0f, Limits.SensMin, Limits.SensMax,
                "How many degrees the camera rolls per degree of head roll (0 = off).");
            _invertYaw = config.Bind("HeadTracking", "InvertYaw", false,
                "Flip the yaw direction.");
            _invertPitch = config.Bind("HeadTracking", "InvertPitch", false,
                "Flip the pitch direction.");
            _smoothing = BindRange("HeadTracking", "Smoothing", Limits.SmoothDefault, Limits.SmoothMin, Limits.SmoothMax,
                "How quickly the camera follows the head (0 = instant, 0.95 = slowest).");
            _maxPitch = BindRange("HeadTracking", "MaxPitch", Limits.MaxPitchDefault, Limits.MaxPitchMin, Limits.MaxPitchMax,
                "Clamp for the head pitch offset in degrees. Mouse pitch + head pitch is additionally kept within ±89° so the view never flips.");
            _recenterKey = config.Bind("HeadTracking", "RecenterKey",
                new KeyboardShortcut(KeyCode.F8),
                "Hold-this-pose neutral: press while looking straight ahead to zero the offset (only the main key is used; modifiers are ignored so it works while sprinting).");
            _toggleKey = config.Bind("HeadTracking", "ToggleKey",
                new KeyboardShortcut(KeyCode.None),
                "Key that switches headtracking on/off in-game (None = no key).");
            _showHud = config.Bind("HeadTracking", "ShowHud", true,
                "Show the small status line (input source, tracking state, live yaw/pitch).");
            _logPose = config.Bind("Debug", "LogPose", false,
                "Log the raw tracker values once per second (protocol verification).");
            _simulateInput = config.Bind("Debug", "SimulateInput", false,
                "Feed a simulated head pose from the numpad instead of a tracker "
                + "(testing without hardware): Numpad 4/6 yaw, 8/2 pitch, Numpad5 zero.");
            _apocasetter = config.Bind("General", "Apocasetter", true,
                "Show this mod in the Apocasetter Mods menu (requires Apocasetter installed).");

            WireAll();

            PushAllToRuntime();

            config.SaveOnConfigSet = autoSave;
            config.Save();
        }

        // ---------------------------------------------------------------- binding

        private static ConfigEntry<float> BindRange(string section, string key, float def, float min, float max, string description)
        {
            return _config.Bind(section, key, def,
                new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));
        }

        private static void WireAll()
        {
            Wire(_enabled);
            Wire(_inputMode);
            Wire(_udpPort);
            Wire(_sensYaw);
            Wire(_sensPitch);
            Wire(_sensRoll);
            Wire(_invertYaw);
            Wire(_invertPitch);
            Wire(_smoothing);
            Wire(_maxPitch);
            Wire(_recenterKey);
            Wire(_toggleKey);
            Wire(_showHud);
            Wire(_logPose);
            Wire(_simulateInput);
        }

        private static void Wire<T>(ConfigEntry<T> entry)
        {
            entry.SettingChanged += (sender, args) => OnEntryChanged(entry);
        }

        // ---------------------------------------------------------------- save / push

        public static void Save()
        {
            if (_config == null)
            {
                return;
            }
            _config.Save();
        }

        private static void OnEntryChanged(ConfigEntryBase changed)
        {
            if (_syncing)
            {
                return;
            }
            _syncing = true;
            try
            {
                PushAllToRuntime();
            }
            finally
            {
                _syncing = false;
            }
            SettingsChanged?.Invoke();
        }

        private static void PushAllToRuntime()
        {
            HeadTrackingSettings.Enabled = _enabled.Value;
            HeadTrackingSettings.InputMode = _inputMode.Value;
            HeadTrackingSettings.UdpPort = _udpPort.Value;
            HeadTrackingSettings.SensitivityYaw = _sensYaw.Value;
            HeadTrackingSettings.SensitivityPitch = _sensPitch.Value;
            HeadTrackingSettings.SensitivityRoll = _sensRoll.Value;
            HeadTrackingSettings.InvertYaw = _invertYaw.Value;
            HeadTrackingSettings.InvertPitch = _invertPitch.Value;
            HeadTrackingSettings.Smoothing = _smoothing.Value;
            HeadTrackingSettings.MaxPitch = _maxPitch.Value;
            HeadTrackingSettings.RecenterKey = _recenterKey.Value.MainKey;
            HeadTrackingSettings.ToggleKey = _toggleKey.Value.MainKey;
            HeadTrackingSettings.ShowHud = _showHud.Value;
            HeadTrackingSettings.LogPose = _logPose.Value;
            HeadTrackingSettings.SimulateInput = _simulateInput.Value;
        }
    }
}

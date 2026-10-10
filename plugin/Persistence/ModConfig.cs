using System;
using System.IO;
using ApocalypterHeadTracking.Settings;
using BepInEx.Configuration;
using UnityEngine;

namespace ApocalypterHeadTracking.Persistence
{
    /// <summary>
    /// The BepInEx config bridge: every key lives here, bound with ranges and
    /// descriptions so the Apocasetter Mods menu renders live editors for free
    /// (SettingChanged → runtime push). One migration exists (0.1.5: translation
    /// units + axis signs, keyed on [General] ConfigVersion); no key was ever
    /// renamed or removed. The runtime polls HeadTrackingSettings each frame, so
    /// SettingsChanged is kept only as a public notification.
    /// </summary>
    public static class ModConfig
    {
        public static event Action SettingsChanged;

        private static ConfigFile _config;
        private static bool _syncing;

        private static ConfigEntry<bool> _enabled;
        private static ConfigEntry<int> _inputMode;
        private static ConfigEntry<int> _udpPort;
        private static ConfigEntry<float> _sensYaw, _sensPitch, _sensRoll, _sensX, _sensY, _sensZ;
        private static ConfigEntry<bool> _invertYaw, _invertPitch, _invertX, _invertY, _invertZ;
        private static ConfigEntry<float> _smoothing;
        private static ConfigEntry<float> _maxPitch;
        private static ConfigEntry<KeyboardShortcut> _recenterKey, _toggleKey, _modeKey;
        private static ConfigEntry<int> _mode;
        private static ConfigEntry<bool> _showHud;
        private static ConfigEntry<bool> _logPose;
        private static ConfigEntry<bool> _simulateInput;
        private static ConfigEntry<bool> _useIsolationRig;
        // Read by Apocasetter via Chainloader (not wired to SettingsChanged — we never read it).
        private static ConfigEntry<bool> _apocasetter;
        // 0.1.5: file-format marker for one-time migrations (not a runtime setting).
        private static ConfigEntry<int> _configVersion;

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

        /// <summary>Advance [HeadTracking] Mode (full → rotation only → position only
        /// → full) — the ModeKey handler. Same write path as SetEnabled, so it is
        /// persisted and Apocasetter shows the new value.</summary>
        public static void CycleMode()
        {
            if (_mode == null)
            {
                return;
            }
            _mode.Value = (_mode.Value + 1) % HeadTrackingSettings.ModeCount;
            if (!_config.SaveOnConfigSet)
            {
                _config.Save();
            }
        }

        public static void Load(ConfigFile config)
        {
            _config = config;

            // Must run before any Bind: decides from the file text alone whether the
            // stored translation values are in the pre-0.1.5 format.
            bool legacyTranslation = IsLegacyTranslationFile(config.ConfigFilePath);

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
            _sensX = BindRange("HeadTracking", "SensitivityX", Limits.TransDefault, Limits.SensMin, Limits.TransMax,
                "Sideways lean: camera cm per head cm (1 = 1:1, 0 = off). The camera never moves more than 50 cm per axis.");
            _sensY = BindRange("HeadTracking", "SensitivityY", Limits.TransDefault, Limits.SensMin, Limits.TransMax,
                "Up/down: camera cm per head cm (1 = 1:1, 0 = off). The camera never moves more than 50 cm per axis.");
            _sensZ = BindRange("HeadTracking", "SensitivityZ", Limits.TransDefault, Limits.SensMin, Limits.TransMax,
                "Forward/back lean: camera cm per head cm (1 = 1:1, 0 = off). The camera never moves more than 50 cm per axis.");
            _invertX = config.Bind("HeadTracking", "InvertX", false,
                "Flip the sideways pan direction (which way the camera moves for sideways head movement).");
            _invertY = config.Bind("HeadTracking", "InvertY", false,
                "Flip the height direction.");
            _invertZ = config.Bind("HeadTracking", "InvertZ", false,
                "Flip the forward/back direction.");
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
            // 0.1.6: tracking mode + cycle key. Defaults keep 0.1.5 behaviour (full,
            // no key bound). AcceptableValueList maps an invalid value to its first
            // entry, 0 = full.
            _mode = config.Bind("HeadTracking", "Mode", HeadTrackingSettings.ModeFull,
                new ConfigDescription("What the head moves: 0 = rotation + lean (full), 1 = rotation only, 2 = lean only. "
                    + "ModeKey cycles through these in-game.",
                    new AcceptableValueList<int>(HeadTrackingSettings.ModeFull, HeadTrackingSettings.ModeRotationOnly,
                        HeadTrackingSettings.ModePositionOnly)));
            _modeKey = config.Bind("HeadTracking", "ModeKey",
                new KeyboardShortcut(KeyCode.None),
                "Key that cycles Mode in-game: full → rotation only → lean only (None = no key; "
                + "PageUp is what comparable headtracking mods use).");
            _showHud = config.Bind("HeadTracking", "ShowHud", true,
                "Show the small status line (input source, tracking state, live yaw/pitch).");
            _logPose = config.Bind("Debug", "LogPose", false,
                "Log the raw tracker values once per second (protocol verification).");
            _simulateInput = config.Bind("Debug", "SimulateInput", false,
                "Feed a simulated head pose from the numpad instead of a tracker "
                + "(testing without hardware): Numpad 4/6 yaw, 8/2 pitch, 7/9 roll, 1/3 sideways lean, Numpad5 zero.");
            _useIsolationRig = config.Bind("HeadTracking", "UseIsolationRig", true,
                "Keep the head offset on the mod's own rig object (recommended: immune to other camera mods). "
                + "Turn off only if another mod requires PlayerCamera to stay a direct child of the holder.");
            _apocasetter = config.Bind("General", "Apocasetter", true,
                "Show this mod in the Apocasetter Mods menu (requires Apocasetter installed).");
            _configVersion = config.Bind("General", "ConfigVersion", Limits.ConfigVersion,
                new ConfigDescription("Internal: config format version, written by the mod for one-time upgrades. Do not edit.",
                    new AcceptableValueRange<int>(0, Limits.ConfigVersion)));

            if (legacyTranslation)
            {
                MigrateTranslationToV2();
            }
            _configVersion.Value = Limits.ConfigVersion;

            WireAll();

            PushAllToRuntime();

            config.SaveOnConfigSet = autoSave;
            config.Save();
        }

        // ---------------------------------------------------------------- migration

        /// <summary>A pre-0.1.5 file is one that already holds translation keys
        /// (written by 0.1.2–0.1.4) but no ConfigVersion. Fresh files and 0.1.0/0.1.1
        /// files (no translation keys yet) get the new defaults and need nothing.
        /// Runs once at load; never in a per-frame path.</summary>
        private static bool IsLegacyTranslationFile(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    return false;
                }
                bool hasTranslation = false;
                bool hasVersion = false;
                foreach (string line in File.ReadAllLines(path))
                {
                    string t = line.TrimStart();
                    if (IsKeyLine(t, "SensitivityX"))
                    {
                        hasTranslation = true;
                    }
                    else if (IsKeyLine(t, "ConfigVersion"))
                    {
                        hasVersion = true;
                    }
                }
                return hasTranslation && !hasVersion;
            }
            catch (Exception)
            {
                return false;   // unreadable: leave values alone rather than guess
            }
        }

        private static bool IsKeyLine(string t, string key)
        {
            if (!t.StartsWith(key, StringComparison.Ordinal))
            {
                return false;
            }
            string rest = t.Substring(key.Length).TrimStart();
            return rest.StartsWith("=", StringComparison.Ordinal);
        }

        /// <summary>
        /// 0.1.5 changed two things about translation; this keeps every existing
        /// setup looking EXACTLY as before on screen:
        ///  1. Units: old values were "metres per head cm" (the 0.1.2 unit bug), new
        ///     ones are "cm per cm" → ×100 (0.01 → 1, 0.005 → 0.5), clamped to the
        ///     new 0–3 range.
        ///  2. Axis signs: 0.1.2–0.1.4 had X and Z reversed against OpenTrack's
        ///     convention, so working setups compensated (in OpenTrack, or with the
        ///     mod's InvertX/InvertZ). The base mapping is now correct, so InvertX and
        ///     InvertZ are flipped once to cancel out the correction for them.
        /// </summary>
        private static void MigrateTranslationToV2()
        {
            float x = _sensX.Value, y = _sensY.Value, z = _sensZ.Value;
            _sensX.Value = Mathf.Clamp(x * 100f, Limits.SensMin, Limits.TransMax);
            _sensY.Value = Mathf.Clamp(y * 100f, Limits.SensMin, Limits.TransMax);
            _sensZ.Value = Mathf.Clamp(z * 100f, Limits.SensMin, Limits.TransMax);
            _invertX.Value = !_invertX.Value;
            _invertZ.Value = !_invertZ.Value;
            Plugin.Log?.LogInfo("Config upgraded to format " + Limits.ConfigVersion
                + ": translation sensitivity X/Y/Z " + x + "/" + y + "/" + z + " -> "
                + _sensX.Value + "/" + _sensY.Value + "/" + _sensZ.Value
                + " (new unit: camera cm per head cm), InvertX/InvertZ flipped to keep the same on-screen direction"
                + " (axis signs corrected in 0.1.5).");
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
            Wire(_sensX);
            Wire(_sensY);
            Wire(_sensZ);
            Wire(_invertX);
            Wire(_invertY);
            Wire(_invertZ);
            Wire(_invertYaw);
            Wire(_invertPitch);
            Wire(_smoothing);
            Wire(_maxPitch);
            Wire(_recenterKey);
            Wire(_toggleKey);
            Wire(_mode);
            Wire(_modeKey);
            Wire(_showHud);
            Wire(_logPose);
            Wire(_simulateInput);
            Wire(_useIsolationRig);
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
            HeadTrackingSettings.SensitivityX = _sensX.Value;
            HeadTrackingSettings.SensitivityY = _sensY.Value;
            HeadTrackingSettings.SensitivityZ = _sensZ.Value;
            HeadTrackingSettings.InvertX = _invertX.Value;
            HeadTrackingSettings.InvertY = _invertY.Value;
            HeadTrackingSettings.InvertZ = _invertZ.Value;
            HeadTrackingSettings.InvertYaw = _invertYaw.Value;
            HeadTrackingSettings.InvertPitch = _invertPitch.Value;
            HeadTrackingSettings.Smoothing = _smoothing.Value;
            HeadTrackingSettings.MaxPitch = _maxPitch.Value;
            HeadTrackingSettings.RecenterKey = _recenterKey.Value.MainKey;
            HeadTrackingSettings.ToggleKey = _toggleKey.Value.MainKey;
            HeadTrackingSettings.Mode = _mode.Value;
            HeadTrackingSettings.ModeKey = _modeKey.Value.MainKey;
            HeadTrackingSettings.ShowHud = _showHud.Value;
            HeadTrackingSettings.LogPose = _logPose.Value;
            HeadTrackingSettings.SimulateInput = _simulateInput.Value;
            HeadTrackingSettings.UseIsolationRig = _useIsolationRig.Value;
        }
    }
}

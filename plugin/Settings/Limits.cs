namespace ApocalypterHeadTracking.Settings
{
    /// <summary>Clamps for the config entries — the same numbers Apocasetter shows
    /// as slider ranges.</summary>
    public static class Limits
    {
        public const float SensMin = 0f;
        public const float SensMax = 3f;
        public const float SensDefault = 0.5f;   // real head movement is small: 1:1 is too much

        /// <summary>Translation sensitivity (0.1.5): a plain ratio, camera
        /// centimetres per tracked head centimetre — 1 = 1:1, the convention every
        /// surveyed headtracking mod uses (docs/headtracking-expectations.md).
        /// 0.1.2–0.1.4 applied the tracker's centimetres as Unity METRES, which is
        /// why their "good" values sat at 0.005–0.05 (= 0.5–5 here).</summary>
        public const float TransDefault = 1f;      // = the 0.1.3/0.1.4 default 0.01 on screen

        /// <summary>Same 0–3 span as the rotation sliders.</summary>
        public const float TransMax = 3f;

        /// <summary>
        /// Default camera-lean bounds (0.1.11): how far the camera may leave the
        /// character, in centimetres — a HUMAN can't move its head further than
        /// this, so neither can the camera by default, whatever the sliders or a
        /// spiking tracker say. X is generous on purpose: leaning out of a car
        /// window. All four are config keys (MaxLeanX/Y/Z/Radius) for setups that
        /// want something else.</summary>
        public const float MaxLeanXDefault = 45f;
        public const float MaxLeanYDefault = 30f;
        public const float MaxLeanZDefault = 30f;
        /// <summary>Total distance from neutral (diagonal leans combine axes).</summary>
        public const float MaxLeanRadiusDefault = 50f;
        public const float MaxLeanMin = 0f;
        public const float MaxLeanMax = 150f;

        /// <summary>Config file format version (0.1.5). 2 = translation in cm/cm
        /// with OpenTrack-correct axis signs.</summary>
        public const int ConfigVersion = 2;

        public const float SmoothMin = 0f;
        public const float SmoothMax = 0.95f;
        public const float SmoothDefault = 0.5f;

        public const float MaxPitchMin = 0f;
        public const float MaxPitchMax = 180f;
        public const float MaxPitchDefault = 80f;

        public const int PortMin = 1;
        public const int PortMax = 65535;
        public const int PortDefault = 4242;
    }
}

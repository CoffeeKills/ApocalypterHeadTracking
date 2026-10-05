namespace ApocalypterHeadTracking.Settings
{
    /// <summary>Clamps for the config entries — the same numbers Apocasetter shows
    /// as slider ranges.</summary>
    public static class Limits
    {
        public const float SensMin = 0f;
        public const float SensMax = 3f;
        public const float SensDefault = 0.5f;   // real head movement is small: 1:1 is too much

        /// <summary>Translation sensitivity: camera centimetres per tracked
        /// centimetre. Head translation is small (±5–10 cm) and webcam-tracked
        /// translation is noisy — 0.5× is a calm default.</summary>
        public const float TransDefault = 0.5f;

        /// <summary>Largest translation offset the mod will apply (cm).</summary>
        public const float MaxTransCm = 50f;

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

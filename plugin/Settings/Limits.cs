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
        /// centimetre. Webcam-tracked translation is noisy and can spike, and a
        /// few cm of camera offset reads as a lot in first person — keep the
        /// default tiny (0.5 was far too much: first tester feedback).</summary>
        public const float TransDefault = 0.01f;

        /// <summary>Translation range spans the whole REALISTIC band (0–0.05) so
        /// the Apocasetter slider has usable precision where translation actually
        /// lives — a 0–3 range forced testers to bottom out at 0.005.</summary>
        public const float TransMax = 0.05f;

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

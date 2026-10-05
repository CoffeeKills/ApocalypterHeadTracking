namespace ApocalypterHeadTracking.Settings
{
    /// <summary>Clamps for the config entries — the same numbers Apocasetter shows
    /// as slider ranges.</summary>
    public static class Limits
    {
        public const float SensMin = 0f;
        public const float SensMax = 3f;
        public const float SensDefault = 1f;

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

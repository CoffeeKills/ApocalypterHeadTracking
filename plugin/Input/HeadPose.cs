namespace ApocalypterHeadTracking.Input
{
    /// <summary>One tracker sample: rotations in degrees, translations as the
    /// tracker sends them (unused by the mod — rotation only).</summary>
    public struct HeadPose
    {
        public float Yaw;
        public float Pitch;
        public float Roll;
        public float X;
        public float Y;
        public float Z;
        public bool Valid;
    }

    /// <summary>A headtracking input source (FreeTrack shared memory or OpenTrack
    /// UDP). TryGetPose must be non-blocking and allocation-free — it is called
    /// every frame from the runtime's Update.</summary>
    public interface ITrackerInput
    {
        string ModeName { get; }
        bool TryGetPose(out HeadPose pose);
        void Dispose();
    }
}

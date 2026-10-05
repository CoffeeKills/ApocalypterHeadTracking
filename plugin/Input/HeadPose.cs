namespace ApocalypterHeadTracking.Input
{
    /// <summary>
    /// One tracker sample, NORMALIZED by the reader to OpenTrack's internal pose
    /// convention so the runtime never has to know which protocol it came from:
    ///   Yaw   degrees, positive = head turned right
    ///   Pitch degrees, positive = head tilted down
    ///   Roll  degrees, positive = head tilted left
    ///   X/Y/Z centimetres (unused — the mod is rotation only)
    /// This is exactly what OpenTrack's UDP output sends; the FreeTrack reader
    /// converts into it (see FreeTrackPipe). It also maps 1:1 onto Unity's
    /// Quaternion.Euler(pitch, yaw, roll) on a camera: +x looks down, +y turns
    /// right, +z rolls the view left — so the runtime applies it without sign flips.
    /// </summary>
    public struct HeadPose
    {
        public float Yaw;
        public float Pitch;
        public float Roll;
        public float X;
        public float Y;
        public float Z;
        /// <summary>FreeTrack DataID or UDP packet counter (LogPose only).</summary>
        public uint Frame;
        public bool Valid;
    }

    /// <summary>A headtracking input source (FreeTrack shared memory or OpenTrack
    /// UDP). TryGetPose must be non-blocking and allocation-free — it is called
    /// every frame from the runtime's Update — and returns true only for a pose
    /// that is live (the tracker is still writing), finite and plausible.</summary>
    public interface ITrackerInput
    {
        string ModeName { get; }
        bool TryGetPose(out HeadPose pose);
        void Dispose();
    }

    internal static class PoseChecks
    {
        /// <summary>Largest angle (degrees) a sane tracker reports. OpenTrack's
        /// mapping range is ±180; anything beyond one full turn is garbage (another
        /// app on the UDP port, a non-compliant FreeTrack writer).</summary>
        public const float MaxAbsAngleDeg = 360f;

        public static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }

        public static bool IsPlausibleAngle(float deg)
        {
            return IsFinite(deg) && deg <= MaxAbsAngleDeg && deg >= -MaxAbsAngleDeg;
        }
    }
}

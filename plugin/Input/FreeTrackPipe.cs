using System;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace ApocalypterHeadTracking.Input
{
    /// <summary>
    /// FreeTrack 2.0 protocol reader: OpenTrack's "FreeTrack 2.0" output writes the
    /// pose into the named memory-mapped file "FT_SharedMem".
    ///
    /// Layout and encoding — verified against OpenTrack's own source, not protocol
    /// memory: freetrackclient/fttypes.h (struct FTData) for the layout and
    /// proto-ft/ftnoir_protocol_ft.cpp (freetrack::pose) for the encoding:
    ///   +0  uint32 DataID     InterlockedAdd(+1) on every pose() call (~250 Hz while
    ///                         tracking); zeroed when the game ID changes
    ///   +4  int32  CamWidth   +8 int32 CamHeight (unused)
    ///   +12 float  Yaw        RADIANS, positive = LEFT  (written as -opentrackYaw)
    ///   +16 float  Pitch      RADIANS, positive = UP    (written as -opentrackPitch)
    ///   +20 float  Roll       RADIANS, positive = LEFT  (written as +opentrackRoll)
    ///   +24/28/32 float X/Y/Z millimetres (written as opentrack cm * 10)
    /// Every layout fact is one constant below; every encoding fact is one line in
    /// Decode. A wrong guess is a one-line fix, and [Debug] LogPose shows the
    /// decoded values next to the DataID for a live check.
    ///
    /// Snapshot: the 36 bytes are copied in ONE memcpy into a reusable buffer and
    /// everything (pose + liveness) is decoded from that snapshot — no per-frame
    /// allocation, and the values used for liveness are the values returned.
    /// </summary>
    public class FreeTrackPipe : ITrackerInput
    {
        public const string SharedMemoryName = "FT_SharedMem";

        internal const int OffDataId = 0;
        internal const int OffYaw = 12;
        internal const int OffPitch = 16;
        internal const int OffRoll = 20;
        internal const int OffX = 24;
        internal const int OffY = 28;
        internal const int OffZ = 32;
        internal const int OffPose = OffYaw;
        internal const int PoseBytes = 24;
        internal const int SnapshotSize = 36;

        /// <summary>No DataID tick (or, for writers that never tick DataID, no pose
        /// change) for this long = the tracker stopped; the frozen values in the
        /// mapping are stale.</summary>
        internal const double StaleSeconds = 0.5;

        private const double RetrySeconds = 1.0;
        private const float RadToDeg = 57.29578f;
        /// <summary>|radians| beyond one turn (+slack) means the writer is sending
        /// degrees — not FreeTrack 2.0 compliant (e.g. an old test tool).</summary>
        private const float MaxAbsRadians = 6.3f;

        private MemoryMappedFile _mmf;
        private MemoryMappedViewAccessor _view;
        private double _retryAt;

        private readonly byte[] _snap = new byte[SnapshotSize];
        private readonly byte[] _lastPose = new byte[PoseBytes];
        private bool _haveLast;
        private uint _lastId;
        private bool _idTicks;          // DataID has been seen advancing → use it for liveness
        private double _lastChangeTime;
        private bool _seenChange;       // a stale mapping must never count as live on first read
        private bool _warnedUnits;

        public string ModeName { get { return "FreeTrack"; } }

        public bool TryGetPose(out HeadPose pose)
        {
            pose = default(HeadPose);
            double now = Now();
            if (_view == null)
            {
                if (now < _retryAt)
                {
                    return false;
                }
                if (!TryOpen())
                {
                    _retryAt = now + RetrySeconds;
                    return false;
                }
            }
            try
            {
                // View is mapped at offset 0 (page-aligned), so the handle is the
                // start of FTData on both .NET (PointerOffset 0) and Mono.
                IntPtr p = _view.SafeMemoryMappedViewHandle.DangerousGetHandle();
                Marshal.Copy(p, _snap, 0, SnapshotSize);
            }
            catch (Exception)
            {
                Close();
                _retryAt = now + RetrySeconds;
                return false;
            }
            bool live = Observe(_snap, now);
            if (!Decode(_snap, out pose))
            {
                return false;
            }
            return live;
        }

        /// <summary>Liveness from one snapshot. DataID is the primary signal: a
        /// perfectly still head (OpenTrack deadzone/filter settled) keeps identical
        /// pose bytes but a ticking DataID, and must stay live. Writers that never
        /// tick DataID fall back to "pose bytes changed".</summary>
        internal bool Observe(byte[] snap, double now)
        {
            uint id = BitConverter.ToUInt32(snap, OffDataId);
            if (!_haveLast)
            {
                _lastId = id;
                Buffer.BlockCopy(snap, OffPose, _lastPose, 0, PoseBytes);
                _haveLast = true;
                return false;
            }
            bool idChanged = id != _lastId;
            if (idChanged)
            {
                _idTicks = true;
            }
            bool changed = _idTicks ? idChanged : !PoseEqual(snap);
            if (changed)
            {
                _lastId = id;
                Buffer.BlockCopy(snap, OffPose, _lastPose, 0, PoseBytes);
                _lastChangeTime = now;
                _seenChange = true;
            }
            return _seenChange && now - _lastChangeTime <= StaleSeconds;
        }

        /// <summary>FreeTrack → normalized (OpenTrack convention, degrees, cm).
        /// Rejects non-finite and implausible values.</summary>
        internal bool Decode(byte[] snap, out HeadPose pose)
        {
            pose = default(HeadPose);
            float yawRad = BitConverter.ToSingle(snap, OffYaw);
            float pitchRad = BitConverter.ToSingle(snap, OffPitch);
            float rollRad = BitConverter.ToSingle(snap, OffRoll);
            if (!PoseChecks.IsFinite(yawRad) || !PoseChecks.IsFinite(pitchRad) || !PoseChecks.IsFinite(rollRad))
            {
                return false;
            }
            if (Math.Abs(yawRad) > MaxAbsRadians || Math.Abs(pitchRad) > MaxAbsRadians || Math.Abs(rollRad) > MaxAbsRadians)
            {
                if (!_warnedUnits)
                {
                    _warnedUnits = true;
                    Plugin.Log?.LogWarning("FreeTrack: angle beyond one turn in radians — the writer looks like it "
                        + "sends degrees (not FreeTrack 2.0 compliant). Frames are ignored.");
                }
                return false;
            }
            pose.Yaw = -yawRad * RadToDeg;      // FT +left  → +right
            pose.Pitch = -pitchRad * RadToDeg;  // FT +up    → +down
            pose.Roll = rollRad * RadToDeg;     // FT +left  → +left
            pose.X = BitConverter.ToSingle(snap, OffX) * 0.1f;   // mm → cm
            pose.Y = BitConverter.ToSingle(snap, OffY) * 0.1f;
            pose.Z = BitConverter.ToSingle(snap, OffZ) * 0.1f;
            pose.Frame = BitConverter.ToUInt32(snap, OffDataId);
            pose.Valid = true;
            return true;
        }

        private bool PoseEqual(byte[] snap)
        {
            for (int i = 0; i < PoseBytes; i++)
            {
                if (snap[OffPose + i] != _lastPose[i])
                {
                    return false;
                }
            }
            return true;
        }

        private bool TryOpen()
        {
            if (!BitConverter.IsLittleEndian)
            {
                return false;   // FreeTrack is an x86 shared-memory protocol
            }
            try
            {
                _mmf = MemoryMappedFile.OpenExisting(SharedMemoryName, MemoryMappedFileRights.Read);
                _view = _mmf.CreateViewAccessor(0, SnapshotSize, MemoryMappedFileAccess.Read);
                return true;
            }
            catch (Exception)
            {
                // Not running / output not enabled: caller retries after a cooldown.
                Close();
                return false;
            }
        }

        private void Close()
        {
            if (_view != null)
            {
                _view.Dispose();
                _view = null;
            }
            if (_mmf != null)
            {
                _mmf.Dispose();
                _mmf = null;
            }
            _haveLast = false;
            _seenChange = false;
            _idTicks = false;
        }

        internal static double Now()
        {
            return Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        }

        public void Dispose()
        {
            Close();
        }
    }
}

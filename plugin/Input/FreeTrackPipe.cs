using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace ApocalypterHeadTracking.Input
{
    /// <summary>
    /// FreeTrack 2.0 protocol reader: OpenTrack's "FreeTrack 2.0" output writes the
    /// pose into the named memory-mapped file "FT_SharedMem". Layout (FreeTrack
    /// client protocol, all little-endian):
    ///   int32 dataID, int32 camWidth, int32 camHeight, then 6 floats:
    ///   Yaw, Pitch, Roll (degrees), X, Y, Z (translations).
    /// dataID conventions differ between implementations (0 and non-zero both occur
    /// for valid data in the wild), so it is read but not trusted.
    /// The file only exists while OpenTrack runs with the output enabled, so open
    /// failures are retried lazily with a cooldown.
    /// </summary>
    public class FreeTrackPipe : ITrackerInput
    {
        public const string SharedMemoryName = "FT_SharedMem";
        private const int PoseOffset = 12;   // 3 int32 header words
        private const int PoseSize = 24;     // 6 floats

        private MemoryMappedFile _mmf;
        private MemoryMappedViewAccessor _view;
        private float _retryAt;
        private byte[] _lastBytes = new byte[PoseSize];
        private bool _haveLast;
        private double _lastChangeTime = double.MinValue;

        public string ModeName { get { return "FreeTrack"; } }

        public bool TryGetPose(out HeadPose pose)
        {
            pose = default(HeadPose);
            if (_view == null)
            {
                if (UnityEngine.Time.realtimeSinceStartup < _retryAt)
                {
                    return false;
                }
                if (!TryOpen())
                {
                    _retryAt = UnityEngine.Time.realtimeSinceStartup + 1f;
                    return false;
                }
            }
            try
            {
                pose.Yaw = _view.ReadSingle(PoseOffset);
                pose.Pitch = _view.ReadSingle(PoseOffset + 4);
                pose.Roll = _view.ReadSingle(PoseOffset + 8);
                pose.X = _view.ReadSingle(PoseOffset + 12);
                pose.Y = _view.ReadSingle(PoseOffset + 16);
                pose.Z = _view.ReadSingle(PoseOffset + 20);
                // Staleness: the mapping survives the tracker process (the game's
                // open handle keeps it alive) and keeps the last-written pose
                // frozen. Only trust values that are actually changing.
                byte[] now = new byte[PoseSize];
                _view.ReadArray(PoseOffset, now, 0, PoseSize);
                double t = UnityEngine.Time.realtimeSinceStartup;
                if (!_haveLast)
                {
                    _lastChangeTime = t;
                    Buffer.BlockCopy(now, 0, _lastBytes, 0, PoseSize);
                    _haveLast = true;
                }
                else if (!BytesEqual(now))
                {
                    _lastChangeTime = t;
                    Buffer.BlockCopy(now, 0, _lastBytes, 0, PoseSize);
                }
                else if (t - _lastChangeTime > 0.5)
                {
                    pose = default(HeadPose);
                    return false;   // tracker gone: frozen values are stale
                }
                pose.Valid = true;
                return true;
            }
            catch (Exception)
            {
                // The tracker closed its end mid-read: drop the mapping and retry.
                Close();
                _retryAt = UnityEngine.Time.realtimeSinceStartup + 1f;
                return false;
            }
        }

        private bool BytesEqual(byte[] now)
        {
            for (int i = 0; i < PoseSize; i++)
            {
                if (now[i] != _lastBytes[i])
                {
                    return false;
                }
            }
            return true;
        }

        private bool TryOpen()
        {
            try
            {
                _mmf = MemoryMappedFile.OpenExisting(SharedMemoryName);
                _view = _mmf.CreateViewAccessor(0, PoseOffset + PoseSize, MemoryMappedFileAccess.Read);
                return true;
            }
            catch (Exception)
            {
                // Not running / not enabled / permission: caller retries later.
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
            _lastChangeTime = double.MinValue;
        }

        public void Dispose()
        {
            Close();
        }
    }
}

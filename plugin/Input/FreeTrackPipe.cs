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
        }

        public void Dispose()
        {
            Close();
        }
    }
}

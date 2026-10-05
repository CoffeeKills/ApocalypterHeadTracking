using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ApocalypterHeadTracking.Input
{
    /// <summary>
    /// OpenTrack "UDP over network" listener. Each packet is 48 bytes: 6 little-endian
    /// doubles. The mod assumes the OpenTrack order (X, Y, Z translations, then
    /// Yaw, Pitch, Roll in degrees); if live testing shows otherwise, only the
    /// offsets in Parse need to change. A background thread keeps only the latest
    /// packet; TryGetPose returns it while it is fresh (&lt; 500 ms old).
    /// </summary>
    public class OpenTrackUdp : ITrackerInput
    {
        private const double MaxAgeSeconds = 0.5;

        private readonly UdpClient _client;
        private readonly Thread _thread;
        private readonly object _lock = new object();
        private HeadPose _latest;
        private double _lastPacketTime = double.MinValue;
        private volatile bool _stopping;

        public OpenTrackUdp(int port)
        {
            Port = port;
            _client = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            _client.Client.ReceiveBufferSize = 4096;
            _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "ApocalypterHeadTrackingUdp" };
            _thread.Start();
        }

        public string ModeName { get { return "OpenTrack UDP"; } }

        public int Port { get; private set; }

        private void ReceiveLoop()
        {
            while (!_stopping)
            {
                try
                {
                    IPEndPoint remote = null;
                    byte[] data = _client.Receive(ref remote);
                    HeadPose pose;
                    if (Parse(data, out pose))
                    {
                        lock (_lock)
                        {
                            _latest = pose;
                            _lastPacketTime = TimeNow();
                        }
                    }
                }
                catch (SocketException)
                {
                    // Socket closed during Dispose.
                    return;
                }
                catch (Exception)
                {
                    // A malformed packet must not kill the listener.
                }
            }
        }

        public bool TryGetPose(out HeadPose pose)
        {
            lock (_lock)
            {
                pose = _latest;
                return _lastPacketTime != double.MinValue && TimeNow() - _lastPacketTime < MaxAgeSeconds;
            }
        }

        private static bool Parse(byte[] data, out HeadPose pose)
        {
            pose = default(HeadPose);
            if (data.Length < 48)
            {
                return false;
            }
            double x = BitConverter.ToDouble(data, 0);
            double y = BitConverter.ToDouble(data, 8);
            double z = BitConverter.ToDouble(data, 16);
            double yaw = BitConverter.ToDouble(data, 24);
            double pitch = BitConverter.ToDouble(data, 32);
            double roll = BitConverter.ToDouble(data, 40);
            if (double.IsNaN(yaw) || double.IsNaN(pitch))
            {
                return false;
            }
            pose.X = (float)x;
            pose.Y = (float)y;
            pose.Z = (float)z;
            pose.Yaw = (float)yaw;
            pose.Pitch = (float)pitch;
            pose.Roll = (float)roll;
            pose.Valid = true;
            return true;
        }

        private static double TimeNow()
        {
            return TimeSpan.FromTicks(DateTime.UtcNow.Ticks).TotalSeconds;
        }

        public void Dispose()
        {
            _stopping = true;
            try
            {
                _client.Close();
            }
            catch (Exception)
            {
                // Already closed.
            }
        }
    }
}

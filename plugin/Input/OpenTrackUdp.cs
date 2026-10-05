using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ApocalypterHeadTracking.Input
{
    /// <summary>
    /// OpenTrack "UDP over network" listener.
    ///
    /// Packet — verified against OpenTrack's source: proto-udp sends
    /// writeDatagram((const char*)headpose, sizeof(double[6])), and headpose is
    /// indexed by api/plugin-api.hpp's Axis enum {TX=0, TY=1, TZ=2, Yaw=3, Pitch=4,
    /// Roll=5}: 48 bytes, 6 native-endian (x86 = little-endian) doubles, degrees and
    /// centimetres, OpenTrack's internal convention — which is already the mod's
    /// normalized HeadPose convention, so no sign changes here.
    ///
    /// A background thread receives into a reusable buffer (no per-packet
    /// allocation) and keeps only the latest pose. The thread never dies on a socket
    /// error short of Dispose; a receive timeout makes it re-check the stop flag.
    /// The constructor throws if the port cannot be bound — the runtime catches that
    /// and retries with a cooldown.
    /// </summary>
    public class OpenTrackUdp : ITrackerInput
    {
        internal const int PacketSize = 48;
        internal const int IdxX = 0, IdxY = 1, IdxZ = 2, IdxYaw = 3, IdxPitch = 4, IdxRoll = 5;
        private const double MaxAgeSeconds = 0.5;
        private const int ReceiveTimeoutMs = 250;

        private readonly Socket _socket;
        private readonly Thread _thread;
        private readonly object _lock = new object();
        private readonly byte[] _buf = new byte[512];   // > 48 so oversized garbage is seen, not truncated into a "valid" pose
        private HeadPose _latest;
        private double _lastPacketTime;
        private bool _havePacket;
        private uint _counter;
        private volatile bool _stopping;

        public OpenTrackUdp(int port)
        {
            Port = port;
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                _socket.ReceiveBufferSize = 4096;
                _socket.ReceiveTimeout = ReceiveTimeoutMs;
                // Mono enables SO_REUSEADDR on UDP sockets by default (verified on
                // Mono: a second Bind to the same port succeeds). Two listeners would
                // then silently share the port, each seeing only some datagrams.
                // Exclusive use turns that into a clean "port busy" at Bind.
                try
                {
                    _socket.ExclusiveAddressUse = true;
                }
                catch (Exception)
                {
                    // Platform without the option: fall back to the default.
                }
                _socket.Bind(new IPEndPoint(IPAddress.Any, port));
            }
            catch
            {
                _socket.Close();   // never leak a half-built socket on "port busy"
                throw;
            }
            _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "ApocalypterHeadTrackingUdp" };
            _thread.Start();
        }

        public string ModeName { get { return "OpenTrack UDP"; } }

        public int Port { get; private set; }

        private void ReceiveLoop()
        {
            while (!_stopping)
            {
                int n;
                try
                {
                    n = _socket.Receive(_buf);
                }
                catch (ObjectDisposedException)
                {
                    return;   // Dispose closed the socket
                }
                catch (SocketException)
                {
                    if (_stopping)
                    {
                        return;
                    }
                    // Timeout (re-check _stopping), ICMP reset, oversized datagram
                    // (WSAEMSGSIZE): none of these may kill the listener.
                    continue;
                }
                catch (Exception)
                {
                    if (_stopping)
                    {
                        return;
                    }
                    Thread.Sleep(50);
                    continue;
                }
                HeadPose pose;
                if (n == PacketSize && Parse(_buf, out pose))
                {
                    double t = FreeTrackPipe.Now();
                    lock (_lock)
                    {
                        pose.Frame = ++_counter;
                        _latest = pose;
                        _lastPacketTime = t;
                        _havePacket = true;
                    }
                }
            }
        }

        public bool TryGetPose(out HeadPose pose)
        {
            double now = FreeTrackPipe.Now();
            lock (_lock)
            {
                pose = _latest;
                return _havePacket && now - _lastPacketTime < MaxAgeSeconds;
            }
        }

        /// <summary>48-byte OpenTrack packet → normalized pose. Rejects
        /// non-finite / implausible angles (another app on the port, corrupt data).</summary>
        internal static bool Parse(byte[] data, out HeadPose pose)
        {
            pose = default(HeadPose);
            if (data == null || data.Length < PacketSize || !BitConverter.IsLittleEndian)
            {
                return false;
            }
            float yaw = (float)BitConverter.ToDouble(data, IdxYaw * 8);
            float pitch = (float)BitConverter.ToDouble(data, IdxPitch * 8);
            float roll = (float)BitConverter.ToDouble(data, IdxRoll * 8);
            if (!PoseChecks.IsPlausibleAngle(yaw) || !PoseChecks.IsPlausibleAngle(pitch) || !PoseChecks.IsPlausibleAngle(roll))
            {
                return false;
            }
            float x = (float)BitConverter.ToDouble(data, IdxX * 8);
            float y = (float)BitConverter.ToDouble(data, IdxY * 8);
            float z = (float)BitConverter.ToDouble(data, IdxZ * 8);
            pose.X = PoseChecks.IsFinite(x) ? x : 0f;
            pose.Y = PoseChecks.IsFinite(y) ? y : 0f;
            pose.Z = PoseChecks.IsFinite(z) ? z : 0f;
            pose.Yaw = yaw;
            pose.Pitch = pitch;
            pose.Roll = roll;
            pose.Valid = true;
            return true;
        }

        public void Dispose()
        {
            _stopping = true;
            try
            {
                _socket.Close();   // releases the port immediately; unblocks Receive
            }
            catch (Exception)
            {
                // Already closed.
            }
        }
    }
}

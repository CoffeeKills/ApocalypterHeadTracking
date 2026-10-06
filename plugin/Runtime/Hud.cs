using ApocalypterHeadTracking.Settings;
using UnityEngine;

namespace ApocalypterHeadTracking.Runtime
{
    /// <summary>
    /// Minimal IMGUI status line: input source, tracking state, live yaw/pitch and
    /// the recenter hint. Pure convenience for setup — everything it shows is
    /// driven by HeadTrackingRuntime, nothing here touches the camera.
    /// The line is rebuilt at 10 Hz and drawn on Repaint only: OnGUI runs several
    /// times per frame, and the old per-call string building allocated every time.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        private const float RebuildInterval = 0.1f;

        private HeadTrackingRuntime _runtime;
        private string _line = string.Empty;
        private float _nextBuild;

        private void Awake()
        {
            _runtime = GetComponent<HeadTrackingRuntime>();
        }

        private void OnGUI()
        {
            if (!HeadTrackingSettings.ShowHud || _runtime == null || Event.current.type != EventType.Repaint)
            {
                return;
            }
            if (Time.unscaledTime >= _nextBuild)
            {
                _nextBuild = Time.unscaledTime + RebuildInterval;
                _line = BuildLine();
            }
            GUI.Box(new Rect(8f, Screen.height - 34f, 470f, 26f), GUIContent.none);
            GUI.Label(new Rect(16f, Screen.height - 28f, 460f, 20f), _line);
        }

        private string BuildLine()
        {
            string source = HeadTrackingSettings.SimulateInput ? "simulated"
                : HeadTrackingSettings.InputMode == HeadTrackingSettings.InputOpenTrackUdp
                    ? "UDP :" + HeadTrackingSettings.UdpPort
                    : "FreeTrack";
            string line = "Head Tracking [" + source + "]: " + _runtime.Status;
            if (_runtime.Status == "tracking")
            {
                if (!_runtime.CameraActive)
                {
                    line += " (idle: no 1st-person camera)";
                }
                else
                {
                    line += "  yaw " + _runtime.DisplayYaw.ToString("0.0")
                        + " pitch " + _runtime.DisplayPitch.ToString("0.0");
                }
                if (HeadTrackingSettings.Mode == HeadTrackingSettings.ModeRotationOnly)
                {
                    line += "  [rotation only]";
                }
                else if (HeadTrackingSettings.Mode == HeadTrackingSettings.ModePositionOnly)
                {
                    line += "  [lean only]";
                }
                if (HeadTrackingSettings.RecenterKey != KeyCode.None)
                {
                    line += "  (" + HeadTrackingSettings.RecenterKey + " recenter)";
                }
            }
            return line;
        }
    }
}

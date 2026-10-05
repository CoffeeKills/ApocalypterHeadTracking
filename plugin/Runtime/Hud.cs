using ApocalypterHeadTracking.Runtime;
using ApocalypterHeadTracking.Settings;
using UnityEngine;

namespace ApocalypterHeadTracking.Runtime
{
    /// <summary>
    /// Minimal IMGUI status line: input source, tracking state, live yaw/pitch and
    /// the recenter hint. Pure convenience for setup — everything it shows is
    /// driven by HeadTrackingRuntime, nothing here touches the camera.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        private HeadTrackingRuntime _runtime;

        private void Awake()
        {
            _runtime = GetComponent<HeadTrackingRuntime>();
        }

        private void OnGUI()
        {
            if (!HeadTrackingSettings.ShowHud || _runtime == null)
            {
                return;
            }
            string line = "Head Tracking: " + _runtime.Status;
            if (_runtime.Status == "tracking")
            {
                line += "  yaw " + _runtime.DisplayYaw.ToString("0.0")
                    + " pitch " + _runtime.DisplayPitch.ToString("0.0");
                if (HeadTrackingSettings.RecenterKey != KeyCode.None)
                {
                    line += "  (" + HeadTrackingSettings.RecenterKey + " recenter)";
                }
            }
            GUI.Box(new Rect(8f, Screen.height - 34f, 430f, 26f), GUIContent.none);
            GUI.Label(new Rect(16f, Screen.height - 28f, 420f, 20f), line);
        }
    }
}

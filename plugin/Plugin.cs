using ApocalypterHeadTracking.Persistence;
using ApocalypterHeadTracking.Runtime;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ApocalypterHeadTracking
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        // The game destroys plugin-created objects on scene load (its scene FSMs
        // sweep the scene roots), so all runtime logic lives on a hidden runner
        // object the cleanup cannot find — the same recipe Apocasetter uses.
        private static GameObject _runner;

        private void Awake()
        {
            Log = Logger;

            ModConfig.Load(Config);

            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureRunner("Awake");

            Log.LogInfo(PluginInfo.PLUGIN_NAME + " " + PluginInfo.PLUGIN_VERSION + " loaded. "
                + "Headtracking is applied to the first-person camera (PlayerCamera) only.");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureRunner("scene " + scene.name);
        }

        private static void EnsureRunner(string why)
        {
            // HideAndDontSave keeps the object out of the game's scene-cleanup sweeps;
            // the game cannot destroy what it cannot find.
            if (_runner != null && _runner.activeInHierarchy)
            {
                return;
            }
            if (_runner != null)
            {
                Destroy(_runner);
                Log.LogDebug("Stale (inactive) runtime runner destroyed.");
            }
            _runner = new GameObject("ApocalypterHeadTrackingRuntime")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(_runner);
            _runner.AddComponent<HeadTrackingRuntime>();
            _runner.AddComponent<Hud>();
            Log.LogDebug("Runtime runner created (" + why + ").");
        }

        private void OnDestroy()
        {
            // The game destroys the plugin object on scene load; this is expected.
            // The runner keeps working regardless.
            ModConfig.Save();
        }
    }
}

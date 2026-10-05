namespace ApocalypterHeadTracking
{
    internal static class PluginInfo
    {
        // Own GUID (fresh config file; no legacy migration needed).
        public const string PLUGIN_GUID = "dev.apocalypter.headtracking";
        public const string PLUGIN_NAME = "Apocalypter Head Tracking";
        // BepInEx 5 parses this with System.Version: numeric-only, no pre-release
        // tags. "-alpha" makes BepInEx skip the whole plugin at load.
        public const string PLUGIN_VERSION = "0.1.2";
    }
}

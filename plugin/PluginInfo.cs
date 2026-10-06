namespace ApocalypterHeadTracking
{
    internal static class PluginInfo
    {
        // Own GUID (fresh config file; no legacy migration needed).
        public const string PLUGIN_GUID = "dev.apocalypter.headtracking";
        // Display name as shown in the Apocasetter Mods list. The "Apocalypter"
        // prefix was dropped (0.1.7): every mod in the list starts with it, so it
        // only truncated the part that tells mods apart.
        public const string PLUGIN_NAME = "Head Tracking";
        // BepInEx 5 parses this with System.Version: numeric-only, no pre-release
        // tags. "-alpha" makes BepInEx skip the whole plugin at load.
        public const string PLUGIN_VERSION = "0.1.7";
    }
}

using BepInEx;

namespace SRRAssetPatcherPlugin
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            SRPlugin.SRPlugin.Awaken(this);

            // If you aren't managing your Harmony patching directly
            // If you plan to just enable all of your patches immediately
            // Then all you would call here (instead of my FeatureManager above)
            // would be:
            //      HarmonyInst.PatchAll();
            // or something like:
            //      var assembly = Assembly.GetExecutingAssembly();
            //      HarmonyInst.PatchAll(assembly);
            // either of those will search your .dll for properly annotated classes
            // and apply their patches.

        }
    }
}

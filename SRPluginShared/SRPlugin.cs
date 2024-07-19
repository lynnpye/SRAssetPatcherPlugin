using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace SRPlugin
{
    public static class SRPlugin
    {
        private static string _assemblyPath = null;
        private static List<FeatureImpl> _featureImpls = null;
        private static string _featureSectionName = null;
        private static string _harmonyID = null;
        private static ManualLogSource _logger = null;

        public static string AssemblyPath { get => _assemblyPath ??= Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
        public static string AssetOverrideRoot { get => Application.persistentDataPath; }
        public static BaseUnityPlugin Plugin { get; private set; }
        public static ConfigFile ConfigFile { get => Plugin.Config; }
        public static List<FeatureImpl> FeatureImpls { get => _featureImpls ??= PopulateFeaturesInfo(); }
        public static string FeatureSectionName { get => _featureSectionName ?? $"_{PluginName} Features"; private set => _featureSectionName = value; }
        public static string HarmonyID { get => _harmonyID ??= $"{Guid.NewGuid().ToString()}"; }
        public static Harmony Harmony { get; private set; }
        public static ManualLogSource Logger { get => _logger ??= AccessTools.PropertyGetter(Plugin.GetType(), "Logger").Invoke(Plugin, null) as ManualLogSource; }
        public static string PluginName { get => Plugin.Info.Metadata.Name; }
        public static string PluginGUID { get => Plugin.Info.Metadata.GUID; }
        public static Version PluginVersion { get => Plugin.Info.Metadata.Version; }
        public static Version PreviousPluginVersion { get; private set; }

        public static void Squawk(string msg, params object[] args)
        {
            Logger.LogInfo($"\nXXXX {PluginName} Squawk XXXX\n\n{string.Format(msg, args)}\n\nXXXXXXXXXXXXXXXXXXXXXXXXX");
        }

        public static void Awaken(BaseUnityPlugin plugin, string featureSectionName = null)
        {
            Plugin = plugin;

            // Registering the string[] type converter
            TomlTypeConverter.AddConverter(typeof(string[]), new StringListTypeConverter());

            FeatureSectionName = featureSectionName;

            Harmony = new Harmony(HarmonyID);

            // initializing to v9.0, to force an update on next deployment
            ConfigEntry<string> ciPreviousPluginVersionString =
                SRPlugin.ConfigFile.Bind<string>(
                    SRPlugin.FeatureSectionName,
                    nameof(PluginVersion),
                    null,
                    "readonly setting to mark the version for which this config was generated; changing this will not generally be helpful"
                );
            PreviousPluginVersion = new Version(ciPreviousPluginVersionString.Value ?? new Version(1, 0).ToString());
            ciPreviousPluginVersionString.Value = PluginVersion.ToString();

            foreach (var f in FeatureImpls)
            {
                f.Initialize();
            }

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

            Logger.LogInfo($"Plugin {PluginName} {PluginVersion} is loaded!");
        }

        private static List<FeatureImpl> PopulateFeaturesInfo(bool forceReload = false)
        {
            List<FeatureImpl> featureImpls = new List<FeatureImpl>();

            Assembly assembly = Assembly.GetExecutingAssembly();
            try
            {
                Type fImpl = typeof(FeatureImpl);
                foreach (Type type in assembly.GetTypes())
                {
                    if (!type.IsAbstract && type.IsClass && type.IsSubclassOf(fImpl))
                    {
                        featureImpls.Add(Activator.CreateInstance(type) as FeatureImpl);
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                Console.WriteLine($"Error finding Feature implementations in assembly '{assembly.FullName}': {ex.Message}");
            }

            return featureImpls;
        }
    }
}

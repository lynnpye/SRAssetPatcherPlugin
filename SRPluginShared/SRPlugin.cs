using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SRPlugin
{
    public delegate ConfigFile GetConfigFileDelegate();
    public delegate ManualLogSource GetLoggerDelegate();

    public static class SRPlugin
    {
        public static string HarmonyID { get; private set; }
        public static Harmony Harmony { get; private set; }
        private static GetConfigFileDelegate GetConfigFile;
        private static GetLoggerDelegate GetLogger;
        private static string _assemblyPath;

        public static string AssemblyPath
        {
            get
            {
                if (_assemblyPath == null)
                {
                    _assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                }
                return _assemblyPath;
            }
        }

        public static ConfigFile ConfigFile
        {
            get
            {
                return GetConfigFile.Invoke();
            }
        }

        public static ManualLogSource Logger
        {
            get
            {
                return GetLogger.Invoke();
            }
        }

        public static void Awaken(GetConfigFileDelegate configFileDelegate, GetLoggerDelegate loggerDelegate)
        {
            GetConfigFile = configFileDelegate;
            GetLogger = loggerDelegate;

            HarmonyID = $"{Guid.NewGuid().ToString()}";
            Harmony = new Harmony(HarmonyID);

            InitializeFeatures();

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

        public static void InitializeFeatures()
        {
            foreach (var f in FeatureImpls)
            {
                f.Initialize();
            }
        }

        private static List<FeatureImpl> featureImpls = new List<FeatureImpl>();

        private static List<FeatureImpl> FeatureImpls
        {
            get
            {
                PopulateFeaturesInfo();
                return featureImpls;
            }
        }

        private static void PopulateFeaturesInfo(bool forceReload = false)
        {
            if (featureImpls == null) featureImpls = new List<FeatureImpl>();

            if (!forceReload && featureImpls.Count > 0) return;

            featureImpls.Clear();

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
        }
    }
}

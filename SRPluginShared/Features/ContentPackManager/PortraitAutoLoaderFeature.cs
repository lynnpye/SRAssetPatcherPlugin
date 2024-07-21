using BepInEx.Configuration;
using HarmonyLib;
using isogame;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace SRPlugin.Features.PortraitAutoLoader
{
    internal class PortraitAutoLoaderFeature
  : FeatureImpl
    {
        private static ConfigItem<bool> CIPortraitAutoLoaderEnabled;
        private static ConfigItem<string[]> CIAssetLoadPath;
        private static ConfigItem<string[]> CIContentPackNamesToAutoDependencyInject;

        public PortraitAutoLoaderFeature()
            : base(
                  nameof(PortraitAutoLoader),
                  new List<ConfigItemBase>()
                  {
                      (CIPortraitAutoLoaderEnabled = new ConfigItem<bool>(PLUGIN_FEATURES_SECTION, nameof(PortraitAutoLoader), true, "auto loads portraits from the specified contentpack-like folder structure")),
                      (CIAssetLoadPath = new ConfigItem<string[]>(nameof(AssetLoadPath), [], "organize files in a content pack like structure under these folders and the plugin can try to use them")),
                      (CIContentPackNamesToAutoDependencyInject = new ConfigItem<string[]>(nameof(ContentPackNamesToAutoDependencyInject), [], "content pack names to automatically inject as dependencies")),
                  },
                  new List<PatchRecord>(
                      PatchRecord.RecordPatches(
                          AccessTools.Method(
                              typeof(FileLoaderPatch),
                              nameof(FileLoaderPatch.IncludeSteamContentPacksPrefix)
                          ),
                          AccessTools.Method(
                              typeof(ProjectInfoPatch),
                              nameof(ProjectInfoPatch.ResolveDependenciesPrefix)
                          )
                      )
                  )
                  {

                  }
                  )
        {
        }

        public static bool PortraitAutoLoaderEnabled { get => CIPortraitAutoLoaderEnabled.GetValue(); set => CIPortraitAutoLoaderEnabled.SetValue(value); }
        public static string[] AssetLoadPath { get => CIAssetLoadPath.GetValue(); set => CIAssetLoadPath.SetValue(value); }
        public static string[] ContentPackNamesToAutoDependencyInject { get => CIContentPackNamesToAutoDependencyInject.GetValue(); set => CIContentPackNamesToAutoDependencyInject.SetValue(value); }

        [HarmonyPatch(typeof(FileLoader.ProjectInfo))]
        internal class ProjectInfoPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(FileLoader.ProjectInfo.ResolveDependencies))]
            public static void ResolveDependenciesPrefix(FileLoader.ProjectInfo __instance)
            {
                var allContentPacks = AccessTools.Field(typeof(FileLoader), "allContentPacks").GetValue(LazySingletonBehavior<FileLoader>.Instance) as BetterList<FileLoader.ProjectInfo>;

                if (allContentPacks == null)
                {
                    SRPlugin.Squawk($"Could not find allContentPacks"); return;
                }

                foreach (var insertPackName in ContentPackNamesToAutoDependencyInject)
                {
                    var contentPack = allContentPacks?.buffer?.FirstOrDefault(p => p?.Name == insertPackName);
                    if (contentPack == null)
                    {
                        SRPlugin.Squawk($"Could not find content pack {insertPackName} to inject as dependency");
                        continue;
                    }
                    SRPlugin.Squawk($"Injecting content pack {contentPack.Name} as dependency");
                    // make a PackageRef and add it to
                    var pkgref = new PackageRef();
                    pkgref.package_id = contentPack?.ProjectId;
                    pkgref.package_name = contentPack?.Name;
                    pkgref.package_version = contentPack?.Version;
                    pkgref.package_description = contentPack?.Description;

                    if (__instance.ProjectDef.content_pack_dependencies.Contains(pkgref))
                    {
                        continue;
                    }

                    __instance.ProjectDef.content_pack_dependencies.Insert(0, pkgref);
                } 
            }
        }

        [HarmonyPatch(typeof(FileLoader))]
        internal class FileLoaderPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch("IncludeSteamContentPacks")]
            public static void IncludeSteamContentPacksPrefix(FileLoader __instance)
            {
                if (!PortraitAutoLoaderEnabled)
                {
                    SRPlugin.Squawk($"PortraitAutoLoader is disabled, skipping");
                    return;
                }
                if (AssetLoadPath == null || AssetLoadPath.Length == 0)
                {
                    SRPlugin.Squawk($"No AssetLoadPath specified, skipping");
                    return;
                }

                var IncludeContentPackSearchPath = AccessTools.Method(typeof(FileLoader), "IncludeContentPackSearchPath", [typeof(string), typeof(FileLoader.ProjectState)]);
                if (IncludeContentPackSearchPath == null)
                {
                    SRPlugin.Squawk($"Could not find IncludeContentPackSearchPath method");
                    return;
                }

                foreach (var path in AssetLoadPath)
                {
                    IncludeContentPackSearchPath.Invoke(__instance, [path, FileLoader.ProjectState.Local]);
                }
            }
        }
    }
}

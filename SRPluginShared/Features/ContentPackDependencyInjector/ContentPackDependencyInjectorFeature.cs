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

namespace SRPlugin.Features.ContentPackDependencyInjector
{
    internal class ContentPackDependencyInjectorFeature  : FeatureImpl
    {
        private static ConfigItem<bool> CIContentPackDependencyInjectorEnabled;
        private static ConfigItem<string[]> CIContentPackFolders;
        private static ConfigItem<string[]> CIContentPackNamesToAutoDependencyInject;

        public ContentPackDependencyInjectorFeature()
            : base(
                  nameof(ContentPackDependencyInjector),
                  new List<ConfigItemBase>()
                  {
                      (CIContentPackDependencyInjectorEnabled = new ConfigItem<bool>(PLUGIN_FEATURES_SECTION, nameof(ContentPackDependencyInjector), true, "custom content pack folder location and auto inject selected content packs as dependencies for loaded content")),
                      (CIContentPackFolders = new ConfigItem<string[]>(nameof(ContentPackFolders), [], "additional folders to search for content packs")),
                      (CIContentPackNamesToAutoDependencyInject = new ConfigItem<string[]>(nameof(ContentPackNamesToAutoDependencyInject), [], "content pack names to automatically inject as dependencies; also accepts project_id")),
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

        public static bool ContentPackDependencyInjectorEnabled { get => CIContentPackDependencyInjectorEnabled.GetValue(); set => CIContentPackDependencyInjectorEnabled.SetValue(value); }
        public static string[] ContentPackFolders { get => CIContentPackFolders.GetValue(); set => CIContentPackFolders.SetValue(value); }
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
                    var contentPack = allContentPacks?.buffer?.FirstOrDefault(p => p?.Name == insertPackName || p?.ProjectId == insertPackName);
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
                if (!ContentPackDependencyInjectorEnabled)
                {
                    return;
                }
                if (ContentPackFolders == null || ContentPackFolders.Length == 0)
                {
                    return;
                }

                var IncludeContentPackSearchPath = AccessTools.Method(typeof(FileLoader), "IncludeContentPackSearchPath", [typeof(string), typeof(FileLoader.ProjectState)]);
                if (IncludeContentPackSearchPath == null)
                {
                    SRPlugin.Squawk($"Could not find IncludeContentPackSearchPath method");
                    return;
                }

                foreach (var path in ContentPackFolders)
                {
                    IncludeContentPackSearchPath.Invoke(__instance, [path, FileLoader.ProjectState.Local]);
                }
            }
        }
    }
}

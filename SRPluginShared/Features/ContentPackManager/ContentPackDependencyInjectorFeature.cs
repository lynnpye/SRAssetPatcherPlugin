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

namespace SRPlugin.Features.ContentPackManager
{
    internal class ContentPackDependencyInjectorFeature  : FeatureImpl
    {
        /*
         * ContentPackDependencyInjectorFeature tries to allow the user to change how
         * content packs are loaded and injected as dependencies. So far it has the following
         * functions:
         * - ContentPackDependencyInjector: the main switch to enable or disable the feature
         * - ContentPackFolders: additional folders to search for content packs, like adding custom 
         * - ContentPackNamesToAutoDependencyInject: content pack names to automatically inject as
         * dependencies for loaded content.
         * 
         * At first all I wanted to do was allow you to add custom portraits without having
         * to go through the hassle of repackaging a content pack to replace or add them.
         * 
         * By the time I got to the point of writing this blurb, it turned into
         * a way to just put a content pack into the dependency list of another content pack
         * dynamically.
         * 
         * So far, it works for portraits. I created a new .png that was just a solid green
         * rectangle, the same size as a portrait picture. The AI thought it would be
         * funny since that's part of what it uses when it.. anyway.... I created a new
         * foo.pl.json because I hear that's the rage, got a foo.pl.bytes, and put it
         * into a subfolder structure where you would expect it to be, along with the
         * .png file where you would expect it to be. I used my project.cpack.json to
         * get a project.cpkac.bytes an put that into the appropriately location. 
         * 
         * Then I put that content pack folder into (which was appropriately named
         * 'ContentPack') a folder listed in 'ContentPackFolders', so that my content
         * pack could be loaded. After some trial and error, I got to the point where
         * I could create my little portrait only content pack, with my made up
         * project_id, and put it into a folder that I then put into a content pack
         * search path, and then also put my content pack into the first slot of the
         * dependency list of each story as it resolved those dependencies.
         * 
         * I can imagine providing configuration options to allow precise selection of
         * inserting things into the game's content engine, so it's early days. That said,
         * I did manage to allow mostly dynamic portrait injection; I say mostly because
         * I also think you could allow dynamic content packs, including a more
         * precise injection process. 
         * 
         * For example:
         * Custom Content Pack Folders - Pick your own location outside of the game's
         * install folder to put your custom content packs.
         * 
         * Content Pack Injection - Instead of my puny little toy injection scheme, how
         * about a robust system, maybe with regex, maybe just a few simple flags or fields,
         * that allows you to not only have your dynamic content packs, but also to 
         * inject that content (i.e. specific items within your dynamic content pack) into
         * specific parts of the flow, i.e. part of a pre-cache generation mechanism or something
         * to override the default content pack's items with your own.
         * 
         * Hypothetical Example:
         * Dynamic Content Packs - You configure a folder that loads these dynamic content packs,
         * each with the various .item.bytes (or .item.json, but that's another thing) files in
         * the correct location (auto convertring the .item.json maybe? but that's another thing)
         * and even autogenerating (and caching) a project.cpack.bytes if one isn't already provided
         * (or figuring it out from a project.cpack.json, but that is entirely a different thing).
         * 
         * Smart Content Injection - A flexible system? that allows you to do things like
         * drop your convo.bytes (or, convo.json, but you do you) into your folder along with
         * some configuration garnish, to grab control of a particular character's dialog
         * at a specific point, handling further dialog or whatever. Or adding new default characters
         * or replacing existing ones or just deleting them. Adding new items to shops
         * based on some criteria, so you can play with things like the Adept improvement
         * mod and the various improvements to attributes and cyberware and magic, or
         * the additional weapons mods, but in the existing games, or any game that 
         * happens to have "merchants" and "conversants" that the injector can hook into.
         * 
         * Content Pack... Packs... - So you like content packs A and B, but would like
         * to play with their content while in the story from content pack C. And all of
         * these may or may not be the core HBS content packs (i.e. the core games), so
         * you could, hypothetically, create a dynamic content pack that expects you to
         * have the individual actual content packs in the game's content pack folder as
         * usual, but comes with its own dependency list, including specific 
         * dynamic injection syntax, to place specific items at specific vendors for
         * a specific content pack, and then basically launch *that* game. You've
         * now created a content pack-enstein's.. monster.. who was the good guy
         * in the end, right? Or at least.. anyway... folks could just require
         * you have the appropriate content packs in place, drop a set of configurations
         * into a dynamic content pack and have an entirely mix and match customized
         * experience. Hypothetically.
         */

        private static ConfigItem<bool> CIContentPackDependencyInjectorEnabled;
        private static ConfigItem<string[]> CIAppendedContentPackFolders;
        private static ConfigItem<string[]> CIContentPackNamesToAutoDependencyInject;

        public ContentPackDependencyInjectorFeature()
            : base(
                  nameof(ContentPackManager),
                  new List<ConfigItemBase>()
                  {
                      (CIContentPackDependencyInjectorEnabled = new ConfigItem<bool>(PLUGIN_FEATURES_SECTION, nameof(ContentPackManager), true, "custom content pack folder location and auto inject selected content packs as dependencies for loaded content")),
                      (CIAppendedContentPackFolders = new ConfigItem<string[]>(nameof(AppendedContentPackFolders), [], "additional folders to search for content packs; it just means the game will be aware of them")),
                      (CIContentPackNamesToAutoDependencyInject = new ConfigItem<string[]>(nameof(ContentPackNamesToAutoDependencyInject), [], "content pack names to automatically inject as dependencies; also accepts project_id")),
                  },
                  new List<PatchRecord>(
                      PatchRecord.RecordPatches(
                          AccessTools.Method(
                              typeof(FileLoaderPatch),
                              nameof(FileLoaderPatch.SetEditorDataTempPathsPrefix)
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
        public static string[] AppendedContentPackFolders { get => CIAppendedContentPackFolders.GetValue(); set => CIAppendedContentPackFolders.SetValue(value); }
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
            /**
             * This is lazy and shall probably be punished. I'm patching
             * FileLoader.SetEditorDataTempPaths because when it's called in
             * the Editor, I don't care, and the only other spot happens to
             * be exactly in FileLoader.refreshProjectList() where I would
             * want to be able to add content packs dynamically and safely
             * call it "appending". As a side effect, for example, my
             * custom portraits in my random custom content pack were
             * automatically picked up running the otherwise bog standard
             * HBS base campaigns. Without having to ship a modified .dll.
             * I like how that feels.
             * 
             * Anyhow it's lazy because I *could* be making use of the
             * transpile type patches but I don't want to go down
             * that path just yet.
             * 
             * That said, if I were creating an API, this would be where
             * I would expose something like an AppendContentPackToDependencyList()
             * call that would let you just attach to that event.
             * Of course, I'm imagining a much larger amount of participation
             * before I would expect that to happen, but it's a direction.
             * Implies a Prepend same. And makes you think about other things you
             * could do between these two calls to be able to put
             * something else somewhere it otherwise isn't. But in
             * a standardized, friendly, plugins play well together easily,
             * sort of way.
             * 
             */
            [HarmonyPrefix]
            [HarmonyPatch(nameof(FileLoader.SetEditorDataTempPaths))]
            public static void SetEditorDataTempPathsPrefix(FileLoader __instance)
            {
                if (!ContentPackDependencyInjectorEnabled)
                {
                    return;
                }
                if (AppendedContentPackFolders == null || AppendedContentPackFolders.Length == 0)
                {
                    return;
                }

                var IncludeContentPackSearchPath = AccessTools.Method(typeof(FileLoader), "IncludeContentPackSearchPath", [typeof(string), typeof(FileLoader.ProjectState)]);
                if (IncludeContentPackSearchPath == null)
                {
                    SRPlugin.Squawk($"Could not find IncludeContentPackSearchPath method");
                    return;
                }

                foreach (var path in AppendedContentPackFolders)
                {
                    IncludeContentPackSearchPath.Invoke(__instance, [path, FileLoader.ProjectState.Local]);
                }
            }
        }
    }
}

using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SRPlugin.Features.MusicPatcherFeature
{
    internal class MusicPatcherFeature : FeatureImpl
    {
        private static ConfigItem<bool> CIMusicPatcherEnabled;
        private static Dictionary<string, string> ReplacementAssetLocations = new Dictionary<string, string>();
        private static string REPLACEMENTS_SECTION = "MusicReplacements";

        private class ReplacementItem
        {
            public string asset;
            public string file;
        }

        private static string GetLongDescription()
        {
            return
$@"
To replace asset requests with files from disk, you will need to add a [Replacements] section.
In the [Replacements] section, you will then need to add two lines per replacement, in the form of:

[Replacements]

keyofyourchoosing.asset = some/asset-lookup-name
keyofyourchoosing.file = ..\some\folder\file.wav

anotherkey.asset = another/asset-lookup-name
anotherkey.file = c:\maybe\a\full\path\file.wav

Relative paths will start from the ""{SRPlugin.AssetOverrideRoot}"" folder. Note how 'keyofyourchoosing' and 'anotherkey' are the
first part of two keys, each ending in '.asset' and '.file'. You can pick the first part and need to make it distinct between
your various replacements, and the second part says which entry it is, the asset or the file path.

So if you have a theme.wav file in the BepInEx\musicpatches\ folder, and you want to replace the startup music, you would use:

[Replacements]

hktitletheme.asset = Music/HongKong-TitleTheme-UI
hktitletheme.file = ..\musicpatches\theme.wav

MusicPatcherEnabled = true|false -- is this music patcher feature to be enabled?
";
        }

        public MusicPatcherFeature()
            : base(new List<ConfigItemBase>
                {
                    (CIMusicPatcherEnabled = new ConfigItem<bool>(FEATURES_SECTION, nameof(MusicPatcherEnabled), true, GetLongDescription()))
                }, new List<PatchRecord>()
                {
                    PatchRecord.Postfix(
                        typeof(Resources)
                            .GetMethod(
                                nameof(Resources.Load),
                                [typeof(string), typeof(Type)]
                            ),
                        typeof(ResourcesPatch)
                            .GetMethod(
                                nameof(ResourcesPatch.LoadPostfix)
                                )
                        )
                })
        {

        }

        private static void PopulateReplacements()
        {
            // let's get the list of things we should be replacing ready before patching to allow us to replace them
            // we aren't binding, so these are going to only exist as OrphanedEntries, which is private
            Dictionary<ConfigDefinition, string> orphans = PrivateEye.GetPrivateGetterValue<Dictionary<ConfigDefinition, string>>(SRPlugin.ConfigFile, "OrphanedEntries", null);

            if (orphans == null)
            {
                SRPlugin.Logger.LogInfo($"No orphaned entries exist for the MusicPatcher to patch, are you sure your .cfg is setup correctly?");
                return;
            }

            Dictionary<string, ReplacementItem> reps = new Dictionary<string, ReplacementItem>();

            foreach (var key in orphans.Keys)
            {
                if (!REPLACEMENTS_SECTION.Equals(key.Section, StringComparison.OrdinalIgnoreCase)) continue;

                string keyname = key.Key;

                if (
                    (!keyname.EndsWith(".file", StringComparison.OrdinalIgnoreCase) && !keyname.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                    || keyname.StartsWith(".")
                    )
                {
                    SRPlugin.Logger.LogInfo($"Existence of incorrectly formatted entry key '{keyname}' in [Replacements] section suggests a badly formed .cfg file");
                }

                int dotIndex = keyname.LastIndexOf('.');

                string tag = keyname.Substring(0, dotIndex);

                string val = orphans[key];

                ReplacementItem repitem = reps.ContainsKey(tag) ? reps[tag] : (reps[tag] = new ReplacementItem());

                bool isFilePath = keyname.EndsWith(".file", StringComparison.OrdinalIgnoreCase);
                if (isFilePath)
                {
                    string filePath = GetFullPath(val);
                    repitem.file = filePath;
                }
                else
                {
                    repitem.asset = val;
                }
            }

            foreach (var tag in reps.Keys)
            {
                var rep = reps[tag];
                if (rep.file == null || rep.asset == null)
                {
                    SRPlugin.Logger.LogInfo($"Found file or asset for tag {tag} but not both");
                    continue;
                }
                ReplacementAssetLocations[rep.asset] = rep.file;
            }
        }

        public static string GetFullPath(string filePath)
        {
            var root = Path.GetPathRoot(filePath);
            if (!(root.StartsWith(@"\") || root.EndsWith(@"\") && root != @"\"))
            {
                filePath = Path.GetFullPath(Path.Combine(Application.persistentDataPath, filePath));
            }
            return filePath;
        }

        public static bool MusicPatcherEnabled
        {
            get
            {
                return CIMusicPatcherEnabled.GetValue();
            }

            set
            {
                CIMusicPatcherEnabled.SetValue(value);
            }
        }

        public override void HandleEnabled()
        {
            SRPlugin.Logger.LogInfo(string.Format("\n{0}{0}\n{1}\n\n{0}{0}", "=========\n", $""));
            PopulateReplacements();
        }

        /*//////*/

        private class AudioClipBox
        {
            public AudioClip clip;
        }

        private static IEnumerator<WWW> GetWWWAudioClip(string wwwUri, AudioClipBox box)
        {
            using (var www = new WWW(wwwUri))
            {
                while (!www.isDone)
                {
                    SRPlugin.Logger.LogInfo(string.Format("\n{0}{0}\n{1}\n\n{0}{0}", "=========\n", $"not done, pre-yield: www.isDone:{www.isDone}  progress:{www.progress}:"));
                    if (!www.isDone)
                    {
                        yield return www;
                    }
                }

                if (www.isDone)
                {
                    SRPlugin.Logger.LogInfo(string.Format("\n{0}{0}\n{1}\n\n{0}{0}", "=========\n", $"GetWWWAudioClip: www.isDone:{www.isDone}:  progress:{www.progress}:"));
                    box.clip = www.GetAudioClip(false, true);
                }
            }
        }

        private static bool IsKnownMusicSuffix(string filePath)
        {
            List<string> knownMusicSuffixes = new List<string>([
                ".wav", ".ogg"
            ]);

            bool isKnown = knownMusicSuffixes.Exists(s => filePath.ToLower().EndsWith(s));

            if (!isKnown)
            {
                SRPlugin.Logger.LogInfo(string.Format("\n{0}{0}\n{1}\n\n{0}{0}", "=========\n", $"unknown music suffix for {filePath}"));
            }

            return isKnown;
        }

        private static UnityEngine.Object GetReplacementUnityObjectFromFile(string assetPath, string filePath)
        {
            if (assetPath.StartsWith("Music/") && assetPath.Length > 6 && IsKnownMusicSuffix(filePath))
            {
                AudioClipBox clipHolder = new AudioClipBox();

                var uri = new Uri(filePath);
                var wenum = GetWWWAudioClip(uri.ToString().Replace(" ", "%20"), clipHolder);

                // waiting for the thing to complete the fetch
                while (wenum.MoveNext()) ;

                // and clipHolder should now have our audioClip!
                var uo = clipHolder.clip;
                SRPlugin.Logger.LogInfo(string.Format("\n{0}{0}\n{1}\n\n{0}{0}", "=========\n", $"loading {filePath} as music"));

                return uo;
            }

            return null;
        }

        [HarmonyPatch(typeof(Resources))]
        public class ResourcesPatch
        {
            [HarmonyPostfix]
            // I need to get a linter
            [HarmonyPatch(
                typeof(Resources),
                nameof(Resources.Load),
                [typeof(string), typeof(Type)]
                )]
            public static void LoadPostfix(ref UnityEngine.Object __result, string path)
            {
                if (ReplacementAssetLocations.ContainsKey(path))
                {
                    string filePath = ReplacementAssetLocations[path];

                    UnityEngine.Object uobj = GetReplacementUnityObjectFromFile(path, filePath);

                    if (uobj == null)
                    {
                        SRPlugin.Logger.LogInfo($"Unable to convert file \"{filePath}\" to UnityEngine.Object");
                        uobj = __result;
                    }

                    __result = uobj;
                }
            }

            public static void LoadPostfix2(ref UnityEngine.Object __result, string path, Type typeRef = null)
            {

            }
        }
    }
}

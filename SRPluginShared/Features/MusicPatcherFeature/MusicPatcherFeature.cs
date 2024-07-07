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
        private static ConfigItem<bool> CILogMusicAssetsRequested;

        public MusicPatcherFeature()
            : base(new List<ConfigItemBase>
                {
                    (CIMusicPatcherEnabled = new ConfigItem<bool>(FEATURES_SECTION, nameof(MusicPatcherEnabled), true, GetLongDescription())),
                    (CILogMusicAssetsRequested = new ConfigItem<bool>(FEATURES_SECTION, nameof(LogMusicAssetsRequested), false, "outputs each asset request prefixed with the string 'Music/', to help aid in finding a specific music asset name")),
                }, new List<PatchRecord>()
                {
                    PatchRecord.Postfix(
                        typeof(Resources)
                            .GetMethod(
                                nameof(Resources.Load),
                                [typeof(string)]
                            ),
                        typeof(ResourcesPatch)
                            .GetMethod(
                                nameof(ResourcesPatch.LoadPostfixWithPath)
                                )
                        ),
                    PatchRecord.Postfix(
                        typeof(Resources)
                            .GetMethod(
                                nameof(Resources.Load),
                                [typeof(string), typeof(Type)]
                            ),
                        typeof(ResourcesPatch)
                            .GetMethod(
                                nameof(ResourcesPatch.LoadPostfixWithPathAndType)
                                )
                        ),
#if SRR
                    PatchRecord.Prefix(
                        //typeof(SoundManager).GetMethod("SetupMusicPrefab"),
                        AccessTools.Method(typeof(SoundManager), "SetupMusicPrefab"),
                        typeof(SoundManagerPatch).GetMethod(nameof(SoundManagerPatch.SetupMusicPrefabPrefix))
                        ),
#endif
#if !SRR && SRR
                    PatchRecord.Prefix(
                        AccessTools.Method(typeof(SoundManager), "SetupMusicSource"),
                        typeof(SoundManagerPatch).GetMethod(nameof(SoundManagerPatch.SetupMusicSourcePrefix))
                        ),
#endif
                })
        {

        }

        public override void PreApplyPatches()
        {
            PopulateReplacements();
        }

        public static bool MusicPatcherEnabled { get => CIMusicPatcherEnabled.GetValue(); set => CIMusicPatcherEnabled.SetValue(value); }
        public static bool LogMusicAssetsRequested { get => CILogMusicAssetsRequested.GetValue(); set => CILogMusicAssetsRequested.SetValue(value); }

        private static Dictionary<string, string> ReplacementAssetLocations = new Dictionary<string, string>();

        private static Dictionary<string, AudioClip> ReplacementAssetSamples = new Dictionary<string, AudioClip>();
        private static Dictionary<string, AudioClipDataBucket> ReplacementAssetDataBuckets = new Dictionary<string, AudioClipDataBucket>();

        private static string REPLACEMENTS_SECTION = "MusicReplacements";

        public class AudioClipDataBucket
        {
            public float[] samples
            {
                get; set;
            }
            public int channels
            {
                get; set;
            }
            public int frequency
            {
                get; set;
            }
            public string name
            {
                get; set;
            }
            public HideFlags hideFlags
            {
                get; set;
            }
            public bool _3D
            {
                get; set;
            }

            public AudioClipDataBucket(AudioClip audioClip)
            {
                this.channels = audioClip.channels;
                this.frequency = audioClip.frequency;
                float[] _samples = new float[audioClip.samples * audioClip.channels];
                audioClip.GetData(_samples, 0);
                this.samples = _samples;
                this.name = audioClip.name;
                this.hideFlags = audioClip.hideFlags;
            }

            // may I find comfort from a higher power
            private static HashSet<int> _knownClips = new HashSet<int>();
            private static HashSet<int> KnownClips { get => _knownClips ??= new HashSet<int>(); }
            
            public AudioClip ClipFromBucket()
            {
                var newClip = AudioClip.Create(this.name, this.samples.Length, this.channels, this.frequency, false, false);

                KnownClips.Add(newClip.GetInstanceID());

                newClip.SetData(this.samples, 0);
                newClip.hideFlags = this.hideFlags;

                return newClip;
            }
        }

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
                    if (!www.isDone)
                    {
                        yield return www;
                    }
                }

                if (www.isDone)
                {
                    box.clip = www.GetAudioClip(false, false);
                }
            }
        }

        private static bool IsMusicAssetPath(string assetPath)
        {
            return assetPath.StartsWith("Music/") && assetPath.Length > 6;
        }

        private static AudioClip GetReplacementUnityObjectFromFile(string assetPath, string filePath)
        {
            if (IsMusicAssetPath(assetPath))
            {
                AudioClipBox clipHolder = new AudioClipBox();

                var uri = new Uri(filePath);
                var wenum = GetWWWAudioClip(uri.ToString(), clipHolder);

                // waiting for the thing to complete the fetch
                while (wenum.MoveNext()) ;

                // and clipHolder should now have our audioClip!
                var uo = clipHolder.clip;

                return uo;
            }

            return null;
        }

        private static void StandoutLog(string msg, params object[] args)
        {
            string bar = "*X*X*X*X*X*X*X*X*X*X";
            SRPlugin.Logger.LogInfo(string.Format("\n{1}\n\n{0}\n\n{1}\n", string.Format(msg, args), bar));
        }

        private static void PostfixLoad(ref UnityEngine.Object __result, string path)
        {
#if SRR && !SRR
            if (!path.EndsWith("-Prefab"))
            {
                PostfixLoad_AudioClip(ref __result, path);
                return;
            }
#else
            PostfixLoad_AudioClip(ref __result, path);
#endif
        }

        private static void PostfixLoad_AudioClip(ref UnityEngine.Object __result, string path)
        {
            AudioClip clip = GetClipFromPath(path);
            if (clip == null)
            {
                StandoutLog($"Tried to load/retrieve audio clip {path} and got null");
                return;
            }
            __result = clip;
        }

        // may I find comfort from a higher power
        private static HashSet<int> _knownClips = null;
        private static HashSet<int> KnownClips { get => _knownClips ??= new HashSet<int>(); }

        private static AudioClip GetClipFromPath(string path)
        {
            AudioClipDataBucket theBucket = null;

            if (ReplacementAssetDataBuckets.ContainsKey(path))
            {
                theBucket = ReplacementAssetDataBuckets[path];
            }

            if (ReplacementAssetLocations.ContainsKey(path))
            {
                string filePath = ReplacementAssetLocations[path];

                AudioClip uobj = GetReplacementUnityObjectFromFile(path, filePath);

                if (uobj == null)
                {
                    SRPlugin.Logger.LogInfo($"Unable to convert file \"{filePath}\" to UnityEngine.Object");
                    return null;
                }
                uobj.name = path.Substring(6);

                // ReplacementAssetSamples[path] = uobj;
                theBucket = new AudioClipDataBucket(uobj);
                ReplacementAssetDataBuckets[path] = theBucket;
            }

            AudioClip theClip = null;
            if (theBucket != null)
            {
                theClip = theBucket.ClipFromBucket();
                theClip.name = theBucket.name;
                KnownClips.Add(theClip.GetInstanceID());
            }

            return theClip;
        }

        private static void CloneGameObjectAudioClip(string path, AudioSource resultAudioSource)
        {
            // no clones for good clips
            if (resultAudioSource != null && resultAudioSource.clip != null && KnownClips.Contains(resultAudioSource.clip.GetInstanceID()))
            {
                return;
            }
            
            AudioClip clip = GetClipFromPath(path);

            if (clip != null)
            {
                resultAudioSource.clip = clip;
                resultAudioSource.loop = true;
                resultAudioSource.playOnAwake = true;
                
                return;
            }

            return;
        }

        private static void LogMusicAssetRequest(string path, Type systemTypeInstance = null)
        {
            if (!LogMusicAssetsRequested) return;

            var label = 
                systemTypeInstance == null
                ? $".asset = {path})"
                : $".asset = {path} (expecting typeof {systemTypeInstance}, this is probably not interesting)"
                ;

            var msg = "";
            if (path.EndsWith("-Prefab"))
            {
                msg += $"\n-- Prefab was requested; If you want to replace this one, try <yourReplacementKey>.asset = {path.Substring(0, path.Length - 7)}";
            }

            StandoutLog($"LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL\n{label}{msg}");
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
            public static void LoadPostfixWithPathAndType(ref UnityEngine.Object __result, string path, Type systemTypeInstance)
            {
                if (!IsMusicAssetPath(path)) return;

                LogMusicAssetRequest(path, systemTypeInstance );

                PostfixLoad(ref __result, path);
            }

            [HarmonyPostfix]
            [HarmonyPatch(
                typeof(Resources),
                nameof(Resources.Load),
                [typeof(string)]
                )]
            public static void LoadPostfixWithPath(ref UnityEngine.Object __result, string path)
            {
                if (!IsMusicAssetPath(path)) return;

                LogMusicAssetRequest(path);

                PostfixLoad(ref __result, path);
            }
        }

#if SRR
        [HarmonyPatch(typeof(SoundManager))]
        public class SoundManagerPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(SoundManager), "SetupMusicPrefab")]
            public static bool SetupMusicPrefabPrefix(ref AudioSource __result, SoundManager __instance, string sound,
                ref GameObject ___musicObject, ref GameObject ___musicObjectPrevious, ref string ___musicSourceName, ref string ___musicSourceNamePrevious,
                ref AudioSource ___musicSource, ref AudioSource ___musicSourcePrevious)
            {
                if (___musicObject != null)
                {
                    if (string.Compare(___musicSourceName, sound, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        AudioSource resultAudioSource = ___musicObject.GetComponent<AudioSource>();

                        CloneGameObjectAudioClip("Music/" + sound, resultAudioSource);

                        __result = resultAudioSource;
                        return false;
                    }
                    AudioSource audioSource = ___musicObject.GetComponent<AudioSource>();
                    if (audioSource != null)
                    {
                        audioSource.Stop();
                    }
                    if (___musicObjectPrevious != null)
                    {
                        if (string.Compare(___musicSourceNamePrevious, sound, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            GameObject gameObject = ___musicObject;
                            string text = ___musicSourceName;
                            ___musicObject = ___musicObjectPrevious;
                            ___musicSourceName = ___musicSourceNamePrevious;
                            ___musicObjectPrevious = gameObject;
                            ___musicSourceNamePrevious = text;
                            ___musicSource = ___musicObject.GetComponent<AudioSource>();

                            CloneGameObjectAudioClip("Music/" + sound, ___musicSource);

#if !SRR
// a holdover from when these patches were going to apply to more than SRR
                            __instance.AuditMusicTwo();
#endif
                            __result = ___musicSource;
                            return false;
                        }
                        audioSource = ___musicObjectPrevious.GetComponent<AudioSource>();
                        if (audioSource != null)
                        {
                            Resources.UnloadAsset(audioSource.clip);
                        }
                    }
                    ___musicObjectPrevious = ___musicObject;
                    ___musicObject = null;
                    ___musicSourceNamePrevious = ___musicSourceName;
                    ___musicSourceName = string.Empty;
                }

                GameObject gameObject2 = Resources.Load("Music/" + sound + "-Prefab", typeof(GameObject)) as GameObject;
                if (gameObject2 != null)
                {
                    ___musicObject = global::UnityEngine.Object.Instantiate(gameObject2) as GameObject;
                    ___musicSource = ___musicObject.GetComponent<AudioSource>();

                    CloneGameObjectAudioClip("Music/" + sound, ___musicSource);

                    ___musicSourceName = sound;
                    SoundManager.SoundType soundType = SoundManager.SoundType.Music;
                    int num = (int)soundType;
                    AudioSource unusedSource = SoundManager.GetUnusedSource(soundType);
                    GameObject gameObject3 = unusedSource.gameObject;
                    ___musicObject.transform.parent = unusedSource.gameObject.transform.parent;

                    int[] numSources = PrivateEye.GetPrivateFieldValue<int[]>(LazySingletonBehavior<SoundManager>.Instance, "numSources", null);
                    AudioSource[,] sources = PrivateEye.GetPrivateFieldValue<AudioSource[,]>(LazySingletonBehavior<SoundManager>.Instance, "sources", null);
                    for (int i = 0; i < numSources[num]; i++)
                    {
                        if (sources[num, i] == unusedSource)
                        {
                            sources[num, i] = ___musicSource;
                            ___musicObject.name = gameObject3.name;
                            ___musicObject.transform.parent = gameObject3.transform.parent;
                            global::UnityEngine.Object.Destroy(unusedSource);
                            global::UnityEngine.Object.Destroy(gameObject3);
                            break;
                        }
                    }
#if !SRR
// a holdover from when these patches were going to apply to more than SRR
                    __instance.AuditMusicTwo();
#endif
                }
                else
                {
                    ___musicSource =
                        AccessTools.Method(typeof(SoundManager), "SetupMusicSource", [typeof(string)]).Invoke(__instance, [sound])
                        as AudioSource;
                }

                __result = ___musicSource;
                return false;
            }

#if !SRR && SRR
// a holdover from when these patches were going to apply to more than SRR
// variables are purposefully set to be false always
// want to retain the code but never risk compiling it
            [HarmonyPrefix]
            [HarmonyPatch(typeof(SoundManager), "SetupMusicSource")]
            public static bool SetupMusicSourcePrefix(ref AudioSource __result, SoundManager __instance, string sound,
                ref GameObject ___musicObject, ref GameObject ___musicObjectPrevious, ref string ___musicSourceName, ref string ___musicSourceNamePrevious,
                ref AudioSource ___musicSource, ref AudioSource ___musicSourcePrevious)
            {

                StandoutLog($"source {sound} ping 0");
                if (string.Compare(___musicSourceName, sound, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    StandoutLog($"source {sound} ping 1");
                    CloneGameObjectAudioClip("Music/" + sound, ___musicSource);
#if !SRR
// a holdover from when these patches were going to apply to more than SRR
                    __instance.AuditMusicTwo();
#endif

                    __result = ___musicSource;
                    return false;
                }
                if (string.Compare(___musicSourceNamePrevious, sound, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    StandoutLog($"source {sound} ping 2");
                    ___musicSource.Stop();
                    AudioSource audioSource = ___musicSource;
                    ___musicSourceNamePrevious = ___musicSourceName;
                    ___musicSource = ___musicSourcePrevious;
                    ___musicSourceName = sound;
                    ___musicSourcePrevious = audioSource;

                    CloneGameObjectAudioClip("Music/" + sound, ___musicSource);
#if !SRR
// a holdover from when these patches were going to apply to more than SRR
                    __instance.AuditMusicTwo();
#endif

                    __result = ___musicSource;
                    return false;
                }
                if (___musicSourcePrevious != null)
                {
                    StandoutLog($"source {sound} ping 3");
                    Resources.UnloadAsset(___musicSourcePrevious.clip);
                    ___musicSourcePrevious.Stop();
                    ___musicSourcePrevious.clip = null;
                }
                StandoutLog($"source {sound} ping 4");
                ___musicSourcePrevious = ___musicSource;
                ___musicSourceNamePrevious = ___musicSourceName;
                ___musicSource = SoundManager.GetUnusedSource(SoundManager.SoundType.Music);
                ___musicSourceName = sound;
                StandoutLog($"source {sound} ping 4.1  musicSource.loop:{___musicSource.loop}: playOnAwake:{___musicSource.playOnAwake}:");
                AudioClip audioClip = GetClipFromPath("Music/" + sound);
                StandoutLog($"source {sound} ping 4.2");

                if (audioClip == null)
                {
                    StandoutLog($"source {sound} ping 5");
                    audioClip = Resources.Load("Music/" + sound, typeof(AudioClip)) as AudioClip;
                }

                if (audioClip != null)
                {
                    StandoutLog($"source {sound} ping 6");
                    ___musicSource.clip = audioClip;
                    ___musicSource.loop = true;
                }
                StandoutLog($"source {sound} ping 7  musicSource.loop:{___musicSource.loop}: playOnAwake:{___musicSource.playOnAwake}:");
#if !SRR
// a holdover from when these patches were going to apply to more than SRR
                __instance.AuditMusicTwo();
#endif

                __result = ___musicSource;
                return false;
            }
#endif
        }
#endif

    }
}

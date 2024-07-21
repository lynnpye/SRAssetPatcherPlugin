using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace SRPlugin
{
    public enum PatchType
    {
        None,
        UnpatchOnly,
        Prefix,
        Postfix,
        Reverse
    }

    public class PatchRecord
    {
        private MethodBase original;
        private MethodInfo patch;
        private PatchType patchType;
        private int priority;

        public PatchRecord(MethodBase original, MethodInfo patch, PatchType patchType, int priority = Priority.Normal)
        {
            this.original = original;
            this.patch = patch;
            this.patchType = patchType;
            this.priority = priority;
        }

        public static PatchRecord UnpatchOnly(MethodBase original, MethodInfo patch)
        {
            return new PatchRecord(original, patch, PatchType.UnpatchOnly);
        }

        public static PatchRecord Prefix(MethodBase original, MethodInfo patch, int priority = Priority.Normal)
        {
            return new PatchRecord(original, patch, PatchType.Prefix, priority);
        }

        public static PatchRecord Postfix(MethodBase original, MethodInfo patch, int priority = Priority.Normal)
        {
            return new PatchRecord(original, patch, PatchType.Postfix, priority);
        }

        public static PatchRecord Reverse(MethodBase original, MethodInfo patch)
        {
            return new PatchRecord(original, patch, PatchType.Reverse);
        }

        public static List<PatchRecord> MakePatchRecords(MethodInfo patch, PatchType patchType, int priority, HarmonyPatch classHarmonyPatch, HarmonyPatch[] harmonyPatches)
        {
            List<PatchRecord> records = new List<PatchRecord>();

            foreach (var harmonyPatch in harmonyPatches)
            {
                records.Add(
                    new PatchRecord(
                        original: AccessTools.Method(classHarmonyPatch.info.declaringType, harmonyPatch.info.methodName, harmonyPatch.info.argumentTypes),
                        patch: patch,
                        patchType: patchType,
                        priority: priority
                        )
                );
            }

            return records;
        }

        public static List<PatchRecord> RecordPatches(params MethodInfo[] patches)
        {
            int priority = Priority.Normal;

            if (patches == null || patches.Length == 0) return null;

            List<PatchRecord> records = new List<PatchRecord>();

            foreach (var patch in patches)
            {
                var harmonyPatches = patch.GetCustomAttributes(typeof(HarmonyPatch), false) as HarmonyPatch[];
                if (harmonyPatches == null || harmonyPatches.Length == 0)
                {
                    SRPlugin.Logger.LogInfo($"Method {patch.Name} is not a Harmony patch method.");
                    return null;
                }

                var classHarmonyPatches = patch.DeclaringType.GetCustomAttributes(typeof(HarmonyPatch), false) as HarmonyPatch[];
                if (classHarmonyPatches == null || classHarmonyPatches.Length == 0)
                {
                    SRPlugin.Logger.LogInfo($"Class {patch.DeclaringType.Name} is not a Harmony patch class.");
                    return null;
                }

                PatchType patchType = PatchType.None;

                var harmonyPrefix = patch.GetCustomAttributes(typeof(HarmonyPrefix), false) as HarmonyPrefix[];
                if (harmonyPrefix != null && harmonyPrefix.Length > 0)
                {
                    patchType = PatchType.Prefix;

                    records.AddRange(MakePatchRecords(patch, patchType, priority, classHarmonyPatches[0], harmonyPatches));
                }

                if (patchType == PatchType.None)
                {
                    var harmonyPostfix = patch.GetCustomAttributes(typeof(HarmonyPostfix), false) as HarmonyPostfix[];
                    if (harmonyPostfix != null && harmonyPostfix.Length > 0)
                    {
                        patchType = PatchType.Postfix;

                        records.AddRange(MakePatchRecords(patch, patchType, priority, classHarmonyPatches[0], harmonyPatches));
                    }
                }

                if (patchType == PatchType.None)
                {
                    var harmonyReverse = patch.GetCustomAttributes(typeof(HarmonyReversePatch), false) as HarmonyReversePatch[];
                    if (harmonyReverse != null && harmonyReverse.Length > 0)
                    {
                        patchType = PatchType.Reverse;

                        records.AddRange(MakePatchRecords(patch, patchType, priority, classHarmonyPatches[0], harmonyPatches));
                    }
                }
            }

            return records;
        }

        public void Patch()
        {
            try
            {
                switch (patchType)
                {
                    case PatchType.Prefix:
                        SRPlugin.Harmony.Patch(original: original, prefix: new HarmonyMethod(method: patch, priority: priority));
                        break;
                    case PatchType.Postfix:
                        SRPlugin.Harmony.Patch(original: original, postfix: new HarmonyMethod(method: patch, priority: priority));
                        break;
                    case PatchType.Reverse:
                        {
                            var reversePatcher = SRPlugin.Harmony.CreateReversePatcher(original, new HarmonyMethod(patch));
                            reversePatcher.Patch();
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                SRPlugin.Logger.LogInfo($"Exception while patching. original:{original}:  patch:{patch}:  patchType:{patchType}:");
                SRPlugin.Logger.LogInfo(e);
            }
        }

        public void Unpatch()
        {
            SRPlugin.Harmony.Unpatch(original, patch);
        }
    }
}

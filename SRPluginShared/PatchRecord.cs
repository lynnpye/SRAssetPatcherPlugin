using HarmonyLib;
using System;
using System.Reflection;

namespace SRPlugin
{
    public enum PatchType
    {
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

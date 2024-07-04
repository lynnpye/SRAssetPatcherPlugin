using HarmonyLib;
using System.Collections.Generic;

namespace SRPlugin.Features.AlwaysBeSprinting
{
    public class AlwaysBeSprintingFeature : FeatureImpl
    {
        private static ConfigItem<bool> CIAlwaysBeSprinting;

        public AlwaysBeSprintingFeature()
            : base(new List<ConfigItemBase>()
                {
                    (CIAlwaysBeSprinting = new ConfigItem<bool>(FEATURES_SECTION, nameof(AlwaysBeSprinting), true, "makes some of the longer treks not so bad"))
                }, new List<PatchRecord>()
                {
                    PatchRecord.Postfix(typeof(Constants).GetMethod(nameof(Constants.LoadDefaults)),
                        typeof(ConstantsPatch).GetMethod(nameof(ConstantsPatch.loadDefaultsPostfix)))
                })
        {

        }

        public static bool AlwaysBeSprinting { get => CIAlwaysBeSprinting.GetValue(); set => CIAlwaysBeSprinting.SetValue(value); }

        private static OverrideableValue<int> OVCombatSprint = new OverrideableValue<int>(Constants.MOVE_THRESHOLD_COMBAT_SPRINT, (v) => Constants.MOVE_THRESHOLD_COMBAT_SPRINT = v, 0);
        private static OverrideableValue<int> OVCombatWalk = new OverrideableValue<int>(Constants.MOVE_THRESHOLD_COMBAT_WALK, (v) => Constants.MOVE_THRESHOLD_COMBAT_WALK = v, 0);
        private static OverrideableValue<int> OVFriendlySprint = new OverrideableValue<int>(Constants.MOVE_THRESHOLD_FRIENDLY_SPRINT, (v) => Constants.MOVE_THRESHOLD_FRIENDLY_SPRINT = v, 0);
        private static OverrideableValue<int> OVFriendlyWalk = new OverrideableValue<int>(Constants.MOVE_THRESHOLD_FRIENDLY_WALK, (v) => Constants.MOVE_THRESHOLD_FRIENDLY_WALK = v, 0);

        public static void ResetStartingValues()
        {
            OVCombatSprint.Reset();
            OVCombatWalk.Reset();
            OVFriendlySprint.Reset();
            OVFriendlyWalk.Reset();
        }

        public static void ApplyOverrideValues()
        {
            if (!AlwaysBeSprinting) return;

            OVCombatSprint.SetDefault();
            OVCombatWalk.SetDefault();
            OVFriendlySprint.SetDefault();
            OVFriendlyWalk.SetDefault();
        }

        [HarmonyPatch(typeof(Constants))]
        internal class ConstantsPatch
        {

            [HarmonyPostfix]
            [HarmonyPatch(nameof(Constants.LoadDefaults))]
            public static void loadDefaultsPostfix()
            {
                // only applies if allowed
                ApplyOverrideValues();
            }
        }
    }
}

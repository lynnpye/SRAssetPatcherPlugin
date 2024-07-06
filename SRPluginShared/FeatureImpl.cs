using BepInEx.Configuration;
using System;
using System.Collections.Generic;

namespace SRPlugin
{
    public abstract class ConfigItemBase
    {
        public abstract void Bind();
        public abstract string GetName();
    }

    public class ConfigItem<T> : ConfigItemBase
    {
        public static ConfigEntry<I_T> Bind<I_T>(string section, string key, I_T defaultValue, string description = null)
        {
            if (description == null)
            {
                return SRPlugin.ConfigFile.Bind(section, key, defaultValue);
            }
            else
            {
                return SRPlugin.ConfigFile.Bind(section, key, defaultValue, description);
            }
        }

        public static I_T GetCEfg<I_T>(ConfigEntry<I_T> configEntry, I_T defaultValue)
        {
            if (configEntry == null)
            {
                return defaultValue;
            }
            return configEntry.Value;
        }

        public static I_T SetCEfg<I_T>(ConfigEntry<I_T> configEntry, I_T value)
        {
            if (configEntry != null)
            {
                configEntry.Value = value;
            }
            return value;
        }

        public string section;
        public string key;
        public string description;

        public ConfigEntry<T> configEntry;
        public T defaultValue;
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        public T? currentValue;
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

        public ConfigItem(string section, string key, T defaultValue, string description = null)
        {
            this.section = section;
            this.key = key;
            this.description = description;
            this.defaultValue = defaultValue;
        }

        public Type GetT()
        {
            return typeof(T);
        }

        public void SetValue(T value)
        {
            currentValue = SetCEfg(configEntry, value);
        }

        public T GetValue()
        {
            return GetCEfg(configEntry, currentValue ?? defaultValue);
        }

        public override void Bind()
        {
            configEntry = Bind(section, key, defaultValue, description);
        }

        public override string GetName()
        {
            return key;
        }
    }

    public abstract class FeatureImpl
    {
        public static string FEATURES_SECTION = "Features";

        private List<ConfigItemBase> configItems;
        private List<PatchRecord> patchRecords;
        // only used if there is no enabling flag as the first config item
        private bool _localOnlyIsEnabled;

        public FeatureImpl(Func<List<ConfigItemBase>> configItemsFunc, List<PatchRecord> patchRecords)
        {
            this.configItems = configItemsFunc();
            this.patchRecords = patchRecords;

            _Init();
        }

        public FeatureImpl(List<ConfigItemBase> configItems, List<PatchRecord> patchRecords)
        {
            this.configItems = configItems;
            this.patchRecords = patchRecords;

            _Init();
        }

        private void _Init()
        {
            if (configItems == null || configItems.Count == 0)
            {
                _localOnlyIsEnabled = true;
            }
        }

        public void Initialize()
        {
            Bind();
            SyncToEnabledState();
        }

        public void Bind()
        {
            if (configItems == null) return;
            foreach (var configItem in configItems)
            {
                configItem.Bind();
            }
        }

        public virtual void PreApplyPatches()
        {

        }

        public virtual void PostApplyPatches()
        {

        }

        public virtual void HandleDisabled()
        {

        }

        public virtual void ApplyPatches()
        {
            foreach(var patchRecord in patchRecords)
            {
                patchRecord.Patch();
            }
        }

        public virtual void UnapplyPatches()
        {
            foreach(var patchRecord in patchRecords)
            {
                patchRecord.Unpatch();
            }
        }

        public bool IsEnabled()
        {
            if (configItems == null || configItems.Count == 0)
            {
                return _localOnlyIsEnabled;
            }
            ConfigItem<bool> firstConfigItem = configItems[0] as ConfigItem<bool>;
            return (firstConfigItem != null && firstConfigItem.GetValue());
        }

        public void SetEnabled(bool enabled)
        {
            if (configItems == null || configItems.Count == 0)
            {
                _localOnlyIsEnabled = enabled;
            }
            else
            {
                ConfigItem<bool> firstConfigItem = configItems[0] as ConfigItem<bool>;
                if (firstConfigItem != null)
                {
                    firstConfigItem.SetValue(enabled);
                }
            }

            SyncToEnabledState();
        }

        public void SyncToEnabledState()
        {
            if (IsEnabled())
            {
                PreApplyPatches();
                ApplyPatches();
                PostApplyPatches();
            }
            else
            {
                UnapplyPatches();
                HandleDisabled();
            }
        }
    }
}

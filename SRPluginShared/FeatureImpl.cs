using BepInEx.Configuration;
using System;
using System.Collections.Generic;

namespace SRPlugin
{
    public abstract class ConfigItemBase
    {
        public Func<string> sectionNameGetter;

        public string SectionName { get; set; }
        public string Key { get; private set; }

        public abstract void Bind(FeatureImpl feature);
        public abstract string GetName();

        public ConfigItemBase(Func<string> sectionNameGetter, string key)
        {
            this.sectionNameGetter = sectionNameGetter;
            this.Key = key;
        }

        public abstract void SetFromString(string value);
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

        //public string section;
        public string description;

        public ConfigEntry<T> configEntry;
        public T defaultValue;
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        public T? currentValue;
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

        public ConfigItem(string key, T defaultValue, string description = null)
            : this(() => null, key, defaultValue, description)
        {
        }

        public ConfigItem(Func<string> sectionNameGetter, string key, T defaultValue, string description = null)
            : base(sectionNameGetter, key)
        {
            this.description = description;
            this.defaultValue = defaultValue;
        }

        public override void SetFromString(string value)
        {
            if (configEntry == null) return;

            configEntry.SetSerializedValue(value);
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

        public override void Bind(FeatureImpl feature)
        {
            SectionName = sectionNameGetter?.Invoke() ?? feature.SettingsSectionName();
            configEntry = Bind(SectionName, Key, defaultValue, description);
        }

        public override string GetName()
        {
            return Key;
        }
    }

    public abstract class FeatureImpl
    {
        public static string PLUGIN_FEATURES_SECTION() => SRPlugin.FeatureSectionName;
        public static string FEATURES_SETTINGS_SECTION(Type featureImplType)
        {
            string sectionName = "Settings";
            foreach (var fImpl in SRPlugin.FeatureImpls)
            {
                if (fImpl.GetType() == featureImplType)
                {
                    sectionName = fImpl.SettingsSectionName();
                    break;
                }
            }
            return sectionName;
        }

        protected List<ConfigItemBase> configItems;
        private List<PatchRecord> patchRecords;
        // only used if there is no enabling flag as the first config item
        private bool _localOnlyIsEnabled;

        public string SettingsSectionName() => $"{this.Name} Settings";

        public FeatureImpl(string name, List<ConfigItemBase> configItems, List<PatchRecord> patchRecords)
        {
            this.Name = name;
            this.configItems = configItems;
            this.patchRecords = patchRecords;

            _Init();
        }

        private void _Init()
        {
            // initialize local only mode for handling Enabled
            if (configItems == null || configItems.Count == 0)
            {
                _localOnlyIsEnabled = true;
            }
            else
            {
                ConfigItem<bool> firstConfigItem = configItems[0] as ConfigItem<bool>;
                if (firstConfigItem == null)
                {
                    _localOnlyIsEnabled = true;
                }
            }
        }

        public string Name { get; private set; }

        protected bool MigrateSetting(string oldSection, ConfigItemBase targetItem)
        {
            return MigrateSetting(oldSection, targetItem?.Key, targetItem);
        }

        protected bool MigrateSetting(string oldSection, string oldKey, ConfigItemBase targetItem)
        {
            if (oldSection == null || oldKey == null || targetItem == null) return false;

            var orphans = SRPlugin.ConfigFile.GetOrphans();

            if (orphans == null)
            {
                SRPlugin.Squawk("Failed to find OrphanedEntries during cfg migration");
                return false;
            }

            ConfigDefinition oldDef = new ConfigDefinition(oldSection, oldKey);

            if (orphans.TryGetValue(oldDef, out var outstr))
            {
                targetItem.SetFromString(outstr);
                orphans.Remove(oldDef);
                return true;
            }

            return false;
        }

        private Version MigrateConfigAfterBind(Version startingVersion)
        {
            if (startingVersion == null) return null;

            Version targetVersion = new Version(10, 0);
            if (startingVersion < targetVersion)
            {
                if (configItems == null)
                {
                    return null;
                }

                string oldFeaturesSection = "Features";
                string newFeaturesSection = PLUGIN_FEATURES_SECTION();

                var orphans = SRPlugin.ConfigFile.GetOrphans();

                // versions prior to 10 used "Features" as the features section, so let's migrate those over if we see them
                foreach (ConfigItemBase configItem in configItems)
                {
                    // does it exist in the old features section? it would be orphaned at this point presumably
                    // Note: this may also affect entries that were previously in the old "Features" section but are
                    // now moved to an individual feature section

                    MigrateSetting(oldFeaturesSection, configItem);
                }

                // no remaining changes
                return null;
            }

            // and then we could do
            // if (SRPlugin.PreviousPluginVersion < new Version(11, 5))
            // note it is NOT an "else if", because we may want to bump up multiple version migrations in a single pass
            // note also this ONLY applies to the CURRENT feature, this is NOT plugin wide
            return null;
        }

        /// <summary>
        /// Override to perform custom ConfigFile migration based on a startingVersion. Implement this by checking
        /// startingVersion to see if is lower than the "oldest" migration you would migrate to. If so, perform the migration and
        /// return the next Version you would migrate to. If there are no other migrations you would perform, return null.
        /// </summary>
        /// <param name="startingVersion"></param>
        /// <returns>A Version representing the next Version the feature would need to perform a migration for.
        /// null if no remaining migrations need to be run.</returns>
        public virtual Version AfterBindMigration(Version startingVersion)
        {
            return null;
        }

        private void MigrateConfiguration()
        {
            Version nextBaseVersion = MigrateConfigAfterBind(new Version(1, 0));
            Version nextOverrideVersion = AfterBindMigration(new Version(1, 0));

            while (nextBaseVersion != null || nextOverrideVersion != null)
            {
                if (nextBaseVersion == null)
                {
                    nextOverrideVersion = AfterBindMigration(nextOverrideVersion);
                }
                else if (nextOverrideVersion == null)
                {
                    nextBaseVersion = MigrateConfigAfterBind(nextBaseVersion);
                }
                else
                {
                    if (nextBaseVersion < nextOverrideVersion)
                    {
                        nextBaseVersion = MigrateConfigAfterBind(nextBaseVersion);
                    }
                    else if (nextOverrideVersion > nextBaseVersion)
                    {
                        nextOverrideVersion = AfterBindMigration(nextOverrideVersion);
                    }
                    else
                    {
                        // versions are identical, do both
                        nextBaseVersion = MigrateConfigAfterBind(nextBaseVersion);
                        nextOverrideVersion = AfterBindMigration(nextOverrideVersion);
                    }
                }
            }
        }

        public void Initialize()
        {
            Bind();
            MigrateConfiguration();
            SRPlugin.ConfigFile.Save();
            SyncToEnabledState();
        }

        public void Bind()
        {
            if (configItems == null) return;
            foreach (var configItem in configItems)
            {
                configItem.Bind(this);
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

        public void ApplyPatches()
        {
            if (patchRecords == null) return;
            foreach (var patchRecord in patchRecords)
            {
                patchRecord.Patch();
            }
        }

        public void UnapplyPatches()
        {
            if (patchRecords == null) return;
            foreach (var patchRecord in patchRecords)
            {
                patchRecord.Unpatch();
            }
        }

        private Func<bool> _isEnabled = null;
        private Action<bool> _setEnabled = null;

        public bool IsEnabled()
        {
            if (_isEnabled == null)
            {
                if (Name == null || configItems == null || configItems.Count == 0)
                {
                    _isEnabled = () => _localOnlyIsEnabled;
                }
                else
                {
                    foreach (ConfigItemBase configItem in configItems)
                    {
                        if (string.Equals(Name, configItem.GetName()))
                        {
                            if (configItem is ConfigItem<bool>)
                            {
                                _isEnabled = () => (configItem as ConfigItem<bool>).GetValue();
                            }
                            // I'm leaving this here for posterity, because they make me feel
                            // uncomfortable and yet they also seem useful. The problem is,
                            // while it seems nice to imagine that an int could be the enable flag,
                            // but what do you do in response to SetEnabled(true)? set it to 0? to 1?
                            // maybe sometimes 0 is an actionable value but sometimes it isn't
                            //else if (configItem is ConfigItem<int>)
                            //{
                            //    _isEnabled = () => (configItem as ConfigItem<int>).GetValue() >= 0;
                            //}
                            //else if (configItem is ConfigItem<float>)
                            //{
                            //    _isEnabled = () => (configItem as ConfigItem<float>).GetValue() >= 0;
                            //}
                            //else if (configItem is ConfigItem<double>)
                            //{
                            //    _isEnabled = () => (configItem as ConfigItem<double>).GetValue() >= 0;
                            //}
                            //else if (configItem is ConfigItem<string>)
                            //{
                            //    _isEnabled = () => !string.IsNullOrEmpty((configItem as ConfigItem<string>).GetValue());
                            //}
                            // and otherwise, we fall back to local mode
                            else
                            {
                                _isEnabled = () => _localOnlyIsEnabled;
                            }
                        }
                    }
                }
            }
            return _isEnabled();
        }

        public void SetEnabled(bool enabled)
        {
            if (_setEnabled == null)
            {
                if (Name == null || configItems == null || configItems.Count == 0)
                {
                    _setEnabled = v => _localOnlyIsEnabled = v;
                }
                else
                {
                    foreach (ConfigItemBase configItem in configItems)
                    {
                        if (string.Equals(Name, configItem.GetName()))
                        {
                            if (configItem is ConfigItem<bool>)
                            {
                                _setEnabled = v => (configItem as ConfigItem<bool>).SetValue(v);
                            }
                            else
                            {
                                _setEnabled = v => _localOnlyIsEnabled = v;
                            }
                        }
                    }
                }
            }

            _setEnabled(enabled);

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

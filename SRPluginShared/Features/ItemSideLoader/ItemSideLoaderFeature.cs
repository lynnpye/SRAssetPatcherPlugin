using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace SRPlugin.Features.ItemSideLoader
{
    public class ItemSideLoaderFeature //: FeatureImpl
    {
        //private static ConfigItem<bool> CIItemSideLoader;

        public ItemSideLoaderFeature()
            //: base(
            //      nameof(ItemSideLoader),
            //      new List<ConfigItemBase>()
            //      {
            //          //(CIItemSideLoader = new ConfigItem<bool>(PLUGIN_FEATURES_SECTION, nameof(ItemSideLoader), true, "enables side loading of items into specified vendor inventories")),
            //      },
            //      new List<PatchRecord>()
            //      {

            //      })
        {

        }

        //public static bool ItemSideLoader { get => CIItemSideLoader.GetValue(); set => CIItemSideLoader.SetValue(value); }

        internal class SomePatch
        {
            public static void SomePostfix()
            {

            }
        }
    }
}

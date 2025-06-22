using Beam.UI;
using HarmonyLib;
using System;

namespace ToolbeltFix.Patches
{
    class StorageMenuPresenter_Patches
    {
        [HarmonyPatch(typeof(StorageMenuPresenter), "Start")]
        class StorageMenuPresenter_Start_Patch
        {
            static readonly AccessTools.FieldRef<StorageMenuPresenter, StorageRadialMenuPresenter> _radialPresenterRef = AccessTools.FieldRefAccess<StorageMenuPresenter, StorageRadialMenuPresenter>("_radialPresenter");

            static void Postfix(StorageMenuPresenter __instance)
            {
                if (!Main.Enabled) return;

                try
                {
                    Main.storagePresenters.Add(_radialPresenterRef(__instance), __instance);
                }
                catch (Exception e)
                {
                    Main.Logger.LogException(e);
                }
            }
        }

        [HarmonyPatch(typeof(StorageMenuPresenter), "OnDestroy")]
        class StorageMenuPresenter_OnDestroy_Patch
        {
            static readonly AccessTools.FieldRef<StorageMenuPresenter, StorageRadialMenuPresenter> _radialPresenterRef = AccessTools.FieldRefAccess<StorageMenuPresenter, StorageRadialMenuPresenter>("_radialPresenter");

            static void Postfix(StorageMenuPresenter __instance)
            {
                if (!Main.Enabled) return;

                try
                {
                    Main.storagePresenters.Remove(_radialPresenterRef(__instance));
                }
                catch (Exception e)
                {
                    Main.Logger.LogException(e);
                }
            }
        }
    }
}

using Beam.UI;
using Beam;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using Beam.Utilities;

namespace ToolbeltFix.Patches
{
    class InventoryMenuPresenter_Patches
    {
        [HarmonyPatch(typeof(InventoryMenuPresenter), "RadialPresenter_ElementSecondaryClick")]
        class InventoryMenuPresenter_RadialPresenter_ElementSecondaryClick_Patch
        {
            static readonly AccessTools.FieldRef<InventoryMenuPresenter, InventoryRadialMenuPresenter> _radialPresenterRef = AccessTools.FieldRefAccess<InventoryMenuPresenter, InventoryRadialMenuPresenter>("_radialPresenter");
            static readonly AccessTools.FieldRef<InventoryMenuPresenter, IPlayer> _playerRef = AccessTools.FieldRefAccess<InventoryMenuPresenter, IPlayer>("_player");

            static readonly MethodInfo GetInventoryData = AccessTools.Method(typeof(InventoryMenuPresenter), "GetInventoryData");

            static bool Prefix(InventoryMenuPresenter __instance, IRadialMenuElement<InventoryData> element)
            {
                if (!Main.Enabled || !Main.Settings.quickDrop) return true;

                try
                {
                    InventoryData selectedValue = element.SelectedValue;
                    IPickupable pickupable = selectedValue?.Value;
                    if (pickupable.IsNullOrDestroyed())
                    {
                        return false;
                    }
                    _playerRef(__instance).Holder.ReplicatedRelease(pickupable);
                    IList<IList<InventoryData>> inventoryData = GetInventoryData.Invoke(__instance, null) as IList<IList<InventoryData>>;
                    _radialPresenterRef(__instance).Initialize(inventoryData);
                    _radialPresenterRef(__instance).Refresh();
                    return false;
                }
                catch (Exception e)
                {
                    Main.Logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(InventoryMenuPresenter), "RadialPresenter_ElementSecondaryShortPress")]
        class InventoryMenuPresenter_RadialPresenter_ElementSecondaryShortPress_Patch
        {
            static readonly AccessTools.FieldRef<InventoryMenuPresenter, InventoryRadialMenuPresenter> _radialPresenterRef = AccessTools.FieldRefAccess<InventoryMenuPresenter, InventoryRadialMenuPresenter>("_radialPresenter");
            static readonly AccessTools.FieldRef<InventoryMenuPresenter, IPlayer> _playerRef = AccessTools.FieldRefAccess<InventoryMenuPresenter, IPlayer>("_player");

            static readonly MethodInfo GetInventoryData = AccessTools.Method(typeof(InventoryMenuPresenter), "GetInventoryData");

            static bool Prefix(InventoryMenuPresenter __instance, IRadialMenuElement<InventoryData> element)
            {
                if (!Main.Enabled || !Main.Settings.stackTransfer) return true;

                try
                {
                    if (element.SelectedValue == null || element is StorageRadialMenuElement)
                    {
                        return false;
                    }
                    IPickupable value = element.SelectedValue.Value;

                    if (!__instance.DestinationStorage.IsNullOrDestroyed())
                    {
                        StorageSlot<IPickupable> storageSlot = (_playerRef(__instance).Inventory.GetSlotStorage() as SlotStorage).FindSlot(value);

                        if (storageSlot != null)
                        {
                            bool refresh = false;

                            Main.silentSlotStorageTransfer = false;

                            while (storageSlot.Objects.Count > 0 && __instance.DestinationStorage.CanPush(storageSlot.Objects[0], !Main.silentSlotStorageTransfer))
                            {
                                IPickupable pickupable = storageSlot.Objects[0];
                                _playerRef(__instance).Holder.ReplicatedRelease(pickupable);
                                __instance.DestinationStorage.ReplicatedPush(pickupable);
                                Main.silentSlotStorageTransfer = true;
                                refresh = true;
                            }

                            Main.silentSlotStorageTransfer = false;

                            if (refresh)
                            {
                                IList<IList<InventoryData>> inventoryData = GetInventoryData.Invoke(__instance, null) as IList<IList<InventoryData>>;
                                _radialPresenterRef(__instance).Initialize(inventoryData);
                                _radialPresenterRef(__instance).Refresh();
                            }
                        }
                    }
                    return false;
                }
                catch (Exception e)
                {
                    Main.Logger.LogException(e);
                    return true;
                }
            }
        }
    }
}

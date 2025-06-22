using Beam.Crafting;
using Beam.UI;
using Beam;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ToolbeltFix.Patches
{
    class RadialMenuPresenterBase_Patches
    {
        [HarmonyPatch(typeof(RadialMenuPresenterBase<InventoryData, InventoryRadialMenuElement, InventoryRadialMenuViewAdapterBase>), "OnElementSecondaryShortPress")]
        class RadialMenuPresenterBase_OnElementSecondaryShortPress_Patch
        {
            static readonly AccessTools.FieldRef<StorageMenuPresenter, ISlotStorage<IPickupable>> _storageRef = AccessTools.FieldRefAccess<StorageMenuPresenter, ISlotStorage<IPickupable>>("_storage");

            static readonly MethodInfo RemoveRefreshCallbacks = AccessTools.Method(typeof(StorageMenuPresenter), "RemoveRefreshCallbacks");
            static readonly MethodInfo AddRefreshCallbacks = AccessTools.Method(typeof(StorageMenuPresenter), "AddRefreshCallbacks");
            static readonly MethodInfo MPFriendlyExchange = AccessTools.Method(typeof(StorageMenuPresenter), "MPFriendlyExchange");
            static readonly MethodInfo Refresh = AccessTools.Method(typeof(StorageMenuPresenter), "Refresh");

            static bool Prefix(RadialMenuPresenterBase<InventoryData, InventoryRadialMenuElement, InventoryRadialMenuViewAdapterBase> __instance, IRadialMenuElement<InventoryData> element)
            {
                if (!Main.Enabled || !Main.Settings.stackTransfer) return true;

                try
                {
                    if (!(__instance is StorageRadialMenuPresenter storageRadialPresenter))
                    {
                        return true;
                    }
                    if (element.SelectedValue == null || element is StorageRadialMenuElement)
                    {
                        return false;
                    }
                    StorageMenuPresenter storagePresenter = Main.storagePresenters[storageRadialPresenter];
                    IPickupable value = element.SelectedValue.Value;
                    IList<IPickupable> pickupables = null;

                    if (_storageRef(storagePresenter) is SlotStorage slotStorage && slotStorage.FindSlot(value) is StorageSlot<IPickupable> storageSlot)
                        pickupables = storageSlot.Objects.ToList();
                    else if (_storageRef(storagePresenter) is PileSlotStorage pileSlotStorage)
                        pickupables = pileSlotStorage.GetStored().ToList();

                    if (pickupables != null)
                    {
                        bool refresh = false;

                        RemoveRefreshCallbacks.Invoke(storagePresenter, new object[] { _storageRef(storagePresenter) });
                        Main.silentSlotStorageTransfer = false;

                        while (pickupables.Count > 0 && storagePresenter.DestinationStorage.CanPush(pickupables[0], !Main.silentSlotStorageTransfer))
                        {
                            MPFriendlyExchange.Invoke(storagePresenter, new object[] { _storageRef(storagePresenter), storagePresenter.DestinationStorage, pickupables[0] });
                            Main.silentSlotStorageTransfer = true;
                            pickupables.RemoveAt(0);
                            refresh = true;
                        }

                        Main.silentSlotStorageTransfer = false;

                        AddRefreshCallbacks.Invoke(storagePresenter, new object[] { _storageRef(storagePresenter) });
                        if (refresh) Refresh.Invoke(storagePresenter, null);
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

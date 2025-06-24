using Beam;
using HarmonyLib;
using System.Collections.Generic;

namespace ToolbeltFix
{
    /// <summary>
    /// Compare StorageSlots by first prioritizing inventory slots, then by quantity, and finally (for hotkey slots only) by descending index.
    /// </summary>
    public class InventoryHotkeyQuantityComparer : IComparer<StorageSlot<IPickupable>>
    {
        private static readonly AccessTools.FieldRef<StorageSlot<IPickupable>, int> _indexRef = AccessTools.FieldRefAccess<StorageSlot<IPickupable>, int>("_index");

        public int Compare(StorageSlot<IPickupable> a, StorageSlot<IPickupable> b)
        {
            int aIndex = _indexRef(a);
            int bIndex = _indexRef(b);

            // Sort by storage type: Inventory (index < 10) before Hotkey (index >= 10)
            int inventoryOrder = (bIndex < 10).CompareTo(aIndex < 10);

            if (inventoryOrder != 0)
                return inventoryOrder;

            // Sort by quantity
            int quantityOrder = StorageSlot<IPickupable>.QuantityComparison.Compare(a, b);

            if (quantityOrder != 0)
                return quantityOrder;

            // For hotkey slots, sort by descending index (higher index first)
            if (aIndex >= 10 && bIndex >= 10)
                return bIndex.CompareTo(aIndex);

            // For inventory slots with same quantity, keep the original order
            return 0;
        }
    }
}

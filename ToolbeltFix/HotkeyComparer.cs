using Beam;
using HarmonyLib;
using System.Collections.Generic;

namespace ToolbeltFix
{
    public class HotkeyComparer : IComparer<StorageSlot<IPickupable>>
    {
        private readonly AccessTools.FieldRef<StorageSlot<IPickupable>, int> _indexRef = AccessTools.FieldRefAccess<StorageSlot<IPickupable>, int>("_index");

        public int Compare(StorageSlot<IPickupable> a, StorageSlot<IPickupable> b)
        {
            int indexA = _indexRef(a) < 10 ? 0 : 20 - _indexRef(a);
            int indexB = _indexRef(b) < 10 ? 0 : 20 - _indexRef(b);

            return indexA - indexB;
        }
    }
}

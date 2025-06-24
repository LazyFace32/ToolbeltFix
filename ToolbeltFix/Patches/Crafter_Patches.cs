using Beam.Crafting;
using Beam;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using Beam.Utilities;

namespace ToolbeltFix.Patches
{
    class Crafter_Patches
    {
        [HarmonyPatch(typeof(Crafter), "GetPlayerCraftingMaterials")]
        class Crafter_GetPlayerCraftingMaterials_Patch
        {
            static readonly AccessTools.FieldRef<Crafter, Dictionary<CraftingType, IList<IBase>>> _cachedMaterialsLookupRef = AccessTools.FieldRefAccess<Crafter, Dictionary<CraftingType, IList<IBase>>>("_cachedMaterialsLookup");
            static readonly AccessTools.FieldRef<Crafter, IPlayer> _playerRef = AccessTools.FieldRefAccess<Crafter, IPlayer>("_player");

            static readonly MethodInfo AddMaterial = AccessTools.Method(typeof(Crafter), "AddMaterial");

            static bool Prefix(Crafter __instance, ref IDictionary<CraftingType, IList<IBase>> __result)
            {
                if (!Main.Enabled) return true;

                try
                {
                    foreach (StorageSlot<IPickupable> storageSlot in _playerRef(__instance).Inventory.GetSlotStorage().GetSlots(Main.InventoryHotkeyQuantityComparison))
                    {
                        foreach (IPickupable obj in storageSlot.Objects)
                        {
                            AddMaterial.Invoke(__instance, new object[] { obj });
                        }
                    }
                    IPickupable currentObject = _playerRef(__instance).Holder.CurrentObject;
                    if (!currentObject.IsNullOrDestroyed())
                    {
                        AddMaterial.Invoke(__instance, new object[] { currentObject });
                    }
                    __result = _cachedMaterialsLookupRef(__instance);
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

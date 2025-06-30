using Beam;
using Beam.Serialization;
using Beam.Serialization.Json;
using Beam.Utilities;
using Funlabs;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ToolbeltFix.Patches
{
    class SlotStorage_Patches
    {
        [HarmonyPatch(typeof(SlotStorage), "CanPush", new Type[] { typeof(IPickupable), typeof(bool), typeof(bool) })]
        class SlotStorage_CanPush_Patch
        {
            static bool Prefix(SlotStorage __instance, IPickupable pickupable, bool force, bool notification, ref bool __result)
            {
                if (!Main.Enabled) return true;

                try
                {
                    if (!__instance.Name.Equals("INVENTORY_MENU_BACKPACK_TITLE")) return true;

                    __result = Main.CanPush(__instance, StorageType.Both, pickupable, force, notification);
                    return false;
                }
                catch (Exception e)
                {
                    Main.Logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SlotStorage), nameof(SlotStorage.Pop))]
        class SlotStorage_Pop_Patch
        {
            static bool Prefix(SlotStorage __instance, IPickupable pickupable)
            {
                if (!Main.Enabled) return true;

                try
                {
                    if (!__instance.Name.Equals("INVENTORY_MENU_BACKPACK_TITLE")) return true;

                    Main.Pop(__instance, pickupable, true, true);
                    return false;
                }
                catch (Exception e)
                {
                    Main.Logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SlotStorage), nameof(SlotStorage.PopNext))]
        class SlotStorage_PopNext_Patch
        {
            static readonly AccessTools.FieldRef<SlotStorage, List<StorageSlot<IPickupable>>> _slotDataRef = AccessTools.FieldRefAccess<SlotStorage, List<StorageSlot<IPickupable>>>("_slotData");
            static readonly AccessTools.FieldRef<SlotStorage, StorageSlot<IPickupable>> _selectedSlotRef = AccessTools.FieldRefAccess<SlotStorage, StorageSlot<IPickupable>>("_selectedSlot");

            static readonly MethodInfo OnSelectionChanged = AccessTools.Method(typeof(SlotStorage), "OnSelectionChanged");

            static readonly AccessTools.FieldRef<StorageSlot<IPickupable>, int> _indexRef = AccessTools.FieldRefAccess<StorageSlot<IPickupable>, int>("_index");

            static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");

            static bool Prefix(SlotStorage __instance, IPickupable pickupable)
            {
                if (!Main.Enabled) return true;

                try
                {
                    if (!__instance.Name.Equals("INVENTORY_MENU_BACKPACK_TITLE")) return true;

                    HotkeyData pickupableHotkeyData = null;

                    if (Main.slotStorage_HotkeyController.TryGetValue(__instance, out HotkeyController hotkeyController))
                    {
                        for (int i = 10; i < _slotDataRef(__instance).Count; i++)
                        {
                            HotkeyData hotkeyData = _hotkeysRef(hotkeyController)[i - 10];

                            if (Main._occupied[hotkeyData].Contains(pickupable.ReferenceId))
                            {
                                Main._occupied[hotkeyData].Remove(pickupable.ReferenceId);

                                Main._quantity[hotkeyData].Value = Main._occupied[hotkeyData].Count;

                                if (Main._occupied[hotkeyData].Count == 0)
                                {
                                    pickupableHotkeyData = hotkeyData;
                                }

                                break;
                            }
                        }
                    }

                    if (_selectedSlotRef(__instance) == null || _selectedSlotRef(__instance).Objects.Count == 0)
                    {
                        _selectedSlotRef(__instance) = __instance.FindSlot(pickupable.CraftingType);

                        if (_selectedSlotRef(__instance) == null)
                        {
                            _selectedSlotRef(__instance) = __instance.FindSlot(pickupable.CraftingType.InteractiveType, AttributeType.None);
                        }

                        if ((_selectedSlotRef(__instance) == null || _indexRef(_selectedSlotRef(__instance)) > 9) && pickupableHotkeyData != null)
                        {
                            if (Main.Settings.rememberToolbelt)
                            {
                                Main._rememberHotkey[pickupableHotkeyData].Value = true;
                            }
                            else
                            {
                                Main.ClearHotkey(pickupableHotkeyData);
                            }
                        }
                    }
                    IPickupable pickupable2 = null;
                    StorageSlot<IPickupable> selectedSlot = _selectedSlotRef(__instance);
                    if (selectedSlot != null && selectedSlot.Objects.Count > 0)
                    {
                        pickupable2 = _selectedSlotRef(__instance).Objects[0];
                    }
                    OnSelectionChanged.Invoke(__instance, new object[] { pickupable2 });
                    return false;
                }
                catch (Exception e)
                {
                    Main.Logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SlotStorage), nameof(SlotStorage.Push), new Type[] { typeof(IPickupable), typeof(bool) })]
        class SlotStorage_Push_Patch
        {
            static bool Prefix(SlotStorage __instance, IPickupable pickupable, bool force, ref bool __result)
            {
                if (!Main.Enabled) return true;

                try
                {
                    if (!__instance.Name.Equals("INVENTORY_MENU_BACKPACK_TITLE")) return true;

                    __result = Main.Push(__instance, StorageType.Both, pickupable, force);
                    return false;
                }
                catch (Exception e)
                {
                    Main.Logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SlotStorage), nameof(SlotStorage.SortSlots))]
        class SlotStorage_SortSlots_Patch
        {
            static readonly AccessTools.FieldRef<SlotStorage, List<StorageSlot<IPickupable>>> _slotDataRef = AccessTools.FieldRefAccess<SlotStorage, List<StorageSlot<IPickupable>>>("_slotData");
            static readonly AccessTools.FieldRef<SlotStorage, bool> _sortRef = AccessTools.FieldRefAccess<SlotStorage, bool>("_sort");

            static bool Prefix(SlotStorage __instance)
            {
                if (!Main.Enabled) return true;

                try
                {
                    if (!__instance.Name.Equals("INVENTORY_MENU_BACKPACK_TITLE")) return true;

                    if (!_sortRef(__instance))
                    {
                        return false;
                    }
                    for (int i = 0; i < 10; i++)
                    {
                        if (_slotDataRef(__instance)[i].Objects.Count == 0 && i + 1 < 10)
                        {
                            StorageSlot<IPickupable> value = _slotDataRef(__instance)[i];
                            _slotDataRef(__instance)[i] = _slotDataRef(__instance)[i + 1];
                            _slotDataRef(__instance)[i + 1] = value;
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

#if DEBUG
        [HarmonyPatch(typeof(SlotStorage), "OrderedPush")]
        class SlotStorage_OrderedPush_Patch
        {
            static readonly AccessTools.FieldRef<SlotStorage, Transform> _storageContainerRef = AccessTools.FieldRefAccess<SlotStorage, Transform>("_storageContainer");
            static readonly AccessTools.FieldRef<SlotStorage, LoadState> _loadStateRef = AccessTools.FieldRefAccess<SlotStorage, LoadState>("_loadState");

            static bool Prefix(SlotStorage __instance, List<IPickupable> items, JObject slotsData)
            {
                if (!Main.Enabled) return true;

                try
                {
                    if (!__instance.Name.Equals("INVENTORY_MENU_BACKPACK_TITLE")) return true;

                    IDynamicParentProvider _dynamicParenter = UnityEngine.Object.FindObjectOfType<StrandedWorld>();
                    int numfailedLoadedPickupables = 0;

                    for (int i = 0; i < slotsData.Children.Count; i++)
                    {
                        JObject jobject = slotsData.Children[i];
                        if (jobject.IsValid() && !jobject.IsNull())
                        {
                            for (int j = 0; j < jobject.Children.Count; j++)
                            {
                                IPickupable pickupable = items.FirstOrDefault_NonAlloc((IPickupable itm, MiniGuid referenceId) => itm.ReferenceId.Equals(referenceId), Prefabs.GetReferenceId(jobject.Children[j], true));
                                if (pickupable.IsNullOrDestroyed())
                                {
                                    Debug.LogError(string.Format("Slot Storage {0} Load - Unable to find reference item {1}", __instance.ReferenceId, Prefabs.GetReferenceId(jobject.Children[j], true)));
                                }
                                if (!__instance.Push(pickupable))
                                {
                                    Main.Logger.Warning("Could not push " + pickupable + " to storage. Not enough room.");

                                    if (Main.Settings.dropOverflowItems)
                                    {
                                        pickupable.Release();
                                        pickupable.transform.parent = null;
                                        pickupable.TransformParent = _dynamicParenter?.GetParent();
                                        pickupable.transform.position = new Vector3(_storageContainerRef(__instance).position.x, _storageContainerRef(__instance).position.y + numfailedLoadedPickupables * 1.5f, _storageContainerRef(__instance).position.z);
                                        pickupable.Parent();
                                        pickupable.Hold(false);
                                        pickupable.SetOwner(null);
                                    }

                                    numfailedLoadedPickupables++;
                                }
                            }
                        }
                    }
                    _loadStateRef(__instance) = LoadState.Loaded;

                    return false;
                }
                catch (Exception e)
                {
                    Main.Logger.LogException(e);
                    return true;
                }
            }
        }
#endif
    }
}

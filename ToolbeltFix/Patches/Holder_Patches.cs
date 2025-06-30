using Beam.Crafting;
using Beam;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using Beam.Utilities;

namespace ToolbeltFix.Patches
{
    class Holder_Patches
    {
        [HarmonyPatch(typeof(Holder), nameof(Holder.Select))]
        class Holder_Select_Patch
        {
            static readonly AccessTools.FieldRef<Holder, ISlotStorage<IPickupable>> _storageRef = AccessTools.FieldRefAccess<Holder, ISlotStorage<IPickupable>>("_storage");
            static readonly AccessTools.FieldRef<Holder, IGameInputController> _controllerRef = AccessTools.FieldRefAccess<Holder, IGameInputController>("_controller");
            static readonly AccessTools.FieldRef<Holder, IPickupable> _currentlyPickingRef = AccessTools.FieldRefAccess<Holder, IPickupable>("_currentlyPicking");
            static readonly AccessTools.FieldRef<Holder, Transform> _holdingContainerRef = AccessTools.FieldRefAccess<Holder, Transform>("_holdingContainer");
            static readonly AccessTools.FieldRef<Holder, IPickupable> _currentObjectRef = AccessTools.FieldRefAccess<Holder, IPickupable>("_currentObject");
            static readonly AccessTools.FieldRef<Holder, IPlayer> _playerRef = AccessTools.FieldRefAccess<Holder, IPlayer>("_player");

            static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");

            static readonly AccessTools.FieldRef<StorageSlot<IPickupable>, int> _indexRef = AccessTools.FieldRefAccess<StorageSlot<IPickupable>, int>("_index");

            static readonly MethodInfo HidePickupable = AccessTools.Method(typeof(Holder), "HidePickupable");
            static readonly MethodInfo ShowPickupable = AccessTools.Method(typeof(Holder), "ShowPickupable");
            static readonly MethodInfo Reload = AccessTools.Method(typeof(Holder), "Reload");

            static readonly MethodInfo GetStackSize = AccessTools.Method(typeof(SlotStorage), "GetStackSize");

            static bool Prefix(Holder __instance, IPickupable pickupable, bool __result)
            {
                if (!Main.Enabled) return true;

                try
                {
                    if (pickupable == null)
                    {
                        __result = false;
                        return false;
                    }
                    if (pickupable == _currentObjectRef(__instance))
                    {
                        __result = true;
                        return false;
                    }
                    SlotStorage storage = _storageRef(__instance) as SlotStorage;

                    Main.Pop(storage, pickupable, true, false);
                    if (!_currentObjectRef(__instance).IsNullOrDestroyed() && !storage.Push(_currentObjectRef(__instance)))
                    {
                        storage.Push(pickupable);
                        __result = false;
                        return false;
                    }
                    if (_playerRef(__instance).Movement.ClimbingLadder)
                    {
                        __instance.ShowCurrent();
                        HidePickupable.Invoke(__instance, new object[] { pickupable });
                    }
                    else
                    {
                        ShowPickupable.Invoke(__instance, new object[] { pickupable });
                    }
                    if (Main.CanPush(storage, StorageType.Hotkeys, pickupable, false, false))
                    {
                        StorageSlot<IPickupable> slot = Main.GetSlot(storage, StorageType.Hotkeys, pickupable, false);

                        if (slot != null && Main.slotStorage_HotkeyController.TryGetValue(storage, out HotkeyController hotkeyController))
                        {
                            HotkeyData hotkeyData = _hotkeysRef(hotkeyController)[_indexRef(slot) - 10];
                            CraftingType craftingType = hotkeyData.CraftingType.Value;

                            Main._occupied[hotkeyData].Add(pickupable.ReferenceId);

                            Main._reserved[hotkeyData].Remove(pickupable.ReferenceId);
                            Main._reserved[hotkeyData].AddLast(pickupable.ReferenceId);

                            while (Main._reserved[hotkeyData].Count > (int)GetStackSize.Invoke(storage, new object[] { craftingType }))
                            {
                                Main._reserved[hotkeyData].RemoveFirst();
                            }

                            Main._quantity[hotkeyData].Value = Main._occupied[hotkeyData].Count;
                            Main._rememberHotkey[hotkeyData].Value = false;
                        }
                    }

                    _currentObjectRef(__instance) = pickupable;
                    _currentObjectRef(__instance).SetOwner(_playerRef(__instance));
                    _currentObjectRef(__instance).Store();
                    _currentObjectRef(__instance).transform.CheckForProjectilesInChildren();
                    _currentObjectRef(__instance).transform.parent = null;
                    _currentObjectRef(__instance).TransformParent = _holdingContainerRef(__instance);
                    _currentObjectRef(__instance).Parent();
                    _currentObjectRef(__instance).transform.localRotation = Quaternion.identity;
                    _currentObjectRef(__instance).transform.localPosition = Vector3.zero;
                    _currentObjectRef(__instance).transform.localScale = Vector3.one;
                    _currentObjectRef(__instance).Hold(true);
                    _playerRef(__instance).Character.Hold(_currentObjectRef(__instance));
                    Reload.Invoke(__instance, new object[] { null, true });
                    _controllerRef(__instance).Raycaster.Reset();
                    __result = true;
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

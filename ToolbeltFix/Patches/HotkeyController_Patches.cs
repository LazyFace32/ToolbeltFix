using Beam.Crafting;
using Beam.Events;
using Beam.Serialization.Json;
using Beam.UI;
using Beam.Utilities;
using Beam;
using Funlabs;
using HarmonyLib;
using MEC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;

namespace ToolbeltFix.Patches
{
    class HotkeyController_Patches
    {
#if DEBUG
        [SaveOnReload]
        public static readonly Dictionary<HotkeyController, Action<QuickAccessSlotUnlockedEvent>> EventManager_QuickAccessSlotUnlocked_Events = new Dictionary<HotkeyController, Action<QuickAccessSlotUnlockedEvent>>();
        [SaveOnReload]
        public static readonly Dictionary<HotkeyController, Action<OptionsAppliedEvent>> EventManager_OptionsApplied_Events = new Dictionary<HotkeyController, Action<OptionsAppliedEvent>>();
#else
        static readonly Dictionary<HotkeyController, Action<QuickAccessSlotUnlockedEvent>> EventManager_QuickAccessSlotUnlocked_Events = new Dictionary<HotkeyController, Action<QuickAccessSlotUnlockedEvent>>();
        static readonly Dictionary<HotkeyController, Action<OptionsAppliedEvent>> EventManager_OptionsApplied_Events = new Dictionary<HotkeyController, Action<OptionsAppliedEvent>>();
#endif

        [HarmonyPatch(typeof(HotkeyController), "Awake")]
        class HotkeyController_Awake_Patch
        {
            static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");

            static bool Prefix(HotkeyController __instance)
            {
                if (!Main.Enabled) return true;

                try
                {
                    _hotkeysRef(__instance) = new HotkeyData[10];

                    for (int i = 0; i < 10; i++)
                    {
                        int num = i + 1;
                        if (num == 10)
                        {
                            num = 0;
                        }
                        _hotkeysRef(__instance)[i] = new HotkeyData
                        {
                            Number = num
                        };

                        Main._occupied[_hotkeysRef(__instance)[i]] = new HashSet<MiniGuid>();
                        Main._reserved[_hotkeysRef(__instance)[i]] = new LinkedList<MiniGuid>();
                        Main._rememberHotkey[_hotkeysRef(__instance)[i]] = new Property<bool>(false);
                        Main._quantity[_hotkeysRef(__instance)[i]] = new Property<int>(0);
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

        [HarmonyPatch(typeof(HotkeyController), nameof(HotkeyController.SetPlayer))]
        class HotkeyController_SetPlayer_Patch
        {
            static readonly AccessTools.FieldRef<HotkeyController, InventoryRadialMenuPresenter> _inventoryRadialMenuRef = AccessTools.FieldRefAccess<HotkeyController, InventoryRadialMenuPresenter>("_inventoryRadialMenu");
            static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");
            static readonly AccessTools.FieldRef<HotkeyController, HotkeyView> _viewRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyView>("_view");
            static readonly AccessTools.FieldRef<HotkeyController, IPlayer> _playerRef = AccessTools.FieldRefAccess<HotkeyController, IPlayer>("_player");

            static readonly MethodInfo EventManager_QuickAccessSlotUnlocked = AccessTools.Method(typeof(HotkeyController), "EventManager_QuickAccessSlotUnlocked");
            static readonly MethodInfo EventManager_OptionsApplied = AccessTools.Method(typeof(HotkeyController), "EventManager_OptionsApplied");
            static readonly MethodInfo SubscribeToInputEvents = AccessTools.Method(typeof(HotkeyController), "SubscribeToInputEvents");
            static readonly MethodInfo Holder_Dropped = AccessTools.Method(typeof(HotkeyController), "Holder_Dropped");
            static readonly MethodInfo LoadOptions = AccessTools.Method(typeof(HotkeyController), "LoadOptions");

            static readonly MethodInfo InventoryRadialMenuElement_View = AccessTools.PropertyGetter(typeof(InventoryRadialMenuElement), "View");

            static bool Prefix(HotkeyController __instance, IPlayer player)
            {
                if (!Main.Enabled) return true;

                try
                {
                    _playerRef(__instance) = player;
                    Main.slotStorage_HotkeyController[_playerRef(__instance).Inventory.GetSlotStorage()] = __instance;
                    PlayerUI playerUI = _playerRef(__instance).PlayerUI;
                    _viewRef(__instance) = playerUI?.Canvas.GetComponentInChildren<HotkeyView>();
                    if (_viewRef(__instance) == null)
                    {
                        if (_playerRef(__instance).IsOwner)
                        {
                            Debug.LogError(string.Format("HotkeyController:: No view component found! for player with id {0}", _playerRef(__instance).Id));
                        }
                        return false;
                    }

                    _inventoryRadialMenuRef(__instance) = _playerRef(__instance).PlayerUI.GetController<InventoryRadialMenuPresenter>();

                    InventoryRadialMenuElement inventoryRadialMenuElement = _inventoryRadialMenuRef(__instance).View.ElementPrefab;
                    UInventoryRadialMenuElementViewAdapter inventoryRadialMenuElementView = InventoryRadialMenuElement_View.Invoke(inventoryRadialMenuElement, null) as UInventoryRadialMenuElementViewAdapter;
                    Main._quantityGroupPrefab = inventoryRadialMenuElementView.QuantityGroup;

                    _viewRef(__instance).Initialize(_hotkeysRef(__instance));
                    _playerRef(__instance).Holder.Dropped += Main.CreateMethodDelegate<Action<IPickupable>>(Holder_Dropped, __instance);
                    SubscribeToInputEvents.Invoke(__instance, new object[] { _playerRef(__instance).Input });

                    EventManager_QuickAccessSlotUnlocked_Events[__instance] = Main.CreateMethodDelegate<Action<QuickAccessSlotUnlockedEvent>>(EventManager_QuickAccessSlotUnlocked, __instance);
                    EventManager.AddListener(new EventManager.EventDelegate<QuickAccessSlotUnlockedEvent>(EventManager_QuickAccessSlotUnlocked_Events[__instance]));

                    EventManager_OptionsApplied_Events[__instance] = Main.CreateMethodDelegate<Action<OptionsAppliedEvent>>(EventManager_OptionsApplied, __instance);
                    EventManager.AddListener(new EventManager.EventDelegate<OptionsAppliedEvent>(EventManager_OptionsApplied_Events[__instance]));

                    LoadOptions.Invoke(__instance, null);
                    return false;
                }
                catch (Exception e)
                {
                    Main.Logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(HotkeyController), "OnDestroy")]
        class HotkeyController_OnDestroy_Patch
        {
            static readonly AccessTools.FieldRef<HotkeyController, InventoryRadialMenuPresenter> _inventoryRadialMenuRef = AccessTools.FieldRefAccess<HotkeyController, InventoryRadialMenuPresenter>("_inventoryRadialMenu");
            static readonly AccessTools.FieldRef<HotkeyController, bool> _subbedToInventoryEventsRef = AccessTools.FieldRefAccess<HotkeyController, bool>("_subbedToInventoryEvents");
            static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");
            static readonly AccessTools.FieldRef<HotkeyController, HotkeyView> _viewRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyView>("_view");
            static readonly AccessTools.FieldRef<HotkeyController, IPlayer> _playerRef = AccessTools.FieldRefAccess<HotkeyController, IPlayer>("_player");

            static readonly AccessTools.FieldRef<PlatformHotkeyViewProvider, HotkeyView> _kbViewRef = AccessTools.FieldRefAccess<PlatformHotkeyViewProvider, HotkeyView>("_kbView");
            static readonly AccessTools.FieldRef<PlatformHotkeyViewProvider, HotkeyView> _dpViewRef = AccessTools.FieldRefAccess<PlatformHotkeyViewProvider, HotkeyView>("_dpView");

            static readonly AccessTools.FieldRef<UHotkeyView, IList<HotkeyElementView>> _elementsRef = AccessTools.FieldRefAccess<UHotkeyView, IList<HotkeyElementView>>("_elements");

            static readonly MethodInfo UnsubscribeFromInputEvents = AccessTools.Method(typeof(HotkeyController), "UnsubscribeFromInputEvents");
            static readonly MethodInfo Holder_Dropped = AccessTools.Method(typeof(HotkeyController), "Holder_Dropped");

            static bool Prefix(HotkeyController __instance)
            {
                if (!Main.Enabled) return true;

                try
                {
                    foreach (HotkeyData hotkeyData in _hotkeysRef(__instance))
                    {
                        hotkeyData.Locked.ClearSubscribers();
                        hotkeyData.CraftingType.ClearSubscribers();
                        Main._rememberHotkey[hotkeyData].ClearSubscribers();
                        Main._quantity[hotkeyData].ClearSubscribers();

                        Main._occupied.Remove(hotkeyData);
                        Main._reserved.Remove(hotkeyData);
                        Main._rememberHotkey.Remove(hotkeyData);
                        Main._quantity.Remove(hotkeyData);
                    }

                    if (_viewRef(__instance) != null)
                    {
                        foreach (HotkeyElementView hotkeyElementView in _elementsRef(_kbViewRef(_viewRef(__instance) as PlatformHotkeyViewProvider) as UHotkeyView))
                        {
                            Main._quantityGroup.Remove(hotkeyElementView);
                            Main._quantityLabel.Remove(hotkeyElementView);
                        }

                        foreach (HotkeyElementView hotkeyElementView in _elementsRef(_dpViewRef(_viewRef(__instance) as PlatformHotkeyViewProvider) as UHotkeyView))
                        {
                            Main._quantityGroup.Remove(hotkeyElementView);
                            Main._quantityLabel.Remove(hotkeyElementView);
                        }
                    }
                    else if (!_playerRef(__instance).IsNullOrDestroyed() && _playerRef(__instance).Peer.IsLocalPeer())
                    {
                        Main._quantityGroup.Clear();
                        Main._quantityLabel.Clear();
                    }

                    if (!_playerRef(__instance).IsNullOrDestroyed())
                    {
                        if (_playerRef(__instance).Holder != null)
                        {
                            _playerRef(__instance).Holder.Dropped -= Main.CreateMethodDelegate<Action<IPickupable>>(Holder_Dropped, __instance);
                        }

                        Main.slotStorage_HotkeyController.Remove(_playerRef(__instance).Inventory.GetSlotStorage());
                    }

                    if (_subbedToInventoryEventsRef(__instance) && _inventoryRadialMenuRef(__instance) != null)
                    {
                        _inventoryRadialMenuRef(__instance).View.ShowView -= __instance.Show;
                        _inventoryRadialMenuRef(__instance).View.HideView -= __instance.Hide;
                    }
                    IPlayer player = _playerRef(__instance);
                    UnsubscribeFromInputEvents.Invoke(__instance, new object[] { player?.Input });

                    if (EventManager_QuickAccessSlotUnlocked_Events.TryGetValue(__instance, out Action<QuickAccessSlotUnlockedEvent> quickAccesSlotUnlockedEvent))
                    {
                        EventManager.RemoveListener(new EventManager.EventDelegate<QuickAccessSlotUnlockedEvent>(quickAccesSlotUnlockedEvent));
                        EventManager_QuickAccessSlotUnlocked_Events.Remove(__instance);
                    }

                    if (EventManager_OptionsApplied_Events.TryGetValue(__instance, out Action<OptionsAppliedEvent> optionsAppliedEvent))
                    {
                        EventManager.RemoveListener(new EventManager.EventDelegate<OptionsAppliedEvent>(optionsAppliedEvent));
                        EventManager_OptionsApplied_Events.Remove(__instance);
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

        [HarmonyPatch(typeof(HotkeyController), "LoadOptions")]
        class HotkeyController_LoadOptions_Patch
        {
            static readonly AccessTools.FieldRef<HotkeyController, InventoryRadialMenuPresenter> _inventoryRadialMenuRef = AccessTools.FieldRefAccess<HotkeyController, InventoryRadialMenuPresenter>("_inventoryRadialMenu");
            static readonly AccessTools.FieldRef<HotkeyController, bool> _subbedToInventoryEventsRef = AccessTools.FieldRefAccess<HotkeyController, bool>("_subbedToInventoryEvents");
            static readonly AccessTools.FieldRef<HotkeyController, HotkeyView> _viewRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyView>("_view");

            static readonly AccessTools.FieldRef<PlatformHotkeyViewProvider, HotkeyView> platform_viewRef = AccessTools.FieldRefAccess<PlatformHotkeyViewProvider, HotkeyView>("_view");

            static readonly MethodInfo GetView = AccessTools.Method(typeof(PlatformHotkeyViewProvider), "GetView");

            static bool Prefix(HotkeyController __instance)
            {
                if (!Main.Enabled) return true;

                try
                {
                    if (Options.GeneralSettings.AlwaysShowHotkeyBar)
                    {
                        HotkeyView currentPlatformHotkeyView = platform_viewRef(_viewRef(__instance) as PlatformHotkeyViewProvider);

                        if (currentPlatformHotkeyView != (HotkeyView)GetView.Invoke(_viewRef(__instance) as PlatformHotkeyViewProvider, null))
                        {
                            _viewRef(__instance).Hide();
                        }

                        if (_subbedToInventoryEventsRef(__instance))
                        {
                            _inventoryRadialMenuRef(__instance).View.ShowView -= __instance.Show;
                            _inventoryRadialMenuRef(__instance).View.HideView -= __instance.Hide;
                            _subbedToInventoryEventsRef(__instance) = false;
                        }

                        if (currentPlatformHotkeyView == null || !currentPlatformHotkeyView.Visible)
                        {
                            _viewRef(__instance).Show();
                        }
                        return false;
                    }
                    if (!_subbedToInventoryEventsRef(__instance))
                    {
                        _inventoryRadialMenuRef(__instance).View.ShowView += __instance.Show;
                        _inventoryRadialMenuRef(__instance).View.HideView += __instance.Hide;
                        _subbedToInventoryEventsRef(__instance) = true;
                    }
                    _viewRef(__instance).Hide();
                    return false;
                }
                catch (Exception e)
                {
                    Main.Logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(HotkeyController), "Holder_Dropped")]
        class HotkeyController_Holder_Dropped_Patch
        {
            static bool Prefix()
            {
                if (!Main.Enabled) return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(HotkeyController), "DoHotkey")]
        class HotkeyController_DoHotkey_Patch
        {
            static readonly AccessTools.FieldRef<HotkeyController, InventoryRadialMenuPresenter> _inventoryRadialMenuRef = AccessTools.FieldRefAccess<HotkeyController, InventoryRadialMenuPresenter>("_inventoryRadialMenu");
            static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");
            static readonly AccessTools.FieldRef<HotkeyController, HotkeyView> _viewRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyView>("_view");
            static readonly AccessTools.FieldRef<HotkeyController, IPlayer> _playerRef = AccessTools.FieldRefAccess<HotkeyController, IPlayer>("_player");

            static readonly AccessTools.FieldRef<SlotStorage, List<StorageSlot<IPickupable>>> _slotDataRef = AccessTools.FieldRefAccess<SlotStorage, List<StorageSlot<IPickupable>>>("_slotData");

            static readonly MethodInfo GetInventoryData = AccessTools.Method(typeof(InventoryMenuPresenter), "GetInventoryData");
            static readonly MethodInfo BlockHotkeyInput = AccessTools.Method(typeof(HotkeyController), "BlockHotkeyInput");

            static bool Prefix(HotkeyController __instance, int number)
            {
                if (!Main.Enabled) return true;

                try
                {
                    if ((bool)BlockHotkeyInput.Invoke(__instance, null))
                    {
                        return false;
                    }

                    IPickupable currentObject = _playerRef(__instance).Holder.CurrentObject;
                    HotkeyData hotkeyData = _hotkeysRef(__instance)[number];

                    if (Main._reserved[hotkeyData].Count == 0)
                    {
                        return false;
                    }

                    SlotStorage storage = _playerRef(__instance).Inventory.GetSlotStorage() as SlotStorage;
                    StorageSlot<IPickupable> storageSlot = _slotDataRef(storage)[10 + number];
                    IPickupable pickupable = storageSlot.Objects.FirstOrDefault();

                    if (!pickupable.IsNullOrDestroyed() && (currentObject.IsNullOrDestroyed() || storage.CanPush(currentObject)))
                    {
                        storage.ReplicatedSelect(pickupable);

                        IList<IList<InventoryData>> inventoryData = GetInventoryData.Invoke(_inventoryRadialMenuRef(__instance).Inventory, null) as IList<IList<InventoryData>>;
                        _inventoryRadialMenuRef(__instance).Initialize(inventoryData);
                        _inventoryRadialMenuRef(__instance).Refresh();
                        return false;
                    }
                    if (!currentObject.IsNullOrDestroyed() && Main._reserved[hotkeyData].Contains(currentObject.ReferenceId))
                    {
                        return false;
                    }
                    _viewRef(__instance).OnAssignmentFailed();
                    return false;
                }
                catch (Exception e)
                {
                    Main.Logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(HotkeyController), "DoHotkeyAssignment")]
        class HotkeyController_DoHotkeyAssignment_Patch
        {
            static readonly AccessTools.FieldRef<HotkeyController, InventoryRadialMenuPresenter> _inventoryRadialMenuRef = AccessTools.FieldRefAccess<HotkeyController, InventoryRadialMenuPresenter>("_inventoryRadialMenu");
            static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");
            static readonly AccessTools.FieldRef<HotkeyController, HotkeyView> _viewRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyView>("_view");
            static readonly AccessTools.FieldRef<HotkeyController, IPlayer> _playerRef = AccessTools.FieldRefAccess<HotkeyController, IPlayer>("_player");

            static readonly AccessTools.FieldRef<SlotStorage, List<StorageSlot<IPickupable>>> _slotDataRef = AccessTools.FieldRefAccess<SlotStorage, List<StorageSlot<IPickupable>>>("_slotData");

            static readonly MethodInfo BlockHotkeyInput = AccessTools.Method(typeof(HotkeyController), "BlockHotkeyInput");

            static readonly MethodInfo GetInventoryData = AccessTools.Method(typeof(InventoryMenuPresenter), "GetInventoryData");

            static bool Prefix(HotkeyController __instance, int number, IPickupable obj)
            {
                if (!Main.Enabled) return true;

                try
                {
                    if ((bool)BlockHotkeyInput.Invoke(__instance, null))
                    {
                        return false;
                    }

                    SlotStorage storage = _playerRef(__instance).Inventory.GetSlotStorage() as SlotStorage;
                    HotkeyData hotkeyData = _hotkeysRef(__instance)[number];

                    if (obj.IsNullOrDestroyed() && Main._reserved[hotkeyData].Count == 0)
                    {
                        _viewRef(__instance)?.OnAssignmentFailed();
                        return false;
                    }

                    if (Main._reserved[hotkeyData].Count > 0)
                    {
                        if (obj.IsNullOrDestroyed() || hotkeyData.CraftingType.Value.Equals(obj.CraftingType))
                        {
                            if (ClearHotkeySlot(__instance, number, false))
                            {
                                _viewRef(__instance)?.OnAssignmentRemoved();
                                return false;
                            }
                        }
                        else
                        {
                            // Should the old items be moved to the inventory or switch hotkey slot with the new items?

                            if (ReplaceHotkeySlot(__instance, number, obj))
                            {
                                _viewRef(__instance)?.OnAssignmentCreated();
                                return false;
                            }
                        }
                    }
                    else
                    {
                        AddHotkeySlot(__instance, number, obj);
                        _viewRef(__instance)?.OnAssignmentCreated();
                        return false;
                    }

                    _viewRef(__instance)?.OnAssignmentFailed();
                    return false;
                }
                catch (Exception e)
                {
                    Main.Logger.LogException(e);
                    return true;
                }
            }

            static bool ClearHotkeySlot(HotkeyController __instance, int number, bool abortOnFail = true)
            {
                SlotStorage storage = _playerRef(__instance).Inventory.GetSlotStorage() as SlotStorage;
                StorageSlot<IPickupable> hotkeySlot = _slotDataRef(storage)[number + 10];
                HotkeyData hotkeyData = _hotkeysRef(__instance)[number];

                if (hotkeySlot != null)
                {
                    IList<IPickupable> transferedPickupables = new List<IPickupable>();
                    bool refresh = false;
                    bool success = true;

                    Main.silentSlotStorageTransfer = true;

                    while (hotkeySlot.Objects.Count > 0)
                    {
                        IPickupable pickupable = hotkeySlot.Objects[0];

                        if (!Main.CanPush(storage, StorageType.Inventory, pickupable, false))
                        {
                            success = false;
                            break;
                        }

                        Main.Pop(storage, pickupable, false, false);
                        Main.Push(storage, StorageType.Inventory, pickupable, false);
                        transferedPickupables.Add(pickupable);
                        refresh = true;
                    }

                    if (!success && abortOnFail)
                    {
                        for (int i = transferedPickupables.Count - 1; i >= 0; i--)
                        {
                            IPickupable pickupable = transferedPickupables[i];

                            Main.Pop(storage, pickupable, false, false);
                            Main.Push(storage, StorageType.Hotkeys, pickupable, false);

                            hotkeySlot.Objects.Remove(pickupable);
                            hotkeySlot.Objects.Insert(0, pickupable);

                            Main._reserved[hotkeyData].Remove(pickupable.ReferenceId);
                            Main._reserved[hotkeyData].AddFirst(pickupable.ReferenceId);
                        }
                    }
                    else if (refresh && _inventoryRadialMenuRef(__instance) != null)
                    {
                        IList<IList<InventoryData>> inventoryData = GetInventoryData.Invoke(_inventoryRadialMenuRef(__instance).Inventory, null) as IList<IList<InventoryData>>;
                        _inventoryRadialMenuRef(__instance).Initialize(inventoryData);
                        _inventoryRadialMenuRef(__instance).Refresh();
                    }

                    Main.silentSlotStorageTransfer = false;

                    if (success)
                    {
                        Main.ClearHotkey(hotkeyData);
                        return true;
                    }
                }

                return false;
            }

            static bool ReplaceHotkeySlot(HotkeyController __instance, int number, IPickupable obj)
            {
                SlotStorage storage = _playerRef(__instance).Inventory.GetSlotStorage() as SlotStorage;
                HotkeyData hotkeyData = _hotkeysRef(__instance)[number];
                List<IPickupable> pickupables = new List<IPickupable>();
                bool transferPickupables = true;

                HotkeyData existingHotkeyData = null;

                if (obj.Equals(_playerRef(__instance).Holder.CurrentObject))
                {
                    for (int i = 0; i < _hotkeysRef(__instance).Length; i++)
                    {
                        HotkeyData data = _hotkeysRef(__instance)[i];

                        if (Main._occupied[data].Contains(obj.ReferenceId))
                        {
                            existingHotkeyData = data;
                            break;
                        }
                    }

                    transferPickupables = false;
                    pickupables.Add(obj);
                }
                else
                {
                    StorageSlot<IPickupable> storageSlot = storage.FindSlot(obj);

                    if (storageSlot != null)
                    {
                        pickupables.AddRange(storageSlot.Objects.Take(Main.GetHotkeyStackSize(storage, storageSlot.CraftingType)));

                        Main.silentSlotStorageTransfer = true;

                        foreach (IPickupable pickupable in pickupables)
                        {
                            Main.Pop(storage, pickupable, false, false);
                        }

                        Main.silentSlotStorageTransfer = false;
                    }
                }

                if (ClearHotkeySlot(__instance, number))
                {
                    if (existingHotkeyData != null)
                    {
                        Main._occupied[existingHotkeyData].Remove(obj.ReferenceId);
                        Main._reserved[existingHotkeyData].Remove(obj.ReferenceId);

                        Main._quantity[existingHotkeyData].Value = Main._occupied[existingHotkeyData].Count;

                        if (Main._occupied[existingHotkeyData].Count == 0)
                        {
                            if (Main._reserved[existingHotkeyData].Count > 0 && Main.Settings.rememberToolbelt)
                            {
                                Main._rememberHotkey[hotkeyData].Value = true;
                            }
                            else
                            {
                                Main.ClearHotkey(existingHotkeyData);
                            }
                        }
                    }

                    Main.silentSlotStorageTransfer = true;

                    foreach (IPickupable pickupable in pickupables)
                    {
                        Main._occupied[hotkeyData].Add(pickupable.ReferenceId);
                        Main._reserved[hotkeyData].AddLast(pickupable.ReferenceId);

                        if (transferPickupables)
                        {
                            Main.Push(storage, StorageType.Hotkeys, pickupable, false);
                        }
                    }

                    Main.silentSlotStorageTransfer = false;

                    if (transferPickupables && _inventoryRadialMenuRef(__instance) != null)
                    {
                        IList<IList<InventoryData>> inventoryData = GetInventoryData.Invoke(_inventoryRadialMenuRef(__instance).Inventory, null) as IList<IList<InventoryData>>;
                        _inventoryRadialMenuRef(__instance).Initialize(inventoryData);
                        _inventoryRadialMenuRef(__instance).Refresh();

                        storage.SortSlots();
                    }

                    Main._quantity[hotkeyData].Value = Main._occupied[hotkeyData].Count;
                    hotkeyData.CraftingType.Value = obj.CraftingType;
                    return true;
                }
                else
                {
                    if (transferPickupables)
                    {
                        Main.silentSlotStorageTransfer = true;

                        foreach (IPickupable pickupable in pickupables)
                        {
                            Main.Push(storage, StorageType.Inventory, pickupable, false);
                        }

                        Main.silentSlotStorageTransfer = false;
                    }

                    return false;
                }
            }

            static void AddHotkeySlot(HotkeyController __instance, int number, IPickupable obj)
            {
                SlotStorage storage = _playerRef(__instance).Inventory.GetSlotStorage() as SlotStorage;
                HotkeyData hotkeyData = _hotkeysRef(__instance)[number];
                bool refresh = false;

                if (obj.Equals(_playerRef(__instance).Holder.CurrentObject))
                {
                    for (int i = 0; i < _hotkeysRef(__instance).Length; i++)
                    {
                        HotkeyData data = _hotkeysRef(__instance)[i];

                        if (Main._occupied[data].Contains(obj.ReferenceId))
                        {
                            Main._occupied[data].Remove(obj.ReferenceId);
                            Main._reserved[data].Remove(obj.ReferenceId);

                            Main._quantity[data].Value = Main._occupied[data].Count;

                            if (Main._occupied[data].Count == 0)
                            {
                                if (Main._reserved[data].Count > 0 && Main.Settings.rememberToolbelt)
                                {
                                    Main._rememberHotkey[hotkeyData].Value = true;
                                }
                                else
                                {
                                    Main.ClearHotkey(data);
                                }
                            }

                            break;
                        }
                    }

                    Main._occupied[hotkeyData].Add(obj.ReferenceId);
                    Main._reserved[hotkeyData].AddLast(obj.ReferenceId);
                }
                else
                {
                    StorageSlot<IPickupable> storageSlot = storage.FindSlot(obj);

                    if (storageSlot != null)
                    {
                        int pickupablesToTransfer = Math.Min(storageSlot.Objects.Count, Main.GetHotkeyStackSize(storage, storageSlot.CraftingType));

                        Main.silentSlotStorageTransfer = true;

                        for (int i = 0; i < pickupablesToTransfer; i++)
                        {
                            // Take from the beginning of the list
                            IPickupable pickupable = storageSlot.Objects[0];

                            Main._occupied[hotkeyData].Add(pickupable.ReferenceId);
                            Main._reserved[hotkeyData].AddLast(pickupable.ReferenceId);

                            Main.Pop(storage, pickupable, true, false);
                            Main.Push(storage, StorageType.Hotkeys, pickupable, false);
                            refresh = true;
                        }

                        Main.silentSlotStorageTransfer = false;
                    }
                }

                if (refresh && _inventoryRadialMenuRef(__instance) != null)
                {
                    IList<IList<InventoryData>> inventoryData = GetInventoryData.Invoke(_inventoryRadialMenuRef(__instance).Inventory, null) as IList<IList<InventoryData>>;
                    _inventoryRadialMenuRef(__instance).Initialize(inventoryData);
                    _inventoryRadialMenuRef(__instance).Refresh();
                }

                Main._quantity[hotkeyData].Value = Main._occupied[hotkeyData].Count;
                hotkeyData.CraftingType.Value = obj.CraftingType;
            }
        }

        [HarmonyPatch(typeof(HotkeyController), nameof(HotkeyController.Save))]
        class HotkeyController_Save_Patch
        {
            static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");
            static readonly AccessTools.FieldRef<HotkeyController, IPlayer> _playerRef = AccessTools.FieldRefAccess<HotkeyController, IPlayer>("_player");
            static readonly AccessTools.FieldRef<HotkeyController, int> _levelRef = AccessTools.FieldRefAccess<HotkeyController, int>("_level");

            static readonly MethodInfo GetStackSize = AccessTools.Method(typeof(SlotStorage), "GetStackSize");

            static bool Prefix(HotkeyController __instance, ref JObject __result)
            {
                if (!Main.Enabled) return true;

                try
                {
                    SlotStorage storage = _playerRef(__instance).Inventory.GetSlotStorage() as SlotStorage;

                    JObject jobject = new JObject();
                    jobject.AddField("Level", _levelRef(__instance));
                    JObject jobject2 = new JObject();
                    for (int i = 0; i < _hotkeysRef(__instance).Length; i++)
                    {
                        HotkeyData hotkeyData = _hotkeysRef(__instance)[i];
                        CraftingType craftingType = hotkeyData.CraftingType.Value;
                        JObject jobject3 = new JObject();
                        jobject3.AddField("Locked", hotkeyData.Locked.Value);
                        jobject3.AddField("ReferenceId", Main._reserved[hotkeyData].LastOrDefault().ToString());

                        if (Main._reserved[hotkeyData].Count > 1)
                        {
                            JObject jobject4 = new JObject();

                            foreach (MiniGuid miniGuid in Main._reserved[hotkeyData].Take((int)GetStackSize.Invoke(storage, new object[] { craftingType })))
                            {
                                jobject4.Add(miniGuid.ToString());
                            }

                            jobject3.AddField("ReservedReferenceIds", jobject4);
                        }

                        jobject3.AddField("CraftingType", craftingType.Save());
                        jobject2.Add(jobject3);
                    }
                    jobject.AddField("Hotkeys", jobject2);
                    __result = jobject;
                    return false;
                }
                catch (Exception e)
                {
                    Main.Logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(HotkeyController), nameof(HotkeyController.Load))]
        class HotkeyController_Load_Patch
        {
            static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");
            static readonly AccessTools.FieldRef<HotkeyController, IPlayer> _playerRef = AccessTools.FieldRefAccess<HotkeyController, IPlayer>("_player");
            static readonly AccessTools.FieldRef<HotkeyController, int> _levelRef = AccessTools.FieldRefAccess<HotkeyController, int>("_level");

            static readonly AccessTools.FieldRef<SlotStorage, List<StorageSlot<IPickupable>>> _slotDataRef = AccessTools.FieldRefAccess<SlotStorage, List<StorageSlot<IPickupable>>>("_slotData");

            static readonly MethodInfo UpdateLevel = AccessTools.Method(typeof(HotkeyController), "UpdateLevel");

            static bool Prefix(HotkeyController __instance, JObject data)
            {
                if (!Main.Enabled) return true;

                try
                {
                    _levelRef(__instance) = data.GetField("Level").GetValue<int>();
                    UpdateLevel.Invoke(__instance, null);

                    JObject hotkeysData = data.GetField("Hotkeys");

                    for (int i = 0; i < hotkeysData.Children.Count; i++)
                    {
                        HotkeyData hotkeyData = _hotkeysRef(__instance)[i];
                        JObject jobject = hotkeysData.Children[i];
                        bool value = jobject.GetField("Locked").GetValue<bool>();
                        if (value)
                        {
                            break;
                        }

                        JObject referenceIdsData = jobject.GetField("ReservedReferenceIds");

                        if (referenceIdsData != null && !referenceIdsData.IsNull())
                        {
                            for (int j = 0; j < referenceIdsData.Children.Count; j++)
                            {
                                JObject jobject1 = referenceIdsData.Children[j];
                                MiniGuid miniGuid = jobject1.GetValue<string>().ToMiniGuid();

                                if (!miniGuid.IsDefault())
                                {
                                    Main._reserved[hotkeyData].AddLast(miniGuid);
                                }
                            }
                        }
                        else
                        {
                            JObject referenceIdData = jobject.GetField("ReferenceId");
                            MiniGuid miniGuid = referenceIdData.GetValue<string>().ToMiniGuid();

                            if (!miniGuid.IsDefault())
                            {
                                Main._reserved[hotkeyData].AddLast(miniGuid);
                            }
                        }

                        hotkeyData.Locked.Value = value;
                    }

                    LevelLoader.CurrentLoader.DeferredLoading.Add(() => Timing.RunCoroutine(DeferredLoad(__instance, hotkeysData)));
                    return false;
                }
                catch (Exception e)
                {
                    Main.Logger.LogException(e);
                    return true;
                }
            }

            public static IEnumerator<float> DeferredLoad(HotkeyController __instance, JObject hotkeysData)
            {
                float startTime = Time.unscaledTime;
                while (!_playerRef(__instance).Inventory.Loaded || !_playerRef(__instance).Holder.Loaded)
                {
                    if (Time.unscaledTime > startTime + PlayerRegistry.LocalPeer.GlobalTimeout)
                    {
                        MultiplayerMng.LogError(string.Format("Timeout while waiting for Holder & Inventory ({0}) to load.", _playerRef(__instance).Inventory.GetSlotStorage().LoadState), null);
                        Main.Logger.Error(string.Format("Timeout while waiting for Holder & Inventory ({0}) to load. Cannot proceed with loading the toolbelt.", _playerRef(__instance).Inventory.GetSlotStorage().LoadState));
                        yield break;
                    }
                    yield return 0f;
                }

                List<IPickupable> list = new List<IPickupable>();
                list.AddRange(_playerRef(__instance).Inventory.GetSlotStorage().GetStored());
                if (!_playerRef(__instance).Holder.CurrentObject.IsNullOrDestroyed())
                {
                    list.Add(_playerRef(__instance).Holder.CurrentObject);
                }

                SlotStorage storage = _playerRef(__instance).Inventory.GetSlotStorage() as SlotStorage;
                Dictionary<int, IPickupable> hotkeyPickupables = new Dictionary<int, IPickupable>();

                Main.silentSlotStorageTransfer = true;

                for (int i = 0; i < hotkeysData.Children.Count; i++)
                {
                    HotkeyData hotkeyData = _hotkeysRef(__instance)[i];
                    JObject jobject = hotkeysData.Children[i];
                    if (hotkeyData.Locked.Value)
                    {
                        break;
                    }
                    if (Main._reserved[hotkeyData].Count == 0)
                    {
                        continue;
                    }

                    List<IPickupable> pickupables = new List<IPickupable>();
                    CraftingType craftingType = CraftingType.Empty;

                    foreach (MiniGuid miniGuid in Main._reserved[hotkeyData])
                    {
                        IPickupable pickupable = list.FirstOrDefault_NonAlloc((IPickupable o, MiniGuid referenceId) => o.ReferenceId.Equals(referenceId), miniGuid);

                        if (!pickupable.IsNullOrDestroyed())
                        {
                            Main._occupied[hotkeyData].Add(pickupable.ReferenceId);
                            craftingType = pickupable.CraftingType;
                            list.Remove(pickupable);

                            if (!pickupable.Equals(_playerRef(__instance).Holder.CurrentObject))
                            {
                                Main.Pop(storage, pickupable, true, false);
                                pickupables.Add(pickupable);
                            }
                        }
                    }

                    foreach (IPickupable pickupable in pickupables)
                    {
                        Main.Push(storage, StorageType.Hotkeys, pickupable, false);
                    }

                    if (Main._occupied[hotkeyData].Count > 0)
                    {
                        Main._quantity[hotkeyData].Value = Main._occupied[hotkeyData].Count;
                        hotkeyData.CraftingType.Value = craftingType;
                    }
                    else if (Main.Settings.rememberToolbelt)
                    {
                        CraftingType craftingType2 = new CraftingType(AttributeType.None, InteractiveType.None);
                        JObject craftingTypeData = jobject.GetField("CraftingType");

                        if (craftingTypeData != null && !craftingTypeData.IsNull())
                        {
                            craftingType2.Load(craftingTypeData);
                        }

                        if (!craftingType2.Equals(CraftingType.Empty))
                        {
                            hotkeyData.CraftingType.Value = craftingType2;
                            Main._rememberHotkey[hotkeyData].Value = true;
                        }
                        else
                        {
                            Main._reserved[hotkeyData].Clear();
                        }
                    }
                    else
                    {
                        Main._reserved[hotkeyData].Clear();
                    }
                }

                Main.silentSlotStorageTransfer = false;
                yield break;
            }
        }

        [HarmonyPatch(typeof(HotkeyController), "UpdateLevel")]
        class HotkeyController_UpdateLevel_Patch
        {
            static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");
            static readonly AccessTools.FieldRef<HotkeyController, IPlayer> _playerRef = AccessTools.FieldRefAccess<HotkeyController, IPlayer>("_player");

            static readonly AccessTools.FieldRef<SlotStorage, List<StorageSlot<IPickupable>>> _slotDataRef = AccessTools.FieldRefAccess<SlotStorage, List<StorageSlot<IPickupable>>>("_slotData");
            static readonly AccessTools.FieldRef<SlotStorage, List<StorageSlot<IPickupable>>> _tempRef = AccessTools.FieldRefAccess<SlotStorage, List<StorageSlot<IPickupable>>>("_temp");
            static readonly AccessTools.FieldRef<SlotStorage, int> _slotCountRef = AccessTools.FieldRefAccess<SlotStorage, int>("_slotCount");

            static void Postfix(HotkeyController __instance)
            {
                if (!Main.Enabled) return;

                try
                {
                    SlotStorage storage = _playerRef(__instance).Inventory.GetSlotStorage() as SlotStorage;
                    int hotkeySlots = 0;

                    foreach (HotkeyData hotkeyData in _hotkeysRef(__instance))
                    {
                        if (hotkeyData.Locked.Value) break;

                        hotkeySlots++;
                    }

                    int newCapacity = 10 + hotkeySlots;
                    int oldCapacity = _slotCountRef(storage);
                    int slotsToAdd = newCapacity - oldCapacity;

                    _slotCountRef(storage) = newCapacity;
                    _slotDataRef(storage).Capacity = newCapacity;
                    _tempRef(storage).Capacity = newCapacity;

                    for (int i = 0; i < slotsToAdd; i++)
                    {
                        _slotDataRef(storage).Add(new StorageSlot<IPickupable>(oldCapacity + i));
                        _tempRef(storage).Add(null);
                    }
                }
                catch (Exception e)
                {
                    Main.Logger.LogException(e);
                }
            }
        }
    }
}

using Beam;
using Beam.Crafting;
using Beam.Serialization.Json;
using Beam.UI;
using Beam.Utilities;
using Funlabs;
using HarmonyLib;
using MEC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
#if DEBUG
using UnityEngine.UI;
#endif

namespace ToolbeltFix
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        [Draw("Remember Toolbelt", Height = 15)] public bool rememberToolbelt = false;
        [Draw("Always Show Hotkey Bar", Height = 15)] public bool alwaysShowHotkeyBar = false;
        [Draw("Stack Transfer", Height = 15)] public bool stackTransfer = false;
        [Draw("Quick Drop", Height = 15)] public bool quickDrop = true;

        [Space(10)]
        [Header("Cheats")]
        [Draw("Allow Container Crates", Height = 15)] public bool allowContainerCrates = false;
        //[Draw("Allow Large Items (Cheat)", Height = 15)] public bool allowLargeItems = false;
        //[Draw("Allow Multiple Same Type Items", Height = 15)] public bool allowMultipleSameTypeItems = false;

#if DEBUG
        [Space(10)]
        [Header("Debug")]
        [Draw("Grey", Min = 0, Max = 1, Width = 400, Type = DrawType.Slider)] public float albedo = 1f;
        [Draw("A", Min = 0, Max = 1, Width = 400, Type = DrawType.Slider)] public float a = 1f;
        [Draw("Block Crafting", Height = 15)] public bool blockCrafting = false;

        [Space(10)]
        [Draw("Simulate Release OnToggle", Height = 15)] public bool simulateReleaseOnToggle = false;
#endif

        private bool alwaysShowHotkeyBarOld;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        public void OnChange()
        {
#if DEBUG
            Main.ApplySettings();
#endif

            if (alwaysShowHotkeyBar != alwaysShowHotkeyBarOld)
            {
                Options.GeneralSettings.AlwaysShowHotkeyBar = alwaysShowHotkeyBarOld = alwaysShowHotkeyBar;

                if (PlayerRegistry.LocalPlayer.IsValid())
                {
                    AccessTools.Method(typeof(HotkeyController), "LoadOptions").Invoke(PlayerRegistry.LocalPlayer.Hotkeys, null);
                }
            }
        }

        public void Load()
        {
            alwaysShowHotkeyBarOld = alwaysShowHotkeyBar;
        }
    }

    public enum StorageType
    {
        Inventory,
        Hotkeys,
        Both
    }

#if DEBUG
    [EnableReloading]
#endif
    public class Main
    {
        private static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");

        private static readonly MethodInfo ClearExistingHotkey = AccessTools.Method(typeof(HotkeyController), "ClearExistingHotkey");

        private static readonly AccessTools.FieldRef<HotkeyElementView, UImageViewAdapter> _iconRef = AccessTools.FieldRefAccess<HotkeyElementView, UImageViewAdapter>("_icon");

        private static readonly AccessTools.FieldRef<StorageSlot<IPickupable>, int> _indexRef = AccessTools.FieldRefAccess<StorageSlot<IPickupable>, int>("_index");

        private static readonly AccessTools.FieldRef<SlotStorage, List<StorageSlot<IPickupable>>> _slotDataRef = AccessTools.FieldRefAccess<SlotStorage, List<StorageSlot<IPickupable>>>("_slotData");
        private static readonly AccessTools.FieldRef<SlotStorage, List<StorageSlot<IPickupable>>> _tempRef = AccessTools.FieldRefAccess<SlotStorage, List<StorageSlot<IPickupable>>>("_temp");
        private static readonly AccessTools.FieldRef<SlotStorage, Transform> _storageContainerRef = AccessTools.FieldRefAccess<SlotStorage, Transform>("_storageContainer");
        private static readonly AccessTools.FieldRef<SlotStorage, bool> _storeOtherStorageRef = AccessTools.FieldRefAccess<SlotStorage, bool>("_storeOtherStorage");
        private static readonly AccessTools.FieldRef<SlotStorage, LoadState> _loadStateRef = AccessTools.FieldRefAccess<SlotStorage, LoadState>("_loadState");
        private static readonly AccessTools.FieldRef<SlotStorage, bool> _modifiedRef = AccessTools.FieldRefAccess<SlotStorage, bool>("_modified");

        private static readonly MethodInfo CheckTypesMatch = AccessTools.Method(typeof(SlotStorage), "CheckTypesMatch");
        private static readonly MethodInfo GetStackSize = AccessTools.Method(typeof(SlotStorage), "GetStackSize");
        private static readonly MethodInfo OnPushed = AccessTools.Method(typeof(SlotStorage), "OnPushed");
        private static readonly MethodInfo OnPopped = AccessTools.Method(typeof(SlotStorage), "OnPopped");

        private static Dictionary<StorageRadialMenuPresenter, StorageMenuPresenter> storagePresenters = new Dictionary<StorageRadialMenuPresenter, StorageMenuPresenter>();
        private static IList<HotkeyElementView> kbHotkeyElements;
        private static IList<HotkeyElementView> dpHotkeyElements;
        private static bool silentSlotStorageTransfer;

        private static InventoryHotkeyComparer InventoryHotkeyComparison { get; } = new InventoryHotkeyComparer();

        private static UnityModManager.ModEntry.ModLogger logger;
        private static Settings settings;
        private static bool enabled;

#if DEBUG
        private static Harmony harmony;
#endif

#if DEBUG
        private static bool worldLoaded;

        private static Vector2 referenceResolution = new Vector2(1280f, 720f);
        private static Text texta, textb, textc, textd;
        private static GameObject canvas;

        [SaveOnReload]
        public static bool canvasActive;
        private static Font font;
#endif

        private static bool Load(UnityModManager.ModEntry modEntry)
        {
            settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            logger = modEntry.Logger;

            settings.Load();

#if DEBUG
            harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
#else
            Harmony harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
#endif

#if DEBUG
            modEntry.OnUpdate = OnUpdate;
#endif
            modEntry.OnToggle = OnToggle;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnGUI = OnGUI;

#if DEBUG
            modEntry.OnUnload = Unload;

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif

            VersionChecker.CheckVersion(modEntry);

            return true;
        }

#if DEBUG
        private static bool Unload(UnityModManager.ModEntry modEntry)
        {
            harmony.UnpatchAll(modEntry.Info.Id);
            return true;
        }
#endif

#if DEBUG
        private static readonly AccessTools.FieldRef<UHotkeyView, IList<HotkeyElementView>> _elementsRef = AccessTools.FieldRefAccess<UHotkeyView, IList<HotkeyElementView>>("_elements");
        private static readonly AccessTools.FieldRef<HotkeyController, HotkeyView> _viewRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyView>("_view");

        private static readonly AccessTools.FieldRef<PlatformHotkeyViewProvider, HotkeyView> _kbViewRef = AccessTools.FieldRefAccess<PlatformHotkeyViewProvider, HotkeyView>("_kbView");
        private static readonly AccessTools.FieldRef<PlatformHotkeyViewProvider, HotkeyView> _dpViewRef = AccessTools.FieldRefAccess<PlatformHotkeyViewProvider, HotkeyView>("_dpView");

        private static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
            try
            {
                if (StrandedWorld.Instance && PlayerRegistry.AllPlayers.Count > 0 && !worldLoaded)
                {
                    worldLoaded = true;

                    InitCanvas();
                    canvas.SetActive(canvasActive);

                    kbHotkeyElements = _elementsRef(_kbViewRef(_viewRef(PlayerRegistry.LocalPlayer.Hotkeys) as PlatformHotkeyViewProvider) as UHotkeyView);
                    dpHotkeyElements = _elementsRef(_dpViewRef(_viewRef(PlayerRegistry.LocalPlayer.Hotkeys) as PlatformHotkeyViewProvider) as UHotkeyView);

                    if (storagePresenters.Count == 0)
                    {
                        AccessTools.FieldRef<StorageMenuPresenter, StorageRadialMenuPresenter> _radialPresenterRef = AccessTools.FieldRefAccess<StorageMenuPresenter, StorageRadialMenuPresenter>("_radialPresenter");
                        StorageMenuPresenter[] storageMenuPresenters = UnityEngine.Object.FindObjectsOfType<StorageMenuPresenter>();

                        foreach (StorageMenuPresenter storagePresenter in storageMenuPresenters)
                        {
                            storagePresenters.Add(_radialPresenterRef(storagePresenter), storagePresenter);
                        }
                    }

                    //SlotStorageAudio[] audios = UnityEngine.Object.FindObjectsOfType<SlotStorageAudio>();
                    //logger.Log("OnUpdate - Audios: " + audios.Length + "\n" + audios.Join(audio => GetPath(audio.transform) + "\n"));
                }
                else if ((!StrandedWorld.Instance || PlayerRegistry.AllPlayers.Count == 0) && worldLoaded)
                {
                    worldLoaded = false;
                }

                if (!worldLoaded) return;

                if (Input.GetKeyDown(KeyCode.M))
                {
                    canvas.SetActive(!canvas.activeInHierarchy);
                    canvasActive = canvas.activeInHierarchy;
                }

                SlotStorage storage = PlayerRegistry.LocalPlayer.Holder.Storage as SlotStorage;
                List<StorageSlot<IPickupable>> _slotData = AccessTools.Field(typeof(SlotStorage), "_slotData").GetValue(storage) as List<StorageSlot<IPickupable>>;
                Holder holder = PlayerRegistry.LocalPlayer.Holder;

                texta.text = textb.text = textc.text = textd.text = string.Empty;

                texta.text += string.Format("i: {0}\n", 0);
                textb.text += string.Format("Type: {0}\n", holder.CurrentObject?.CraftingType.InteractiveType);
                textc.text += string.Format("Count: {0}\n", (holder.CurrentObject.IsNullOrDestroyed() ? 0 : 1));
                textd.text += string.Format("ReferenceId: {0}\n", holder.CurrentObject?.ReferenceId);

                texta.text += "\n";
                textb.text += "\n";
                textc.text += "\n";
                textd.text += "\n";

                for (int i = 0; i < storage.SlotCount; i++)
                {
                    texta.text += string.Format("i: {0} I: {1}\n", i, _indexRef(_slotData[i]));
                    textb.text += string.Format("Type: {0}\n", _slotData[i].CraftingType.InteractiveType);
                    textc.text += string.Format("Count: {0}\n", _slotData[i].Objects.Count);
                    textd.text += string.Format("ReferenceId: {0}\n", _slotData[i].Objects.Join(item => item.ReferenceId.ToString(), ", "));
                }

                texta.text += "\n\n\n";
                textb.text += "\n\n\n";
                textc.text += "\n\n\n";
                textd.text += "\n\n\n";

                foreach (HotkeyData hotkeyData in _hotkeysRef(PlayerRegistry.LocalPlayer.Hotkeys))
                {
                    texta.text += string.Format("i: {0}\n", hotkeyData.Number);
                    textb.text += string.Format("Type: {0}\n", hotkeyData.CraftingType.Value.InteractiveType);
                    textc.text += string.Format("Locked: {0}\n", hotkeyData.Locked.Value);
                    textd.text += string.Format("ReferenceId: {0}\n", hotkeyData.ReferenceId);
                }
            }
            catch (Exception e)
            {
                logger.LogException(e);
            }
        }

        private static string GetPath(Transform transform, string path = "")
        {
            path = transform.name + "/" + path;

            if (transform.parent)
                return GetPath(transform.parent, path);
            else
                return path;
        }
#endif

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Draw(modEntry);

            GUILayout.Space(10);
            GUILayout.Label("Created by Lazy");
        }

#if DEBUG
        public static void ApplySettings()
        {
            if (!worldLoaded) return;

            SlotStorage storage = PlayerRegistry.LocalPlayer.Holder.Storage as SlotStorage;
            IPickupable currentObject = PlayerRegistry.LocalPlayer.Holder.CurrentObject;
            HotkeyController hotkeyController = PlayerRegistry.LocalPlayer.Hotkeys;

            for (int i = 10; i < _slotDataRef(storage).Count; i++)
            {
                StorageSlot<IPickupable> hotkeySlot = _slotDataRef(storage)[i];

                if (hotkeySlot.Objects.Count == 0 && settings.rememberToolbelt)
                {
                    _iconRef(kbHotkeyElements[i - 10]).Color = new Color(settings.albedo, settings.albedo, settings.albedo, settings.a);
                    if (i - 10 < dpHotkeyElements.Count) _iconRef(dpHotkeyElements[i - 10]).Color = new Color(settings.albedo, settings.albedo, settings.albedo, settings.a);
                }
            }


            if (!currentObject.IsNullOrDestroyed() && CanPush(storage as SlotStorage, StorageType.Hotkeys, currentObject, false, false))
            {
                StorageSlot<IPickupable> slot = GetSlot(storage, StorageType.Hotkeys, currentObject, false);

                if (slot != null)
                {
                    HotkeyData hotkeyData = _hotkeysRef(hotkeyController)[_indexRef(slot) - 10];

                    _iconRef(kbHotkeyElements[_indexRef(slot) - 10]).Color = Color.white;
                    if (_indexRef(slot) - 10 < dpHotkeyElements.Count) _iconRef(dpHotkeyElements[_indexRef(slot) - 10]).Color = Color.white;
                }
            }
        }
#endif

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
#if DEBUG
            if (!value && settings.simulateReleaseOnToggle)
            {
                Timing.RunCoroutine(ToggleFailedNotification(modEntry), Segment.RealtimeUpdate);
                modEntry.Enabled = true;

                return false;
            }

            if (!value && worldLoaded)
            {
                if (canvas) UnityEngine.Object.Destroy(canvas);

                worldLoaded = false;
            }
#else
            if (StrandedWorld.Instance || PlayerRegistry.AllPlayers.Count > 0)
            {
                Timing.RunCoroutine(ToggleFailedNotification(modEntry), Segment.RealtimeUpdate);
                modEntry.Enabled = true;
                return false;
            }
#endif

            if (value)
            {
                Options.GeneralSettings.AlwaysShowHotkeyBar = settings.alwaysShowHotkeyBar;
            }
            else
            {
                Options.GeneralSettings.AlwaysShowHotkeyBar = false;
            }

            enabled = value;
            return true;
        }

        private static IEnumerator<float> ToggleFailedNotification(UnityModManager.ModEntry modEntry)
        {
            modEntry.CustomRequirements = "Can only toggle from the main menu";
            yield return Timing.WaitForSeconds(5f);
            modEntry.CustomRequirements = "";
        }

#if DEBUG
        private static void InitCanvas()
        {
            canvas = CreateCanvas();

            int fontSize = 11;

            texta = AddText("Text", fontSize, new Vector2(0.005f, 0.02f), new Vector2(0.98f, 0.98f));
            textb = AddText("Text", fontSize, new Vector2(0.045f, 0.02f), new Vector2(0.98f, 0.98f));
            textc = AddText("Text", fontSize, new Vector2(0.22f, 0.02f), new Vector2(0.98f, 0.98f));
            textd = AddText("Text", fontSize, new Vector2(0.28f, 0.02f), new Vector2(0.98f, 0.98f));
        }

        private static GameObject CreateCanvas()
        {
            GameObject canvasObject = new GameObject("Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 12;

            canvasObject.SetActive(false);
            AddCanvasScaler(canvasObject);

            return canvasObject;
        }

        private static void AddCanvasScaler(GameObject canvasObject)
        {
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static Text AddText(string txt, int fontSize, Vector2 anchorMin, Vector2 anchorMax)
        {
            // Text
            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(canvas.transform);

            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.text = txt;
            text.fontSize = fontSize;

            // Text position
            text.rectTransform.anchorMin = anchorMin;
            text.rectTransform.anchorMax = anchorMax;
            text.rectTransform.offsetMin = new Vector2();
            text.rectTransform.offsetMax = new Vector2();

            return text;
        }
#endif

        private static IEnumerable<StorageSlot<IPickupable>> GetSlots(SlotStorage __instance, params IComparer<StorageSlot<IPickupable>>[] comparators)
        {
            for (int j = 0; j < _slotDataRef(__instance).Count; j++)
            {
                _tempRef(__instance)[j] = _slotDataRef(__instance)[j];
            }
            if (comparators.Length > 0)
            {
                for (int k = 0; k < comparators.Length; k++)
                {
                    _tempRef(__instance).Sort(comparators[k]);
                }
            }
            int num;
            for (int i = 0; i < _tempRef(__instance).Count; i = num + 1)
            {
                yield return _tempRef(__instance)[i];
                num = i;
            }
            yield break;
        }

        private static IEnumerable<IPickupable> GetStored(SlotStorage __instance, StorageType storageType)
        {
            int count = storageType == StorageType.Inventory ? 10 : __instance.SlotCount;

            for (int i = storageType == StorageType.Hotkeys ? 10 : 0; i < count; i++)
            {
                foreach (IPickupable pickupable in _slotDataRef(__instance)[i].Objects)
                {
                    yield return pickupable;
                }
            }
            yield break;
        }

        private static bool CanPush(SlotStorage __instance, StorageType storageType, IPickupable pickupable, bool force, bool notification = true)
        {
            if (pickupable.IsNullOrDestroyed() || (!force && !pickupable.CanPickUp) || Has(__instance, storageType, pickupable))
            {
                return false;
            }
            if (!_storeOtherStorageRef(__instance) && pickupable.CraftingType.InteractiveType == InteractiveType.CONTAINER)
            {
                if (notification)
                {
                    OnPushed.Invoke(__instance, new object[] { pickupable, false });
                }
                return false;
            }
            if (GetSlot(__instance, storageType, pickupable, false) == null)
            {
                if (notification)
                {
                    OnPushed.Invoke(__instance, new object[] { pickupable, false });
                }
                return false;
            }
            return true;
        }

        private static bool Has(SlotStorage __instance, StorageType storageType, IPickupable pickupable)
        {
            if (pickupable.IsNullOrDestroyed())
            {
                return false;
            }
            foreach (IPickupable pickupable2 in GetStored(__instance, storageType))
            {
                if (pickupable.ReferenceId.Equals(pickupable2.ReferenceId))
                {
                    return true;
                }
            }
            return false;
        }

        private static void Pop(SlotStorage __instance, IPickupable pickupable, bool sort = true, bool silent = false, bool allowClearHotkey = false)
        {
            StorageSlot<IPickupable> storageSlot = __instance.FindSlot(pickupable);
            if (storageSlot == null)
            {
                return;
            }

            pickupable.Release();
            storageSlot.Objects.Remove(pickupable);
            if (storageSlot.Objects.Count == 0)
            {
                storageSlot.CraftingType = new CraftingType(AttributeType.None, InteractiveType.None);

                if (_indexRef(storageSlot) > 9 && allowClearHotkey && !settings.rememberToolbelt && PlayerRegistry.LocalPlayer.IsValid())
                {
                    ClearExistingHotkey.Invoke(PlayerRegistry.LocalPlayer.Hotkeys, new object[] { pickupable.ReferenceId });
                }
                else if (sort)
                {
                    __instance.SortSlots();
                }
            }
            if (!silent) OnPopped.Invoke(__instance, new object[] { pickupable });
            _modifiedRef(__instance) = true;
        }

        private static bool Push(SlotStorage __instance, StorageType storageType, IPickupable pickupable, bool force, bool silent = false)
        {
            if (CanPush(__instance, storageType, pickupable, force, true))
            {
                StorageSlot<IPickupable> slot = GetSlot(__instance, storageType, pickupable, true);
                if (slot != null)
                {
                    if (_indexRef(slot) > 9 && _loadStateRef(__instance).IsLoaded() && PlayerRegistry.LocalPlayer.IsValid())
                    {
                        HotkeyController hotkeyController = PlayerRegistry.LocalPlayer.Hotkeys;
                        HotkeyData hotkeyData = _hotkeysRef(hotkeyController)[_indexRef(slot) - 10];

                        _iconRef(kbHotkeyElements[_indexRef(slot) - 10]).Color = Color.white;
                        if (_indexRef(slot) - 10 < dpHotkeyElements.Count) _iconRef(dpHotkeyElements[_indexRef(slot) - 10]).Color = Color.white;
                        hotkeyData.ReferenceId = pickupable.ReferenceId;
                    }

                    pickupable.transform.CheckForProjectilesInChildren();
                    pickupable.transform.parent = null;
                    pickupable.TransformParent = _storageContainerRef(__instance);
                    pickupable.Parent();
                    pickupable.transform.localPosition = Vector3.zero;
                    pickupable.transform.localRotation = Quaternion.identity;
                    pickupable.transform.localScale = Vector3.one;
                    _modifiedRef(__instance) = true;
                    slot.Objects.Add(pickupable);
                    if (!silent) OnPushed.Invoke(__instance, new object[] { pickupable, true });
                    pickupable.Store();
                    return true;
                }
            }
            return false;
        }

        private static StorageSlot<IPickupable> GetSlot(SlotStorage __instance, StorageType storageType, IPickupable pickupable, bool assign = true)
        {
            if (storageType != StorageType.Inventory && PlayerRegistry.LocalPlayer.IsValid())
            {
                HotkeyController hotkeyController = PlayerRegistry.LocalPlayer.Hotkeys;

                for (int i = 10; i < _slotDataRef(__instance).Count; i++)
                {
                    MiniGuid referenceId = _hotkeysRef(hotkeyController)[i - 10].ReferenceId;
                    StorageSlot<IPickupable> storageSlot = _slotDataRef(__instance)[i];

                    if (storageSlot.Objects.Count == 0 && !referenceId.IsDefault() && referenceId.Equals(pickupable.ReferenceId))
                    {
                        if (assign)
                        {
                            storageSlot.CraftingType = new CraftingType(pickupable.CraftingType.AttributeType, pickupable.CraftingType.InteractiveType);
                        }
                        return storageSlot;
                    }
                }

                for (int i = 10; i < _slotDataRef(__instance).Count; i++)
                {
                    CraftingType craftingType = _hotkeysRef(hotkeyController)[i - 10].CraftingType.Value;
                    StorageSlot<IPickupable> storageSlot = _slotDataRef(__instance)[i];

                    if (storageSlot.Objects.Count == 0 && !craftingType.Equals(CraftingType.Empty) && (bool)CheckTypesMatch.Invoke(__instance, new object[] { craftingType, pickupable.CraftingType.InteractiveType, pickupable.CraftingType.AttributeType }))
                    {
                        if (assign)
                        {
                            storageSlot.CraftingType = new CraftingType(pickupable.CraftingType.AttributeType, pickupable.CraftingType.InteractiveType);
                        }
                        return storageSlot;
                    }
                }

                if (storageType == StorageType.Hotkeys)
                {
                    return null;
                }
            }
            for (int i = 0; i < 10; i++)
            {
                StorageSlot<IPickupable> storageSlot = _slotDataRef(__instance)[i];
                if (storageSlot.Objects.Count > 0)
                {
                    CraftingType craftingType = storageSlot.CraftingType;
                    if (storageSlot.Objects.Count < (int)GetStackSize.Invoke(__instance, new object[] { craftingType }) && (bool)CheckTypesMatch.Invoke(__instance, new object[] { craftingType, pickupable.CraftingType.InteractiveType, pickupable.CraftingType.AttributeType }))
                    {
                        return storageSlot;
                    }
                }
            }
            for (int j = 0; j < _slotDataRef(__instance).Count; j++)
            {
                StorageSlot<IPickupable> storageSlot2 = _slotDataRef(__instance)[j];
                if (storageSlot2.Objects.Count == 0 && (j < 10 || _loadStateRef(__instance).IsLoading()))
                {
                    if (assign)
                    {
                        storageSlot2.CraftingType = new CraftingType(pickupable.CraftingType.AttributeType, pickupable.CraftingType.InteractiveType);
                    }
                    return storageSlot2;
                }
            }
            return null;
        }

        [HarmonyPatch(typeof(GeneralSettings), nameof(GeneralSettings.Load))]
        private class GeneralSettings_Load_Patch
        {
            private static void Postfix(GeneralSettings __instance)
            {
                if (!enabled) return;

                try
                {
                    __instance.AlwaysShowHotkeyBar = settings.alwaysShowHotkeyBar;
                }
                catch (Exception e)
                {
                    logger.LogException(e);
                }
            }
        }

        [HarmonyPatch(typeof(Crafter), "GetPlayerCraftingMaterials")]
        private class Crafter_GetPlayerCraftingMaterials_Patch
        {
            private static readonly AccessTools.FieldRef<Crafter, Dictionary<CraftingType, IList<IBase>>> _cachedMaterialsLookupRef = AccessTools.FieldRefAccess<Crafter, Dictionary<CraftingType, IList<IBase>>>("_cachedMaterialsLookup");
            private static readonly AccessTools.FieldRef<Crafter, IPlayer> _playerRef = AccessTools.FieldRefAccess<Crafter, IPlayer>("_player");

            private static readonly MethodInfo AddMaterial = AccessTools.Method(typeof(Crafter), "AddMaterial");

            private static bool Prefix(Crafter __instance, ref IDictionary<CraftingType, IList<IBase>> __result)
            {
                if (!enabled) return true;

                try
                {
                    foreach (StorageSlot<IPickupable> storageSlot in GetSlots(_playerRef(__instance).Inventory.GetSlotStorage() as SlotStorage, InventoryHotkeyComparison, StorageSlot<IPickupable>.QuantityComparison))
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
                    logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Holder), "Release")]
        private class Holder_Release_Patch
        {
            private static readonly AccessTools.FieldRef<Holder, IPickupable> _currentObjectRef = AccessTools.FieldRefAccess<Holder, IPickupable>("_currentObject");

            private static bool Prefix(Holder __instance, IPickupable pickupable)
            {
                if (!enabled) return true;

                try
                {
                    if (pickupable.IsNullOrDestroyed())
                    {
                        return false;
                    }
                    if (_currentObjectRef(__instance) == pickupable)
                    {
                        __instance.DropCurrent();
                        return false;
                    }

                    Pop(__instance.Storage as SlotStorage, pickupable, true, false, true);
                    return false;
                }
                catch (Exception e)
                {
                    logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Holder), nameof(Holder.Select))]
        private class Holder_Select_Patch
        {
            private static readonly AccessTools.FieldRef<Holder, ISlotStorage<IPickupable>> _storageRef = AccessTools.FieldRefAccess<Holder, ISlotStorage<IPickupable>>("_storage");
            private static readonly AccessTools.FieldRef<Holder, IGameInputController> _controllerRef = AccessTools.FieldRefAccess<Holder, IGameInputController>("_controller");
            private static readonly AccessTools.FieldRef<Holder, IPickupable> _currentlyPickingRef = AccessTools.FieldRefAccess<Holder, IPickupable>("_currentlyPicking");
            private static readonly AccessTools.FieldRef<Holder, Transform> _holdingContainerRef = AccessTools.FieldRefAccess<Holder, Transform>("_holdingContainer");
            private static readonly AccessTools.FieldRef<Holder, IPickupable> _currentObjectRef = AccessTools.FieldRefAccess<Holder, IPickupable>("_currentObject");
            private static readonly AccessTools.FieldRef<Holder, IPlayer> _playerRef = AccessTools.FieldRefAccess<Holder, IPlayer>("_player");

            private static readonly MethodInfo HidePickupable = AccessTools.Method(typeof(Holder), "HidePickupable");
            private static readonly MethodInfo ShowPickupable = AccessTools.Method(typeof(Holder), "ShowPickupable");
            private static readonly MethodInfo Reload = AccessTools.Method(typeof(Holder), "Reload");

            private static bool Prefix(Holder __instance, IPickupable pickupable, bool __result)
            {
                if (!enabled) return true;

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
                    _storageRef(__instance).Pop(pickupable);
                    if (!_currentObjectRef(__instance).IsNullOrDestroyed() && !_storageRef(__instance).Push(_currentObjectRef(__instance)))
                    {
                        _storageRef(__instance).Push(pickupable);
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
                    if (_currentlyPickingRef(__instance) == pickupable && CanPush(_storageRef(__instance) as SlotStorage, StorageType.Hotkeys, pickupable, false, false))
                    {
                        StorageSlot<IPickupable> slot = GetSlot(_storageRef(__instance) as SlotStorage, StorageType.Hotkeys, pickupable, false);

                        if (slot != null && _storageRef(__instance).LoadState.IsLoaded() && PlayerRegistry.LocalPlayer.IsValid())
                        {
                            HotkeyController hotkeyController = PlayerRegistry.LocalPlayer.Hotkeys;
                            HotkeyData hotkeyData = _hotkeysRef(hotkeyController)[_indexRef(slot) - 10];
                            hotkeyData.ReferenceId = pickupable.ReferenceId;

                            _iconRef(kbHotkeyElements[_indexRef(slot) - 10]).Color = Color.white;
                            if (_indexRef(slot) - 10 < dpHotkeyElements.Count) _iconRef(dpHotkeyElements[_indexRef(slot) - 10]).Color = Color.white;
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
                    logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SlotStorage), "CanPush", new Type[] { typeof(IPickupable), typeof(bool), typeof(bool) })]
        private class SlotStorage_CanPush_Patch
        {
            private static bool Prefix(SlotStorage __instance, IPickupable pickupable, bool force, bool notification, ref bool __result)
            {
                if (!enabled) return true;

                try
                {
                    if (!__instance.Name.Equals("INVENTORY_MENU_BACKPACK_TITLE")) return true;

                    __result = CanPush(__instance, StorageType.Both, pickupable, force, notification);
                    return false;
                }
                catch (Exception e)
                {
                    logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SlotStorage), nameof(SlotStorage.Pop))]
        private class SlotStorage_Pop_Patch
        {
            private static bool Prefix(SlotStorage __instance, IPickupable pickupable)
            {
                if (!enabled) return true;

                try
                {
                    if (!__instance.Name.Equals("INVENTORY_MENU_BACKPACK_TITLE")) return true;

                    Pop(__instance, pickupable);
                    return false;
                }
                catch (Exception e)
                {
                    logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SlotStorage), nameof(SlotStorage.PopNext))]
        private class SlotStorage_PopNext_Patch
        {
            private static readonly AccessTools.FieldRef<StorageSlot<IPickupable>, int> _indexRef = AccessTools.FieldRefAccess<StorageSlot<IPickupable>, int>("_index");

            private static readonly AccessTools.FieldRef<SlotStorage, List<StorageSlot<IPickupable>>> _slotDataRef = AccessTools.FieldRefAccess<SlotStorage, List<StorageSlot<IPickupable>>>("_slotData");
            private static readonly AccessTools.FieldRef<SlotStorage, StorageSlot<IPickupable>> _selectedSlotRef = AccessTools.FieldRefAccess<SlotStorage, StorageSlot<IPickupable>>("_selectedSlot");

            private static readonly MethodInfo OnSelectionChanged = AccessTools.Method(typeof(SlotStorage), "OnSelectionChanged");

            private static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");

            private static readonly MethodInfo ClearHotkey = AccessTools.Method(typeof(HotkeyController), "ClearHotkey");

            private static bool Prefix(SlotStorage __instance, IPickupable pickupable)
            {
                if (!enabled) return true;

                try
                {
                    if (!__instance.Name.Equals("INVENTORY_MENU_BACKPACK_TITLE")) return true;

                    if (_selectedSlotRef(__instance) == null || _selectedSlotRef(__instance).Objects.Count == 0)
                    {
                        StorageSlot<IPickupable> storageSlot = __instance.FindSlot(pickupable.CraftingType);
                        if (storageSlot == null)
                        {
                            storageSlot = __instance.FindSlot(pickupable.CraftingType.InteractiveType, AttributeType.None);
                        }
                        if (storageSlot != null)
                        {
                            _selectedSlotRef(__instance) = storageSlot;
                        }

                        if ((storageSlot == null || _indexRef(storageSlot) > 9) && PlayerRegistry.LocalPlayer.IsValid())
                        {
                            HotkeyController hotkeyController = PlayerRegistry.LocalPlayer.Hotkeys;

                            for (int i = 10; i < _slotDataRef(__instance).Count; i++)
                            {
                                StorageSlot<IPickupable> hotkeySlot = _slotDataRef(__instance)[i];
                                HotkeyData hotkeyData = _hotkeysRef(hotkeyController)[i - 10];

                                if (hotkeyData.CraftingType.Value.Equals(pickupable.CraftingType) && hotkeySlot.Objects.Count == 0)
                                {
                                    if (settings.rememberToolbelt)
                                    {
#if DEBUG
                                        _iconRef(kbHotkeyElements[i - 10]).Color = new Color(settings.albedo, settings.albedo, settings.albedo, settings.a);
                                        if (i - 10 < dpHotkeyElements.Count) _iconRef(dpHotkeyElements[i - 10]).Color = new Color(settings.albedo, settings.albedo, settings.albedo, settings.a);
#else
                                        _iconRef(kbHotkeyElements[i - 10]).Color = new Color(0.4f, 0.4f, 0.4f, 0.65f);
                                        if (i - 10 < dpHotkeyElements.Count) _iconRef(dpHotkeyElements[i - 10]).Color = new Color(0.4f, 0.4f, 0.4f, 0.65f);
#endif
                                    }
                                    else
                                    {
                                        ClearHotkey.Invoke(hotkeyController, new object[] { hotkeyData });
                                    }
                                }
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
                    logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SlotStorage), nameof(SlotStorage.Push), new Type[] { typeof(IPickupable), typeof(bool) })]
        private class SlotStorage_Push_Patch
        {
            private static bool Prefix(SlotStorage __instance, IPickupable pickupable, bool force, ref bool __result)
            {
                if (!enabled) return true;

                try
                {
                    if (!__instance.Name.Equals("INVENTORY_MENU_BACKPACK_TITLE")) return true;

                    __result = Push(__instance, StorageType.Both, pickupable, force);
                    return false;
                }
                catch (Exception e)
                {
                    logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SlotStorage), nameof(SlotStorage.SortSlots))]
        private class SlotStorage_SortSlots_Patch
        {
            private static readonly AccessTools.FieldRef<SlotStorage, List<StorageSlot<IPickupable>>> _slotDataRef = AccessTools.FieldRefAccess<SlotStorage, List<StorageSlot<IPickupable>>>("_slotData");
            private static readonly AccessTools.FieldRef<SlotStorage, bool> _sortRef = AccessTools.FieldRefAccess<SlotStorage, bool>("_sort");

            private static bool Prefix(SlotStorage __instance)
            {
                if (!enabled) return true;

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
                    logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SlotStorageAudio), "SlotStorage_Pushed")]
        private class SlotStorageAudio_SlotStorage_Pushed_Patch
        {
            private static readonly AccessTools.FieldRef<SlotStorageAudio, AudioClip> _storedClipRef = AccessTools.FieldRefAccess<SlotStorageAudio, AudioClip>("_storedClip");
            private static readonly AccessTools.FieldRef<SlotStorageAudio, AudioClip> _fullClipRef = AccessTools.FieldRefAccess<SlotStorageAudio, AudioClip>("_fullClip");

            private static bool Prefix(SlotStorageAudio __instance, bool success)
            {
                if (!enabled) return true;

                try
                {
                    if (LevelLoader.IsLoading() || silentSlotStorageTransfer)
                    {
                        return false;
                    }
                    AudioManager.GetAudioPlayer().Play3D(success ? _storedClipRef(__instance) : _fullClipRef(__instance), __instance.transform.position, AudioMixerChannels.FX, AudioRollOffDistance.VeryNear, AudioPlayMode.Single);
                    return false;
                }
                catch (Exception e)
                {
                    logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SlotStorageAudio), "SlotStorage_Popped")]
        private class SlotStorageAudio_SlotStorage_Popped_Patch
        {
            private static readonly AccessTools.FieldRef<SlotStorageAudio, AudioClip> _poppedClipRef = AccessTools.FieldRefAccess<SlotStorageAudio, AudioClip>("_poppedClip");

            private static bool Prefix(SlotStorageAudio __instance)
            {
                if (!enabled) return true;

                try
                {
                    if (LevelLoader.IsLoading() || silentSlotStorageTransfer)
                    {
                        return false;
                    }
                    AudioManager.GetAudioPlayer().Play3D(_poppedClipRef(__instance), __instance.transform.position, AudioMixerChannels.FX, AudioRollOffDistance.VeryNear, AudioPlayMode.Single);
                    return false;
                }
                catch (Exception e)
                {
                    logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(StorageMenuPresenter), "Start")]
        private class StorageMenuPresenter_Start_Patch
        {
            private static readonly AccessTools.FieldRef<StorageMenuPresenter, StorageRadialMenuPresenter> _radialPresenterRef = AccessTools.FieldRefAccess<StorageMenuPresenter, StorageRadialMenuPresenter>("_radialPresenter");

            private static void Postfix(StorageMenuPresenter __instance)
            {
                if (!enabled) return;

                try
                {
                    storagePresenters.Add(_radialPresenterRef(__instance), __instance);
                }
                catch (Exception e)
                {
                    logger.LogException(e);
                }
            }
        }

        [HarmonyPatch(typeof(StorageMenuPresenter), "OnDestroy")]
        private class StorageMenuPresenter_OnDestroy_Patch
        {
            private static readonly AccessTools.FieldRef<StorageMenuPresenter, StorageRadialMenuPresenter> _radialPresenterRef = AccessTools.FieldRefAccess<StorageMenuPresenter, StorageRadialMenuPresenter>("_radialPresenter");

            private static void Postfix(StorageMenuPresenter __instance)
            {
                if (!enabled) return;

                try
                {
                    storagePresenters.Remove(_radialPresenterRef(__instance));
                }
                catch (Exception e)
                {
                    logger.LogException(e);
                }
            }
        }

        [HarmonyPatch(typeof(RadialMenuPresenterBase<InventoryData, InventoryRadialMenuElement, InventoryRadialMenuViewAdapterBase>), "OnElementSecondaryShortPress")]
        private class RadialMenuPresenterBase_OnElementSecondaryShortPress_Patch
        {
            private static readonly AccessTools.FieldRef<StorageMenuPresenter, ISlotStorage<IPickupable>> _storageRef = AccessTools.FieldRefAccess<StorageMenuPresenter, ISlotStorage<IPickupable>>("_storage");

            private static readonly MethodInfo RemoveRefreshCallbacks = AccessTools.Method(typeof(StorageMenuPresenter), "RemoveRefreshCallbacks");
            private static readonly MethodInfo AddRefreshCallbacks = AccessTools.Method(typeof(StorageMenuPresenter), "AddRefreshCallbacks");
            private static readonly MethodInfo MPFriendlyExchange = AccessTools.Method(typeof(StorageMenuPresenter), "MPFriendlyExchange");
            private static readonly MethodInfo Refresh = AccessTools.Method(typeof(StorageMenuPresenter), "Refresh");

            private static bool Prefix(RadialMenuPresenterBase<InventoryData, InventoryRadialMenuElement, InventoryRadialMenuViewAdapterBase> __instance, IRadialMenuElement<InventoryData> element)
            {
                if (!enabled || !settings.stackTransfer) return true;

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
                    StorageMenuPresenter storagePresenter = storagePresenters[storageRadialPresenter];
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
                        silentSlotStorageTransfer = false;

                        while (pickupables.Count > 0 && storagePresenter.DestinationStorage.CanPush(pickupables[0], !silentSlotStorageTransfer))
                        {
                            MPFriendlyExchange.Invoke(storagePresenter, new object[] { _storageRef(storagePresenter), storagePresenter.DestinationStorage, pickupables[0] });
                            silentSlotStorageTransfer = true;
                            pickupables.RemoveAt(0);
                            refresh = true;
                        }

                        silentSlotStorageTransfer = false;

                        AddRefreshCallbacks.Invoke(storagePresenter, new object[] { _storageRef(storagePresenter) });
                        if (refresh) Refresh.Invoke(storagePresenter, null);
                    }
                    return false;
                }
                catch (Exception e)
                {
                    logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(InventoryMenuPresenter), "RadialPresenter_ElementSecondaryClick")]
        private class InventoryMenuPresenter_RadialPresenter_ElementSecondaryClick_Patch
        {
            private static readonly AccessTools.FieldRef<InventoryMenuPresenter, InventoryRadialMenuPresenter> _radialPresenterRef = AccessTools.FieldRefAccess<InventoryMenuPresenter, InventoryRadialMenuPresenter>("_radialPresenter");
            private static readonly AccessTools.FieldRef<InventoryMenuPresenter, IPlayer> _playerRef = AccessTools.FieldRefAccess<InventoryMenuPresenter, IPlayer>("_player");

            private static readonly MethodInfo GetInventoryData = AccessTools.Method(typeof(InventoryMenuPresenter), "GetInventoryData");

            private static bool Prefix(InventoryMenuPresenter __instance, IRadialMenuElement<InventoryData> element)
            {
                if (!enabled || !settings.quickDrop) return true;

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
                    logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(InventoryMenuPresenter), "RadialPresenter_ElementSecondaryShortPress")]
        private class InventoryMenuPresenter_RadialPresenter_ElementSecondaryShortPress_Patch
        {
            private static readonly AccessTools.FieldRef<InventoryMenuPresenter, InventoryRadialMenuPresenter> _radialPresenterRef = AccessTools.FieldRefAccess<InventoryMenuPresenter, InventoryRadialMenuPresenter>("_radialPresenter");
            private static readonly AccessTools.FieldRef<InventoryMenuPresenter, IPlayer> _playerRef = AccessTools.FieldRefAccess<InventoryMenuPresenter, IPlayer>("_player");

            private static readonly MethodInfo GetInventoryData = AccessTools.Method(typeof(InventoryMenuPresenter), "GetInventoryData");

            private static bool Prefix(InventoryMenuPresenter __instance, IRadialMenuElement<InventoryData> element)
            {
                if (!enabled || !settings.stackTransfer) return true;

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

                            silentSlotStorageTransfer = false;

                            while (storageSlot.Objects.Count > 0 && __instance.DestinationStorage.CanPush(storageSlot.Objects[0], !silentSlotStorageTransfer))
                            {
                                IPickupable pickupable = storageSlot.Objects[0];
                                _playerRef(__instance).Holder.ReplicatedRelease(pickupable);
                                __instance.DestinationStorage.ReplicatedPush(pickupable);
                                silentSlotStorageTransfer = true;
                                refresh = true;
                            }

                            silentSlotStorageTransfer = false;

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
                    logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(PlatformHotkeyViewProvider), nameof(PlatformHotkeyViewProvider.Initialize))]
        private class PlatformHotkeyViewProvider_Initialize_Patch
        {
            private static readonly AccessTools.FieldRef<PlatformHotkeyViewProvider, HotkeyView> _kbViewRef = AccessTools.FieldRefAccess<PlatformHotkeyViewProvider, HotkeyView>("_kbView");
            private static readonly AccessTools.FieldRef<PlatformHotkeyViewProvider, HotkeyView> _dpViewRef = AccessTools.FieldRefAccess<PlatformHotkeyViewProvider, HotkeyView>("_dpView");

            private static readonly AccessTools.FieldRef<UHotkeyView, IList<HotkeyElementView>> _elementsRef = AccessTools.FieldRefAccess<UHotkeyView, IList<HotkeyElementView>>("_elements");

            private static void Postfix(PlatformHotkeyViewProvider __instance)
            {
                if (!enabled) return;

                try
                {
                    kbHotkeyElements = _elementsRef(_kbViewRef(__instance) as UHotkeyView);
                    dpHotkeyElements = _elementsRef(_dpViewRef(__instance) as UHotkeyView);
                }
                catch (Exception e)
                {
                    logger.LogException(e);
                }
            }
        }

        [HarmonyPatch(typeof(HotkeyController), "LoadOptions")]
        private class HotkeyController_LoadOptions_Patch
        {
            private static readonly AccessTools.FieldRef<HotkeyController, InventoryRadialMenuPresenter> _inventoryRadialMenuRef = AccessTools.FieldRefAccess<HotkeyController, InventoryRadialMenuPresenter>("_inventoryRadialMenu");
            private static readonly AccessTools.FieldRef<HotkeyController, bool> _subbedToInventoryEventsRef = AccessTools.FieldRefAccess<HotkeyController, bool>("_subbedToInventoryEvents");
            private static readonly AccessTools.FieldRef<HotkeyController, HotkeyView> _viewRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyView>("_view");

            private static readonly AccessTools.FieldRef<PlatformHotkeyViewProvider, HotkeyView> platform_viewRef = AccessTools.FieldRefAccess<PlatformHotkeyViewProvider, HotkeyView>("_view");

            private static readonly MethodInfo GetView = AccessTools.Method(typeof(PlatformHotkeyViewProvider), "GetView");

            private static bool Prefix(HotkeyController __instance)
            {
                if (!enabled) return true;

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
                    logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(HotkeyController), "Holder_Dropped")]
        private class HotkeyController_Holder_Dropped_Patch
        {
            private static bool Prefix()
            {
                if (!enabled) return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(HotkeyController), "DoHotkey")]
        private class HotkeyController_DoHotkey_Patch
        {
            private static readonly AccessTools.FieldRef<HotkeyController, InventoryRadialMenuPresenter> _inventoryRadialMenuRef = AccessTools.FieldRefAccess<HotkeyController, InventoryRadialMenuPresenter>("_inventoryRadialMenu");
            private static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");
            private static readonly AccessTools.FieldRef<HotkeyController, HotkeyView> _viewRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyView>("_view");
            private static readonly AccessTools.FieldRef<HotkeyController, IPlayer> _playerRef = AccessTools.FieldRefAccess<HotkeyController, IPlayer>("_player");

            private static readonly MethodInfo GetInventoryData = AccessTools.Method(typeof(InventoryMenuPresenter), "GetInventoryData");
            private static readonly MethodInfo BlockHotkeyInput = AccessTools.Method(typeof(HotkeyController), "BlockHotkeyInput");

            private static bool Prefix(HotkeyController __instance, int number)
            {
                if (!enabled) return true;

                try
                {
                    if ((bool)BlockHotkeyInput.Invoke(__instance, null))
                    {
                        return false;
                    }

                    MiniGuid referenceId = _hotkeysRef(__instance)[number].ReferenceId;
                    CraftingType craftingType = _hotkeysRef(__instance)[number].CraftingType.Value;
                    IPickupable currentObject = _playerRef(__instance).Holder.CurrentObject;

                    if (referenceId.IsDefault() || !currentObject.IsNullOrDestroyed() && currentObject.CraftingType.Equals(craftingType))
                    {
                        return false;
                    }

                    ISlotStorage<IPickupable> storage = _playerRef(__instance).Inventory.GetSlotStorage();
                    IPickupable pickupable = storage.GetSlots(InventoryHotkeyComparison).SelectMany(slot => slot.Objects).FirstOrDefault_NonAlloc((IPickupable item, MiniGuid id) => item.ReferenceId.Equals(id), referenceId);

                    if (!pickupable.IsNullOrDestroyed() && (currentObject.IsNullOrDestroyed() || storage.CanPush(currentObject)))
                    {
                        _playerRef(__instance).Holder.ReplicatedSelect(pickupable);
                        IList<IList<InventoryData>> inventoryData = GetInventoryData.Invoke(_inventoryRadialMenuRef(__instance).Inventory, null) as IList<IList<InventoryData>>;
                        _inventoryRadialMenuRef(__instance).Initialize(inventoryData);
                        _inventoryRadialMenuRef(__instance).Refresh();
                        return false;
                    }
                    _viewRef(__instance).OnAssignmentFailed();
                    return false;
                }
                catch (Exception e)
                {
                    logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(HotkeyController), "DoHotkeyAssignment")]
        private class HotkeyController_DoHotkeyAssignment_Patch
        {
            private static readonly AccessTools.FieldRef<HotkeyController, InventoryRadialMenuPresenter> _inventoryRadialMenuRef = AccessTools.FieldRefAccess<HotkeyController, InventoryRadialMenuPresenter>("_inventoryRadialMenu");
            private static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");
            private static readonly AccessTools.FieldRef<HotkeyController, HotkeyView> _viewRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyView>("_view");
            private static readonly AccessTools.FieldRef<HotkeyController, IPlayer> _playerRef = AccessTools.FieldRefAccess<HotkeyController, IPlayer>("_player");

            private static readonly MethodInfo BlockHotkeyInput = AccessTools.Method(typeof(HotkeyController), "BlockHotkeyInput");
            private static readonly MethodInfo ClearHotkey = AccessTools.Method(typeof(HotkeyController), "ClearHotkey");

            private static readonly AccessTools.FieldRef<SlotStorage, List<StorageSlot<IPickupable>>> _slotDataRef = AccessTools.FieldRefAccess<SlotStorage, List<StorageSlot<IPickupable>>>("_slotData");

            private static readonly MethodInfo GetInventoryData = AccessTools.Method(typeof(InventoryMenuPresenter), "GetInventoryData");

            private static bool Prefix(HotkeyController __instance, int number, IPickupable obj)
            {
                if (!enabled) return true;

                try
                {
                    if ((bool)BlockHotkeyInput.Invoke(__instance, null))
                    {
                        return false;
                    }

                    HotkeyData hotkeyData = _hotkeysRef(__instance)[number];

                    if (obj.IsNullOrDestroyed() && hotkeyData.ReferenceId.IsDefault())
                    {
                        _viewRef(__instance)?.OnAssignmentFailed();
                        return false;
                    }

                    if (obj.IsNullOrDestroyed() || hotkeyData.CraftingType.Value.Equals(obj.CraftingType))
                    {
                        SlotStorage storage = _playerRef(__instance).Holder.Storage as SlotStorage;
                        StorageSlot<IPickupable> storageSlot = _slotDataRef(storage)[10 + number];

                        if (storageSlot.Objects.Count > 0)
                        {
                            IPickupable pickupable = storageSlot.Objects[0];

                            if (CanPush(storage, StorageType.Inventory, pickupable, false, true))
                            {
                                Pop(storage, pickupable, false, true);
                                Push(storage, StorageType.Inventory, pickupable, false, false);
                                IList<IList<InventoryData>> inventoryData = GetInventoryData.Invoke(_inventoryRadialMenuRef(__instance).Inventory, null) as IList<IList<InventoryData>>;
                                _inventoryRadialMenuRef(__instance).Initialize(inventoryData);
                                _inventoryRadialMenuRef(__instance).Refresh();
                            }
                            else
                            {
                                _viewRef(__instance)?.OnAssignmentFailed();
                                return false;
                            }
                        }

                        ClearHotkey.Invoke(__instance, new object[] { hotkeyData });
                        _viewRef(__instance)?.OnAssignmentRemoved();
                    }
                    else if (!obj.IsNullOrDestroyed())
                    {
                        if (obj.CraftingType.InteractiveType == InteractiveType.CONTAINER && !settings.allowContainerCrates)
                        {
                            LocalizedNotification.Post(_playerRef(__instance), NotificationPriority.Low, 4f, "Cannot assign container crates to the toolbelt.");
                            _viewRef(__instance)?.OnAssignmentFailed();
                            return false;
                        }

                        bool isCurrentObject = obj.Equals(_playerRef(__instance).Holder.CurrentObject);
                        SlotStorage storage = _playerRef(__instance).Holder.Storage as SlotStorage;
                        StorageSlot<IPickupable> storageSlot = _slotDataRef(storage)[10 + number];
                        IPickupable existingHotkeyPickupable = null;
                        HotkeyData existingHotkeyData = null;

                        for (int i = 10; i < _slotDataRef(storage).Count; i++)
                        {
                            StorageSlot<IPickupable> hotkeySlot = _slotDataRef(storage)[i];
                            HotkeyData hotkeyData2 = _hotkeysRef(__instance)[i - 10];

                            if (hotkeyData2.ReferenceId.Equals(obj.ReferenceId))
                            {
                                existingHotkeyData = hotkeyData2;

                                if (hotkeySlot.Objects.Count > 0)
                                {
                                    existingHotkeyPickupable = hotkeySlot.Objects[0];
                                }

                                break;
                            }
                        }

                        if (!isCurrentObject && existingHotkeyPickupable.IsNullOrDestroyed())
                        {
                            Pop(storage, obj, false, true);
                        }

                        if (storageSlot.Objects.Count > 0)
                        {
                            IPickupable hotkeyPickupable = storageSlot.Objects[0];

                            if (CanPush(storage, StorageType.Inventory, hotkeyPickupable, false, true))
                            {
                                Pop(storage, hotkeyPickupable, false, true);
                                Push(storage, StorageType.Inventory, hotkeyPickupable, false, true);
                            }
                            else
                            {
                                if (!isCurrentObject && existingHotkeyPickupable.IsNullOrDestroyed())
                                {
                                    Push(storage, StorageType.Inventory, obj, false, true);
                                }
                                _viewRef(__instance)?.OnAssignmentFailed();
                                return false;
                            }
                        }

                        if (!existingHotkeyPickupable.IsNullOrDestroyed())
                        {
                            obj = existingHotkeyPickupable;
                            Pop(storage, obj, false, true);
                        }

                        if (existingHotkeyData != null)
                        {
                            ClearHotkey.Invoke(__instance, new object[] { existingHotkeyData });
                        }

                        _hotkeysRef(__instance)[number].ReferenceId = obj.ReferenceId;
                        _hotkeysRef(__instance)[number].CraftingType.Value = obj.CraftingType;

                        if (!isCurrentObject || !existingHotkeyPickupable.IsNullOrDestroyed())
                        {
                            Push(storage, StorageType.Hotkeys, obj, false, false);
                            storage.SortSlots();
                        }

                        IList<IList<InventoryData>> inventoryData = GetInventoryData.Invoke(_inventoryRadialMenuRef(__instance).Inventory, null) as IList<IList<InventoryData>>;
                        _inventoryRadialMenuRef(__instance).Initialize(inventoryData);
                        _inventoryRadialMenuRef(__instance).Refresh();
                        _viewRef(__instance)?.OnAssignmentCreated();
                    }

                    _iconRef(kbHotkeyElements[number]).Color = Color.white;
                    if (number < dpHotkeyElements.Count) _iconRef(dpHotkeyElements[number]).Color = Color.white;

                    return false;
                }
                catch (Exception e)
                {
                    logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(HotkeyController), nameof(HotkeyController.Save))]
        private class HotkeyController_Save_Patch
        {
            private static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");
            private static readonly AccessTools.FieldRef<HotkeyController, int> _levelRef = AccessTools.FieldRefAccess<HotkeyController, int>("_level");

            private static bool Prefix(HotkeyController __instance, ref JObject __result)
            {
                if (!enabled) return true;

                try
                {
                    JObject jobject = new JObject();
                    jobject.AddField("Level", _levelRef(__instance));
                    JObject jobject2 = new JObject();
                    for (int i = 0; i < _hotkeysRef(__instance).Length; i++)
                    {
                        HotkeyData hotkeyData = _hotkeysRef(__instance)[i];
                        JObject jobject3 = new JObject();
                        jobject3.AddField("Locked", hotkeyData.Locked.Value);
                        jobject3.AddField("ReferenceId", hotkeyData.ReferenceId.ToString());
                        jobject3.AddField("CraftingType", hotkeyData.CraftingType.Value.Save());
                        jobject2.Add(jobject3);
                    }
                    jobject.AddField("Hotkeys", jobject2);
                    __result = jobject;
                    return false;
                }
                catch (Exception e)
                {
                    logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(HotkeyController), nameof(HotkeyController.Load))]
        private class HotkeyController_Load_Patch
        {
            private static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");
            private static readonly AccessTools.FieldRef<HotkeyController, IPlayer> _playerRef = AccessTools.FieldRefAccess<HotkeyController, IPlayer>("_player");
            private static readonly AccessTools.FieldRef<HotkeyController, int> _levelRef = AccessTools.FieldRefAccess<HotkeyController, int>("_level");

            private static readonly AccessTools.FieldRef<SlotStorage, List<StorageSlot<IPickupable>>> _slotDataRef = AccessTools.FieldRefAccess<SlotStorage, List<StorageSlot<IPickupable>>>("_slotData");

            private static readonly MethodInfo UpdateLevel = AccessTools.Method(typeof(HotkeyController), "UpdateLevel");

            private static bool Prefix(HotkeyController __instance, JObject data)
            {
                if (!enabled) return true;

                try
                {
                    _levelRef(__instance) = data.GetField("Level").GetValue<int>();
                    UpdateLevel.Invoke(__instance, null);

                    JObject hotkeysData = data.GetField("Hotkeys");
                    Timing.RunCoroutine(DeferredLoad(__instance, hotkeysData));
                    return false;
                }
                catch (Exception e)
                {
                    logger.LogException(e);
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

                for (int i = 0; i < hotkeysData.Children.Count; i++)
                {
                    JObject jobject = hotkeysData.Children[i];
                    bool value = jobject.GetField("Locked").GetValue<bool>();
                    if (value)
                    {
                        break;
                    }

                    _hotkeysRef(__instance)[i].Locked.Value = value;

                    MiniGuid miniGuid = jobject.GetField("ReferenceId").GetValue<string>().ToMiniGuid();
                    CraftingType craftingType = new CraftingType(AttributeType.None, InteractiveType.None);
                    JObject craftingTypeData = jobject.GetField("CraftingType");

                    if (craftingTypeData != null && !craftingTypeData.IsNull())
                    {
                        craftingType.Load(craftingTypeData);
                    }

                    if (!miniGuid.IsDefault())
                    {
                        IPickupable pickupable = list.FirstOrDefault_NonAlloc((IPickupable o, MiniGuid referenceId) => o.ReferenceId.Equals(referenceId), miniGuid);

                        if (!pickupable.IsNullOrDestroyed())
                        {
                            _hotkeysRef(__instance)[i].ReferenceId = pickupable.ReferenceId;
                            _hotkeysRef(__instance)[i].CraftingType.Value = pickupable.CraftingType;

                            _iconRef(kbHotkeyElements[i]).Color = Color.white;
                            if (i < dpHotkeyElements.Count) _iconRef(dpHotkeyElements[i]).Color = Color.white;

                            if (!pickupable.Equals(_playerRef(__instance).Holder.CurrentObject))
                            {
                                Pop(storage, pickupable, true, false);
                                hotkeyPickupables.Add(i, pickupable);
                            }
                        }
                        else if (settings.rememberToolbelt)
                        {
                            _hotkeysRef(__instance)[i].ReferenceId = miniGuid;
                            _hotkeysRef(__instance)[i].CraftingType.Value = craftingType;

#if DEBUG
                            _iconRef(kbHotkeyElements[i]).Color = new Color(settings.albedo, settings.albedo, settings.albedo, settings.a);
                            if (i < dpHotkeyElements.Count) _iconRef(dpHotkeyElements[i]).Color = new Color(settings.albedo, settings.albedo, settings.albedo, settings.a);
#else
                            _iconRef(kbHotkeyElements[i]).Color = new Color(0.4f, 0.4f, 0.4f, 0.65f);
                            if (i < dpHotkeyElements.Count) _iconRef(dpHotkeyElements[i]).Color = new Color(0.4f, 0.4f, 0.4f, 0.65f);
#endif
                        }
                    }
                }

                foreach (KeyValuePair<int, IPickupable> pair in hotkeyPickupables)
                {
                    HotkeyData hotkeyData = _hotkeysRef(__instance)[pair.Key];
                    IPickupable pickupable = pair.Value;

                    if (_slotDataRef(storage)[pair.Key + 10].Objects.Count > 0)
                    {
                        logger.Critical(string.Format("An item is already occupying hotkey {0} meaning the hotkey item will not be loaded correctly. This means you can lose some of your items if you save. Please report this issue to the developer.", hotkeyData.Number));
                        continue;
                    }

                    Push(storage, StorageType.Hotkeys, pickupable, false, true);
                }
                yield break;
            }
        }

        [HarmonyPatch(typeof(HotkeyController), "UpdateLevel")]
        private class HotkeyController_UpdateLevel_Patch
        {
            private static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");
            private static readonly AccessTools.FieldRef<HotkeyController, IPlayer> _playerRef = AccessTools.FieldRefAccess<HotkeyController, IPlayer>("_player");

            private static readonly AccessTools.FieldRef<SlotStorage, List<StorageSlot<IPickupable>>> _slotDataRef = AccessTools.FieldRefAccess<SlotStorage, List<StorageSlot<IPickupable>>>("_slotData");
            private static readonly AccessTools.FieldRef<SlotStorage, List<StorageSlot<IPickupable>>> _tempRef = AccessTools.FieldRefAccess<SlotStorage, List<StorageSlot<IPickupable>>>("_temp");
            private static readonly AccessTools.FieldRef<SlotStorage, int> _slotCountRef = AccessTools.FieldRefAccess<SlotStorage, int>("_slotCount");

            private static void Postfix(HotkeyController __instance)
            {
                if (!enabled) return;

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
                    logger.LogException(e);
                }
            }
        }

        public class InventoryHotkeyComparer : IComparer<StorageSlot<IPickupable>>
        {
            private readonly AccessTools.FieldRef<StorageSlot<IPickupable>, int> _indexRef = AccessTools.FieldRefAccess<StorageSlot<IPickupable>, int>("_index");

            public int Compare(StorageSlot<IPickupable> a, StorageSlot<IPickupable> b)
            {
                int indexA = _indexRef(a) - (_indexRef(a) < 10 ? 0 : 20);
                int indexB = _indexRef(b) - (_indexRef(b) < 10 ? 0 : 20);

                if (b == null)
                {
                    return 1;
                }
                if (indexA > indexB)
                {
                    return 1;
                }
                if (indexA < indexB)
                {
                    return -1;
                }
                return 0;
            }
        }
    }
}
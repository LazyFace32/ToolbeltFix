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
using Beam.Events;
#if DEBUG
using Beam.Developer.UI;
using Beam.Developer;
using Beam.Serialization;
using UnityEngine.UI;
#endif

namespace ToolbeltFix
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        [Draw("Remember Toolbelt", Height = 15, Tooltip = "   Remember the item type stored in a toolbelt slot even if the item breaks or is dropped.")]
        public bool rememberToolbelt = false;
        [Draw("Always Show Hotkey Bar", Height = 15)]
        public bool alwaysShowHotkeyBar = false;
        [Draw("Stack Transfer", Height = 15, Tooltip = "   Hold right click on a stack of items to transfer it from one storage to another.")]
        public bool stackTransfer = false;
        [Draw("Quick Drop", Height = 15, Tooltip = "   Right click an item in your inventory to drop it.")]
        public bool quickDrop = true;

        [Space(10)]
        [Header("Cheats")]
        [Draw("Allow Container Crates", Height = 15)] public bool allowContainerCrates = false;
        //[Draw("Allow Large Items (Cheat)", Height = 15)] public bool allowLargeItems = false;
        [Draw("Allow Stackable Toolbelt Slots", Height = 15)] public bool allowStackableToolbeltSlots = false;

#if DEBUG
        [Space(10)]
        [Header("Debug")]
        [Draw("Grey", Min = 0, Max = 1, Width = 400, Type = DrawType.Slider)] public float albedo = 0.4f;
        [Draw("Alpha", Min = 0, Max = 1, Width = 400, Type = DrawType.Slider)] public float alpha = 0.65f;

        [Space(10)]
        [Draw("Simulate Release OnToggle", Height = 15)] public bool simulateReleaseOnToggle = false;
        [Draw("Max Displayed Items", Min = 0, Max = 50, Width = 400, Type = DrawType.Slider)] public int maxDisplayedItems = 4;
        [Draw("Drop Overflow Items", Height = 15)] public bool dropOverflowItems = false;
#endif

        private bool alwaysShowHotkeyBarOld;

        public void Load()
        {
            alwaysShowHotkeyBarOld = alwaysShowHotkeyBar;

#if DEBUG
            Main.hotkeyElementEmptyColor = new Color(albedo, albedo, albedo, alpha);
#endif
        }

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

        private static readonly Dictionary<StorageRadialMenuPresenter, StorageMenuPresenter> storagePresenters = new Dictionary<StorageRadialMenuPresenter, StorageMenuPresenter>();

#if DEBUG
        [SaveOnReload]
        public static GameObject _quantityGroupPrefab;

        [SaveOnReload]
        public static readonly Dictionary<HotkeyElementView, GameObject> _quantityGroup = new Dictionary<HotkeyElementView, GameObject>();
        [SaveOnReload]
        public static readonly Dictionary<HotkeyElementView, ILabelViewAdapter> _quantityLabel = new Dictionary<HotkeyElementView, ILabelViewAdapter>();

        [SaveOnReload]
        public static readonly Dictionary<ISlotStorage<IPickupable>, HotkeyController> slotStorage_HotkeyController = new Dictionary<ISlotStorage<IPickupable>, HotkeyController>();

        [SaveOnReload]
        public static readonly Dictionary<HotkeyData, ICollection<MiniGuid>> _occupied = new Dictionary<HotkeyData, ICollection<MiniGuid>>();
        [SaveOnReload]
        public static readonly Dictionary<HotkeyData, LinkedList<MiniGuid>> _reserved = new Dictionary<HotkeyData, LinkedList<MiniGuid>>();
        [SaveOnReload]
        public static readonly Dictionary<HotkeyData, Property<bool>> _rememberHotkey = new Dictionary<HotkeyData, Property<bool>>();
        [SaveOnReload]
        public static readonly Dictionary<HotkeyData, Property<int>> _quantity = new Dictionary<HotkeyData, Property<int>>();

        [SaveOnReload]
        public static EventManager.EventDelegate<QuickAccessSlotUnlockedEvent> EventManager_QuickAccessSlotUnlocked_Event;
        [SaveOnReload]
        public static EventManager.EventDelegate<OptionsAppliedEvent> EventManager_OptionsApplied_Event;
#else
        private static GameObject _quantityGroupPrefab;

        private static readonly Dictionary<HotkeyElementView, GameObject> _quantityGroup = new Dictionary<HotkeyElementView, GameObject>();
        private static readonly Dictionary<HotkeyElementView, ILabelViewAdapter> _quantityLabel = new Dictionary<HotkeyElementView, ILabelViewAdapter>();

        private static readonly Dictionary<ISlotStorage<IPickupable>, HotkeyController> slotStorage_HotkeyController = new Dictionary<ISlotStorage<IPickupable>, HotkeyController>();

        private static readonly Dictionary<HotkeyData, ICollection<MiniGuid>> _occupied = new Dictionary<HotkeyData, ICollection<MiniGuid>>();
        private static readonly Dictionary<HotkeyData, LinkedList<MiniGuid>> _reserved = new Dictionary<HotkeyData, LinkedList<MiniGuid>>();
        private static readonly Dictionary<HotkeyData, Property<bool>> _rememberHotkey = new Dictionary<HotkeyData, Property<bool>>();
        private static readonly Dictionary<HotkeyData, Property<int>> _quantity = new Dictionary<HotkeyData, Property<int>>();

        private static EventManager.EventDelegate<QuickAccessSlotUnlockedEvent> EventManager_QuickAccessSlotUnlocked_Event;
        private static EventManager.EventDelegate<OptionsAppliedEvent> EventManager_OptionsApplied_Event;
#endif

        internal static Color hotkeyElementEmptyColor = new Color(0.4f, 0.4f, 0.4f, 0.65f);
        internal static Color hotkeyElementColor = Color.white;

        private static CoroutineHandle notificationHandler;
        private static string originalNotificationMessage;
        private static bool silentSlotStorageTransfer;
        private static bool worldLoaded;

        private static InteractiveType InteractiveType_CONTAINER;

        private static HotkeyComparer HotkeyComparison { get; } = new HotkeyComparer();

        protected static UnityModManager.ModEntry.ModLogger Logger { get; private set; }
        protected static Settings Settings { get; private set; }
        protected static bool Enabled { get; private set; }

#if DEBUG
        private static Harmony harmony;
#endif

#if DEBUG
        private static Vector2 referenceResolution = new Vector2(1280f, 720f);
        private static GameObject canvas;
        private static Text[] currentObjectTexts;
        private static Text[] storageTexts;
        private static Text[] hotkeyTexts;

        [SaveOnReload]
        public static bool canvasActive;
        private static Font font;
#endif

        internal static bool Load(UnityModManager.ModEntry modEntry)
        {
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            Logger = modEntry.Logger;

            Settings.Load();

#if DEBUG
            harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
#else
            Harmony harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
#endif

            modEntry.OnUpdate = OnUpdate;
            modEntry.OnToggle = OnToggle;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnGUI = OnGUI;

#if DEBUG
            modEntry.OnUnload = Unload;

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif

            VersionChecker.CheckVersion(modEntry);

            if (Enum.TryParse("CONTAINER", out InteractiveType CONTAINER))
            {
                InteractiveType_CONTAINER = CONTAINER;
            }
            else
            {
                InteractiveType_CONTAINER = InteractiveType.CONTAINER;
                Logger.Error("Could not load the container crate type. This could have unexpected side effects.");
            }

            return true;
        }

#if DEBUG
        internal static bool Unload(UnityModManager.ModEntry modEntry)
        {
            harmony.UnpatchAll(modEntry.Info.Id);
            return true;
        }
#endif

#if DEBUG
        private static readonly AccessTools.FieldRef<Property<int>, Action<int>> ValueChangedRef = AccessTools.FieldRefAccess<Property<int>, Action<int>>("ValueChanged");
        private static readonly AccessTools.FieldRef<HotkeyController, HotkeyView> _viewRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyView>("_view");

        private static readonly AccessTools.FieldRef<PlatformHotkeyViewProvider, HotkeyView> _kbViewRef = AccessTools.FieldRefAccess<PlatformHotkeyViewProvider, HotkeyView>("_kbView");
        private static readonly AccessTools.FieldRef<PlatformHotkeyViewProvider, HotkeyView> _dpViewRef = AccessTools.FieldRefAccess<PlatformHotkeyViewProvider, HotkeyView>("_dpView");

        private static readonly AccessTools.FieldRef<UHotkeyView, IList<HotkeyElementView>> _elementsRef = AccessTools.FieldRefAccess<UHotkeyView, IList<HotkeyElementView>>("_elements");

        private static readonly AccessTools.FieldRef<SlotStorage, StorageSlot<IPickupable>> _selectedSlotRef = AccessTools.FieldRefAccess<SlotStorage, StorageSlot<IPickupable>>("_selectedSlot");
#endif

        internal static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
            try
            {
                if (StrandedWorld.Instance && PlayerRegistry.AllPlayers.Count > 0 && !worldLoaded)
                {
                    worldLoaded = true;

#if DEBUG
                    InitCanvas();
                    canvas.SetActive(canvasActive);

                    if (storagePresenters.Count == 0)
                    {
                        AccessTools.FieldRef<StorageMenuPresenter, StorageRadialMenuPresenter> _radialPresenterRef = AccessTools.FieldRefAccess<StorageMenuPresenter, StorageRadialMenuPresenter>("_radialPresenter");
                        StorageMenuPresenter[] storageMenuPresenters = UnityEngine.Object.FindObjectsOfType<StorageMenuPresenter>();

                        foreach (StorageMenuPresenter storagePresenter in storageMenuPresenters)
                        {
                            storagePresenters.Add(_radialPresenterRef(storagePresenter), storagePresenter);
                        }
                    }

                    foreach (HotkeyElementView hotkeyElementView in _elementsRef(_kbViewRef(_viewRef(PlayerRegistry.LocalPlayer.Hotkeys) as PlatformHotkeyViewProvider) as UHotkeyView))
                    {
                        UnityEngine.Object.Destroy(_quantityGroup[hotkeyElementView]);
                        CreateQuantityGroup(hotkeyElementView);
                    }

                    foreach (HotkeyElementView hotkeyElementView in _elementsRef(_dpViewRef(_viewRef(PlayerRegistry.LocalPlayer.Hotkeys) as PlatformHotkeyViewProvider) as UHotkeyView))
                    {
                        UnityEngine.Object.Destroy(_quantityGroup[hotkeyElementView]);
                        CreateQuantityGroup(hotkeyElementView);
                    }

                    foreach (HotkeyData hotkeyData in _hotkeysRef(PlayerRegistry.LocalPlayer.Hotkeys))
                    {
                        if (hotkeyData.Locked.Value) break;

                        ValueChangedRef(_quantity[hotkeyData])?.Invoke(_occupied[hotkeyData].Count);
                    }
#endif
                }
                else if ((!StrandedWorld.Instance || PlayerRegistry.AllPlayers.Count == 0) && worldLoaded)
                {
                    _quantityGroup.Clear();
                    _quantityLabel.Clear();

                    worldLoaded = false;
                }

#if DEBUG
                if (!worldLoaded) return;

                IPlayer player = PlayerRegistry.LocalPlayer;
                HotkeyController hotkeyController = player.Hotkeys;
                Holder holder = player.Holder;

                SlotStorage storage = player.Inventory.GetSlotStorage() as SlotStorage;
                List<StorageSlot<IPickupable>> _slotData = AccessTools.Field(typeof(SlotStorage), "_slotData").GetValue(storage) as List<StorageSlot<IPickupable>>;

                if (!IsConsoleVisible())
                {
                    if (Input.GetKeyDown(KeyCode.M))
                    {
                        canvas.SetActive(!canvas.activeInHierarchy);
                        canvasActive = canvas.activeInHierarchy;
                    }
                }

                // i, type, count, reference Id
                // i, si, type, count, reference Ids
                // i, type, locked, isOccupied, reference Id

                foreach (Text text in currentObjectTexts)
                    text.text = string.Empty;

                currentObjectTexts[0].text += string.Format("i: {0}\n", 0);
                currentObjectTexts[1].text += string.Format("Type: {0}\n", holder.CurrentObject?.CraftingType.InteractiveType);
                currentObjectTexts[2].text += string.Format("Count: {0}\n", holder.CurrentObject.IsNullOrDestroyed() ? 0 : 1);
                currentObjectTexts[3].text += string.Format("ReferenceId: {0}\n", MiniGuidToString(holder.CurrentObject?.ReferenceId));


                foreach (Text text in storageTexts)
                    text.text = string.Empty;

                for (int i = 0; i < storage.SlotCount; i++)
                {
                    storageTexts[0].text += string.Format("i: {0}\n", i);
                    storageTexts[1].text += string.Format("idx: {0}\n", _indexRef(_slotData[i]));
                    storageTexts[2].text += string.Format("Type: {0}\n", _slotData[i].CraftingType.InteractiveType);
                    storageTexts[3].text += string.Format("Count: {0}\n", _slotData[i].Objects.Count);

                    IEnumerable<IPickupable> items = _slotData[i].Objects.Take(Settings.maxDisplayedItems);

                    storageTexts[4].text += string.Format("ReferenceId: {0}\n", items.Join(item => MiniGuidToString(item.ReferenceId), ", ") + (items.Count() < _slotData[i].Objects.Count ? "..." : ""));
                }

                storageTexts[0].text += string.Format("\n\nSelected: {0}", _selectedSlotRef(storage) != null ? _indexRef(_selectedSlotRef(storage)) : -1);
                storageTexts[2].text += string.Format("\n\nCount: {0}", _selectedSlotRef(storage)?.Objects.Count);


                foreach (Text text in hotkeyTexts)
                    text.text = string.Empty;

                for (int i = 0; i < _hotkeysRef(hotkeyController).Length; i++)
                {
                    HotkeyData hotkeyData = _hotkeysRef(hotkeyController)[i];

                    hotkeyTexts[0].text += string.Format("i: {0}\n", hotkeyData.Number);
                    if (i + 10 < storage.SlotCount)
                    {
                        hotkeyTexts[1].text += string.Format("idx: {0}\n", _indexRef(_slotData[i + 10]));
                    }
                    hotkeyTexts[2].text += string.Format("Type: {0}\n", hotkeyData.CraftingType.Value.InteractiveType);
                    hotkeyTexts[3].text += string.Format("Locked: {0}\n", hotkeyData.Locked.Value);

                    for (int j = 0; j < hotkeyTexts.Length - 1; j++)
                    {
                        hotkeyTexts[j].text += "\n";
                    }

                    IEnumerable<string> occupied = _occupied[hotkeyData].Take(Settings.maxDisplayedItems).Select(id => MiniGuidToString(id));
                    IEnumerable<string> reserved = _reserved[hotkeyData].Take(Settings.maxDisplayedItems).Select(id => MiniGuidToString(id));

                    hotkeyTexts[4].text += string.Format("Occupied: {0}\n", occupied.Join() + (occupied.Count() < _occupied[hotkeyData].Count ? "..." : ""));
                    hotkeyTexts[4].text += string.Format("Reserved: {0}\n", reserved.Join() + (reserved.Count() < _reserved[hotkeyData].Count ? "..." : ""));
                }
#endif
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

#if DEBUG
        private static readonly AccessTools.FieldRef<TestingConsole, bool> TestingConsole_openRef = AccessTools.FieldRefAccess<TestingConsole, bool>("_open");

        private static bool IsConsoleVisible()
        {
            return DeveloperConsolePresenter.Instance && DeveloperConsolePresenter.Instance.IsOpen ||
                Singleton<TestingConsole>.Instance && TestingConsole_openRef(Singleton<TestingConsole>.Instance);
        }

        private static string MiniGuidToString(MiniGuid? miniGuid)
        {
            if (miniGuid == null) return null;
            string str = miniGuid.ToString();

            return str.Substring(0, str.LastIndexOf("-"));
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

        internal static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.Draw(modEntry);

            GUILayout.Space(10);
            GUILayout.Label("Created by Lazy");
        }

#if DEBUG
        internal static void ApplySettings()
        {
            if (!worldLoaded) return;

            IPlayer player = PlayerRegistry.LocalPlayer;
            SlotStorage storage = player.Inventory.GetSlotStorage() as SlotStorage;
            IPickupable currentObject = player.Holder.CurrentObject;
            HotkeyController hotkeyController = player.Hotkeys;

            hotkeyElementEmptyColor = new Color(Settings.albedo, Settings.albedo, Settings.albedo, Settings.alpha);

            for (int i = 10; i < _slotDataRef(storage).Count; i++)
            {
                StorageSlot<IPickupable> hotkeySlot = _slotDataRef(storage)[i];
                HotkeyData hotkeyData = _hotkeysRef(hotkeyController)[i - 10];

                if (_occupied[hotkeyData].Count == 0 && _reserved[hotkeyData].Count > 0 && Settings.rememberToolbelt)
                {
                    _rememberHotkey[hotkeyData].Value = true;
                }
                else
                {
                    _rememberHotkey[hotkeyData].Value = false;
                }
            }

            if (!currentObject.IsNullOrDestroyed() && CanPush(storage, StorageType.Hotkeys, currentObject, false, false))
            {
                StorageSlot<IPickupable> slot = GetSlot(storage, StorageType.Hotkeys, currentObject, false);

                if (slot != null)
                {
                    HotkeyData hotkeyData = _hotkeysRef(hotkeyController)[_indexRef(slot) - 10];

                    _rememberHotkey[hotkeyData].Value = false;
                }
            }
        }
#endif

        internal static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.Save(modEntry);
        }

        internal static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
#if DEBUG
            if (!value && Settings.simulateReleaseOnToggle)
            {
                if (notificationHandler != null)
                {
                    Timing.KillCoroutines(notificationHandler);
                    modEntry.CustomRequirements = originalNotificationMessage;
                }

                originalNotificationMessage = modEntry.CustomRequirements;
                notificationHandler = Timing.RunCoroutine(ToggleFailedNotification(modEntry), Segment.RealtimeUpdate);
                modEntry.Enabled = !value;
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
                if (notificationHandler != null)
                {
                    Timing.KillCoroutines(notificationHandler);
                    modEntry.CustomRequirements = originalNotificationMessage;
                }

                originalNotificationMessage = modEntry.CustomRequirements;
                notificationHandler = Timing.RunCoroutine(ToggleFailedNotification(modEntry), Segment.RealtimeUpdate);
                modEntry.Enabled = !value;
                return false;
            }
#endif

            if (value)
            {
                Options.GeneralSettings.AlwaysShowHotkeyBar = Settings.alwaysShowHotkeyBar;
            }
            else
            {
                if (notificationHandler != null)
                {
                    Timing.KillCoroutines(notificationHandler);
                    modEntry.CustomRequirements = originalNotificationMessage;
                }

                Options.GeneralSettings.AlwaysShowHotkeyBar = false;
            }

            Enabled = value;
            return true;
        }

        internal static IEnumerator<float> ToggleFailedNotification(UnityModManager.ModEntry modEntry)
        {
            modEntry.CustomRequirements = "Can only toggle from the main menu";

            yield return Timing.WaitForSeconds(5f);
            modEntry.CustomRequirements = originalNotificationMessage;
        }

#if DEBUG
        private static void InitCanvas()
        {
            canvas = CreateCanvas();

            int fontSize = 11;

            currentObjectTexts = new Text[4];
            currentObjectTexts[0] = AddText(string.Empty, fontSize, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f));
            currentObjectTexts[1] = AddText(string.Empty, fontSize, new Vector2(0.04f, 0.02f), new Vector2(0.98f, 0.98f));
            currentObjectTexts[2] = AddText(string.Empty, fontSize, new Vector2(0.16f, 0.02f), new Vector2(0.98f, 0.98f));
            currentObjectTexts[3] = AddText(string.Empty, fontSize, new Vector2(0.225f, 0.02f), new Vector2(0.98f, 0.98f));

            storageTexts = new Text[5];
            storageTexts[0] = AddText(string.Empty, fontSize, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.94f));
            storageTexts[1] = AddText(string.Empty, fontSize, new Vector2(0.045f, 0.02f), new Vector2(0.98f, 0.94f));
            storageTexts[2] = AddText(string.Empty, fontSize, new Vector2(0.082f, 0.02f), new Vector2(0.98f, 0.94f));
            storageTexts[3] = AddText(string.Empty, fontSize, new Vector2(0.205f, 0.02f), new Vector2(0.98f, 0.94f));
            storageTexts[4] = AddText(string.Empty, fontSize, new Vector2(0.25f, 0.02f), new Vector2(0.98f, 0.94f));

            hotkeyTexts = new Text[5];
            hotkeyTexts[0] = AddText(string.Empty, fontSize, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.65f));
            hotkeyTexts[1] = AddText(string.Empty, fontSize, new Vector2(0.045f, 0.02f), new Vector2(0.98f, 0.65f));
            hotkeyTexts[2] = AddText(string.Empty, fontSize, new Vector2(0.082f, 0.02f), new Vector2(0.98f, 0.65f));
            hotkeyTexts[3] = AddText(string.Empty, fontSize, new Vector2(0.205f, 0.02f), new Vector2(0.98f, 0.65f));
            hotkeyTexts[4] = AddText(string.Empty, fontSize, new Vector2(0.27f, 0.02f), new Vector2(0.98f, 0.65f));
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
            textObject.transform.localPosition = Vector3.zero;
            textObject.transform.localScale = Vector3.one;

            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.text = txt;
            text.fontSize = fontSize;

            // Text position
            text.rectTransform.anchorMin = anchorMin;
            text.rectTransform.anchorMax = anchorMax;
            text.rectTransform.offsetMin = new Vector2();
            text.rectTransform.offsetMax = new Vector2();

            text.rectTransform.offsetMin = Vector3.zero;
            text.rectTransform.offsetMax = Vector3.zero;

            return text;
        }
#endif

        private static IEnumerable<StorageSlot<IPickupable>> GetSlots(SlotStorage __instance, params IComparer<StorageSlot<IPickupable>>[] comparators)
        {
            for (int j = 0; j < _slotDataRef(__instance).Count; j++)
            {
                _tempRef(__instance)[j] = _slotDataRef(__instance)[j];
            }
            foreach (IComparer<StorageSlot<IPickupable>> comparer in comparators)
            {
                _tempRef(__instance).Sort(comparer);
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
            if (!_storeOtherStorageRef(__instance) && pickupable.CraftingType.InteractiveType == InteractiveType_CONTAINER)
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

        private static void Pop(SlotStorage __instance, IPickupable pickupable, bool sort = true, bool allowClearHotkey = true)
        {
            StorageSlot<IPickupable> storageSlot = __instance.FindSlot(pickupable);
            if (storageSlot == null)
            {
                return;
            }

            pickupable.Release();
            storageSlot.Objects.Remove(pickupable);

            if (_indexRef(storageSlot) > 9 && allowClearHotkey && slotStorage_HotkeyController.TryGetValue(__instance, out HotkeyController hotkeyController))
            {
                HotkeyData hotkeyData = _hotkeysRef(hotkeyController)[_indexRef(storageSlot) - 10];

                _occupied[hotkeyData].Remove(pickupable.ReferenceId);

                _quantity[hotkeyData].Value = _occupied[hotkeyData].Count;

                if (_occupied[hotkeyData].Count == 0)
                {
                    if (Settings.rememberToolbelt)
                    {
                        _rememberHotkey[hotkeyData].Value = true;
                    }
                    else
                    {
                        ClearHotkey(hotkeyData);
                    }
                }
            }
            if (storageSlot.Objects.Count == 0)
            {
                storageSlot.CraftingType = new CraftingType(AttributeType.None, InteractiveType.None);
                if (sort)
                {
                    __instance.SortSlots();
                }
            }
            OnPopped.Invoke(__instance, new object[] { pickupable });
            _modifiedRef(__instance) = true;
        }

        private static bool Push(SlotStorage __instance, StorageType storageType, IPickupable pickupable, bool force)
        {
            if (CanPush(__instance, storageType, pickupable, force, true))
            {
                StorageSlot<IPickupable> slot = GetSlot(__instance, storageType, pickupable, true);
                if (slot != null)
                {
                    if (_indexRef(slot) > 9 && !_loadStateRef(__instance).IsLoading() && slotStorage_HotkeyController.TryGetValue(__instance, out HotkeyController hotkeyController))
                    {
                        HotkeyData hotkeyData = _hotkeysRef(hotkeyController)[_indexRef(slot) - 10];

                        _occupied[hotkeyData].Add(pickupable.ReferenceId);

                        _reserved[hotkeyData].Remove(pickupable.ReferenceId);
                        _reserved[hotkeyData].AddLast(pickupable.ReferenceId);

                        while (_reserved[hotkeyData].Count > (int)GetStackSize.Invoke(__instance, new object[] { slot.CraftingType }))
                        {
                            _reserved[hotkeyData].RemoveFirst();
                        }

                        _quantity[hotkeyData].Value = _occupied[hotkeyData].Count;
                        _rememberHotkey[hotkeyData].Value = false;
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
                    OnPushed.Invoke(__instance, new object[] { pickupable, true });
                    pickupable.Store();
                    return true;
                }
            }
            return false;
        }

        private static StorageSlot<IPickupable> GetSlot(SlotStorage __instance, StorageType storageType, IPickupable pickupable, bool assign = true)
        {
            if (storageType != StorageType.Inventory && slotStorage_HotkeyController.TryGetValue(__instance, out HotkeyController hotkeyController))
            {
                for (int i = 10; i < _slotDataRef(__instance).Count; i++)
                {
                    StorageSlot<IPickupable> storageSlot = _slotDataRef(__instance)[i];
                    HotkeyData hotkeyData = _hotkeysRef(hotkeyController)[i - 10];
                    CraftingType craftingType = hotkeyData.CraftingType.Value;

                    if (_occupied[hotkeyData].Contains(pickupable.ReferenceId) || _occupied[hotkeyData].Count < GetHotkeyStackSize(__instance, craftingType) && _reserved[hotkeyData].Contains(pickupable.ReferenceId))
                    {
                        if (assign)
                        {
                            storageSlot.CraftingType = new CraftingType(pickupable.CraftingType.AttributeType, pickupable.CraftingType.InteractiveType);
                        }
                        return storageSlot;
                    }
                }

                if (!_loadStateRef(__instance).IsLoading())
                {
                    for (int i = 10; i < _slotDataRef(__instance).Count; i++)
                    {
                        StorageSlot<IPickupable> storageSlot = _slotDataRef(__instance)[i];
                        HotkeyData hotkeyData = _hotkeysRef(hotkeyController)[i - 10];
                        CraftingType craftingType = hotkeyData.CraftingType.Value;

                        if (_occupied[hotkeyData].Count < GetHotkeyStackSize(__instance, craftingType) && (bool)CheckTypesMatch.Invoke(__instance, new object[] { craftingType, pickupable.CraftingType.InteractiveType, pickupable.CraftingType.AttributeType }))
                        {
                            if (assign)
                            {
                                storageSlot.CraftingType = new CraftingType(pickupable.CraftingType.AttributeType, pickupable.CraftingType.InteractiveType);
                            }
                            return storageSlot;
                        }
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
            for (int j = 0; j < 10; j++)
            {
                StorageSlot<IPickupable> storageSlot2 = _slotDataRef(__instance)[j];
                if (storageSlot2.Objects.Count == 0)
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

        private static int GetHotkeyStackSize(SlotStorage __instance, CraftingType type)
        {
            if (Settings.allowStackableToolbeltSlots)
            {
                return (int)GetStackSize.Invoke(__instance, new object[] { type });
            }
            else
            {
                return 1;
            }
        }

        private static void ClearHotkey(HotkeyData hotkeyData)
        {
            hotkeyData.CraftingType.Value = CraftingType.Empty;
            _rememberHotkey[hotkeyData].Value = false;
            _quantity[hotkeyData].Value = 0;

            _occupied[hotkeyData].Clear();
            _reserved[hotkeyData].Clear();
        }

        private static void CreateQuantityGroup(HotkeyElementView __instance)
        {
            _quantityGroup[__instance] = UnityEngine.Object.Instantiate(_quantityGroupPrefab, __instance.transform);
            _quantityGroup[__instance].transform.localScale = Vector3.one * 0.7f;

            _quantityGroup[__instance].transform.Find("Image - Navigate Left").gameObject.SetActive(false);
            _quantityGroup[__instance].transform.Find("Image - Navigate Right").gameObject.SetActive(false);
            _quantityLabel[__instance] = _quantityGroup[__instance].transform.Find("Label - Quantity").GetComponent<TMPLabelViewAdapter>();

            RectTransform rectTransform = _quantityGroup[__instance].GetComponent<RectTransform>();
            rectTransform.anchoredPosition = new Vector2(0, -rectTransform.sizeDelta.y * 0.8f);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rectTransform.sizeDelta.x * 0.74f);

            _quantityGroup[__instance].SetActive(false);
        }

        [HarmonyPatch(typeof(GeneralSettings), nameof(GeneralSettings.Load))]
        private class GeneralSettings_Load_Patch
        {
            private static void Postfix(GeneralSettings __instance)
            {
                if (!Enabled) return;

                try
                {
                    __instance.AlwaysShowHotkeyBar = Settings.alwaysShowHotkeyBar;
                }
                catch (Exception e)
                {
                    Logger.LogException(e);
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
                if (!Enabled) return true;

                try
                {
                    foreach (StorageSlot<IPickupable> storageSlot in GetSlots(_playerRef(__instance).Inventory.GetSlotStorage() as SlotStorage, StorageSlot<IPickupable>.QuantityComparison, HotkeyComparison))
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
                    Logger.LogException(e);
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
                if (!Enabled) return true;

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

                    Pop(storage, pickupable, true, false);
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
                    if (CanPush(storage, StorageType.Hotkeys, pickupable, false, false))
                    {
                        StorageSlot<IPickupable> slot = GetSlot(storage, StorageType.Hotkeys, pickupable, false);

                        if (slot != null && slotStorage_HotkeyController.TryGetValue(storage, out HotkeyController hotkeyController))
                        {
                            HotkeyData hotkeyData = _hotkeysRef(hotkeyController)[_indexRef(slot) - 10];
                            CraftingType craftingType = hotkeyData.CraftingType.Value;

                            _occupied[hotkeyData].Add(pickupable.ReferenceId);

                            _reserved[hotkeyData].Remove(pickupable.ReferenceId);
                            _reserved[hotkeyData].AddLast(pickupable.ReferenceId);

                            while (_reserved[hotkeyData].Count > (int)GetStackSize.Invoke(storage, new object[] { craftingType }))
                            {
                                _reserved[hotkeyData].RemoveFirst();
                            }

                            _quantity[hotkeyData].Value = _occupied[hotkeyData].Count;
                            _rememberHotkey[hotkeyData].Value = false;
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
                    Logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SlotStorage), "CanPush", new Type[] { typeof(IPickupable), typeof(bool), typeof(bool) })]
        private class SlotStorage_CanPush_Patch
        {
            private static bool Prefix(SlotStorage __instance, IPickupable pickupable, bool force, bool notification, ref bool __result)
            {
                if (!Enabled) return true;

                try
                {
                    if (!__instance.Name.Equals("INVENTORY_MENU_BACKPACK_TITLE")) return true;

                    __result = CanPush(__instance, StorageType.Both, pickupable, force, notification);
                    return false;
                }
                catch (Exception e)
                {
                    Logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SlotStorage), nameof(SlotStorage.Pop))]
        private class SlotStorage_Pop_Patch
        {
            private static bool Prefix(SlotStorage __instance, IPickupable pickupable)
            {
                if (!Enabled) return true;

                try
                {
                    if (!__instance.Name.Equals("INVENTORY_MENU_BACKPACK_TITLE")) return true;

                    Pop(__instance, pickupable, true, true);
                    return false;
                }
                catch (Exception e)
                {
                    Logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SlotStorage), nameof(SlotStorage.PopNext))]
        private class SlotStorage_PopNext_Patch
        {
            private static readonly AccessTools.FieldRef<SlotStorage, List<StorageSlot<IPickupable>>> _slotDataRef = AccessTools.FieldRefAccess<SlotStorage, List<StorageSlot<IPickupable>>>("_slotData");
            private static readonly AccessTools.FieldRef<SlotStorage, StorageSlot<IPickupable>> _selectedSlotRef = AccessTools.FieldRefAccess<SlotStorage, StorageSlot<IPickupable>>("_selectedSlot");

            private static readonly MethodInfo OnSelectionChanged = AccessTools.Method(typeof(SlotStorage), "OnSelectionChanged");

            private static readonly AccessTools.FieldRef<StorageSlot<IPickupable>, int> _indexRef = AccessTools.FieldRefAccess<StorageSlot<IPickupable>, int>("_index");

            private static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");

            private static bool Prefix(SlotStorage __instance, IPickupable pickupable)
            {
                if (!Enabled) return true;

                try
                {
                    if (!__instance.Name.Equals("INVENTORY_MENU_BACKPACK_TITLE")) return true;

                    HotkeyData pickupableHotkeyData = null;

                    if (slotStorage_HotkeyController.TryGetValue(__instance, out HotkeyController hotkeyController))
                    {
                        for (int i = 10; i < _slotDataRef(__instance).Count; i++)
                        {
                            HotkeyData hotkeyData = _hotkeysRef(hotkeyController)[i - 10];

                            if (_occupied[hotkeyData].Contains(pickupable.ReferenceId))
                            {
                                _occupied[hotkeyData].Remove(pickupable.ReferenceId);

                                _quantity[hotkeyData].Value = _occupied[hotkeyData].Count;

                                if (_occupied[hotkeyData].Count == 0)
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
                            if (Settings.rememberToolbelt)
                            {
                                _rememberHotkey[pickupableHotkeyData].Value = true;
                            }
                            else
                            {
                                ClearHotkey(pickupableHotkeyData);
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
                    Logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SlotStorage), nameof(SlotStorage.Push), new Type[] { typeof(IPickupable), typeof(bool) })]
        private class SlotStorage_Push_Patch
        {
            private static bool Prefix(SlotStorage __instance, IPickupable pickupable, bool force, ref bool __result)
            {
                if (!Enabled) return true;

                try
                {
                    if (!__instance.Name.Equals("INVENTORY_MENU_BACKPACK_TITLE")) return true;

                    __result = Push(__instance, StorageType.Both, pickupable, force);
                    return false;
                }
                catch (Exception e)
                {
                    Logger.LogException(e);
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
                if (!Enabled) return true;

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
                    Logger.LogException(e);
                    return true;
                }
            }
        }

#if DEBUG
        [HarmonyPatch(typeof(SlotStorage), "OrderedPush")]
        private class SlotStorage_OrderedPush_Patch
        {
            private static readonly AccessTools.FieldRef<SlotStorage, Transform> _storageContainerRef = AccessTools.FieldRefAccess<SlotStorage, Transform>("_storageContainer");
            private static readonly AccessTools.FieldRef<SlotStorage, LoadState> _loadStateRef = AccessTools.FieldRefAccess<SlotStorage, LoadState>("_loadState");

            private static bool Prefix(SlotStorage __instance, List<IPickupable> items, JObject slotsData)
            {
                if (!Enabled) return true;

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
                                    Logger.Warning("Could not push " + pickupable + " to storage. Not enough room.");

                                    if (Settings.dropOverflowItems)
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
                    Logger.LogException(e);
                    return true;
                }
            }
        }
#endif

        [HarmonyPatch(typeof(SlotStorageAudio), "SlotStorage_Pushed")]
        private class SlotStorageAudio_SlotStorage_Pushed_Patch
        {
            private static readonly AccessTools.FieldRef<SlotStorageAudio, ISlotStorage<IPickupable>> _slotStorageRef = AccessTools.FieldRefAccess<SlotStorageAudio, ISlotStorage<IPickupable>>("_slotStorage");

            private static bool Prefix(SlotStorageAudio __instance)
            {
                if (!Enabled) return true;

                try
                {
                    if (!slotStorage_HotkeyController.ContainsKey(_slotStorageRef(__instance))) return true;

                    return !silentSlotStorageTransfer;
                }
                catch (Exception e)
                {
                    Logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SlotStorageAudio), "SlotStorage_Popped")]
        private class SlotStorageAudio_SlotStorage_Popped_Patch
        {
            private static readonly AccessTools.FieldRef<SlotStorageAudio, ISlotStorage<IPickupable>> _slotStorageRef = AccessTools.FieldRefAccess<SlotStorageAudio, ISlotStorage<IPickupable>>("_slotStorage");

            private static bool Prefix(SlotStorageAudio __instance)
            {
                if (!Enabled) return true;

                try
                {
                    if (!slotStorage_HotkeyController.ContainsKey(_slotStorageRef(__instance))) return true;

                    return !silentSlotStorageTransfer;
                }
                catch (Exception e)
                {
                    Logger.LogException(e);
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
                if (!Enabled) return;

                try
                {
                    storagePresenters.Add(_radialPresenterRef(__instance), __instance);
                }
                catch (Exception e)
                {
                    Logger.LogException(e);
                }
            }
        }

        [HarmonyPatch(typeof(StorageMenuPresenter), "OnDestroy")]
        private class StorageMenuPresenter_OnDestroy_Patch
        {
            private static readonly AccessTools.FieldRef<StorageMenuPresenter, StorageRadialMenuPresenter> _radialPresenterRef = AccessTools.FieldRefAccess<StorageMenuPresenter, StorageRadialMenuPresenter>("_radialPresenter");

            private static void Postfix(StorageMenuPresenter __instance)
            {
                if (!Enabled) return;

                try
                {
                    storagePresenters.Remove(_radialPresenterRef(__instance));
                }
                catch (Exception e)
                {
                    Logger.LogException(e);
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
                if (!Enabled || !Settings.stackTransfer) return true;

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
                    Logger.LogException(e);
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
                if (!Enabled || !Settings.quickDrop) return true;

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
                    Logger.LogException(e);
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
                if (!Enabled || !Settings.stackTransfer) return true;

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
                    Logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(HotkeyElementView), nameof(HotkeyElementView.Initialize))]
        private class HotkeyElementView_Initialize_Patch
        {
            private static readonly AccessTools.FieldRef<HotkeyElementView, UImageViewAdapter> _iconRef = AccessTools.FieldRefAccess<HotkeyElementView, UImageViewAdapter>("_icon");

            private static void Postfix(HotkeyElementView __instance, HotkeyData hotkeyData)
            {
                if (!Enabled) return;

                try
                {
                    CreateQuantityGroup(__instance);

                    _rememberHotkey[hotkeyData].ValueChanged += (rememberHotkey) => HotkeyData_RememberHotkeyValueChanged(__instance, rememberHotkey);
                    _quantity[hotkeyData].ValueChanged += (quantity) => HotkeyData_QuantityValueChanged(__instance, quantity);
                }
                catch (Exception e)
                {
                    Logger.LogException(e);
                }
            }

            private static void HotkeyData_RememberHotkeyValueChanged(HotkeyElementView __instance, bool rememberHotkey)
            {
                if (rememberHotkey)
                {
                    _iconRef(__instance).Color = hotkeyElementEmptyColor;
                }
                else
                {
                    _iconRef(__instance).Color = hotkeyElementColor;
                }
            }

            private static void HotkeyData_QuantityValueChanged(HotkeyElementView __instance, int quantity)
            {
                if (quantity == 0)
                {
                    _quantityGroup[__instance].SetActive(false);
                    return;
                }

                _quantityLabel[__instance].Text = string.Format("{0}", quantity);
                _quantityGroup[__instance].SetActive(quantity > 1);
            }
        }

        [HarmonyPatch(typeof(HotkeyController), "Awake")]
        private class HotkeyController_Awake_Patch
        {
            private static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");

            private static bool Prefix(HotkeyController __instance)
            {
                if (!Enabled) return true;

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

                        _occupied[_hotkeysRef(__instance)[i]] = new HashSet<MiniGuid>();
                        _reserved[_hotkeysRef(__instance)[i]] = new LinkedList<MiniGuid>();
                        _rememberHotkey[_hotkeysRef(__instance)[i]] = new Property<bool>(false);
                        _quantity[_hotkeysRef(__instance)[i]] = new Property<int>(0);
                    }
                    return false;
                }
                catch (Exception e)
                {
                    Logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(HotkeyController), nameof(HotkeyController.SetPlayer))]
        private class HotkeyController_SetPlayer_Patch
        {
            private static readonly AccessTools.FieldRef<HotkeyController, InventoryRadialMenuPresenter> _inventoryRadialMenuRef = AccessTools.FieldRefAccess<HotkeyController, InventoryRadialMenuPresenter>("_inventoryRadialMenu");
            private static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");
            private static readonly AccessTools.FieldRef<HotkeyController, HotkeyView> _viewRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyView>("_view");
            private static readonly AccessTools.FieldRef<HotkeyController, IPlayer> _playerRef = AccessTools.FieldRefAccess<HotkeyController, IPlayer>("_player");

            private static readonly MethodInfo EventManager_QuickAccessSlotUnlocked = AccessTools.Method(typeof(HotkeyController), "EventManager_QuickAccessSlotUnlocked");
            private static readonly MethodInfo EventManager_OptionsApplied = AccessTools.Method(typeof(HotkeyController), "EventManager_OptionsApplied");
            private static readonly MethodInfo SubscribeToInputEvents = AccessTools.Method(typeof(HotkeyController), "SubscribeToInputEvents");
            private static readonly MethodInfo Holder_Dropped = AccessTools.Method(typeof(HotkeyController), "Holder_Dropped");
            private static readonly MethodInfo LoadOptions = AccessTools.Method(typeof(HotkeyController), "LoadOptions");

            private static readonly MethodInfo InventoryRadialMenuElement_View = AccessTools.PropertyGetter(typeof(InventoryRadialMenuElement), "View");

            private static bool Prefix(HotkeyController __instance, IPlayer player)
            {
                if (!Enabled) return true;

                try
                {
                    _playerRef(__instance) = player;
                    slotStorage_HotkeyController[_playerRef(__instance).Inventory.GetSlotStorage()] = __instance;
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
                    _quantityGroupPrefab = inventoryRadialMenuElementView.QuantityGroup;

                    _viewRef(__instance).Initialize(_hotkeysRef(__instance));
                    _playerRef(__instance).Holder.Dropped += AccessTools.MethodDelegate<Action<IPickupable>>(Holder_Dropped, __instance);
                    SubscribeToInputEvents.Invoke(__instance, new object[] { _playerRef(__instance).Input });
                    EventManager_QuickAccessSlotUnlocked_Event = new EventManager.EventDelegate<QuickAccessSlotUnlockedEvent>(AccessTools.MethodDelegate<Action<QuickAccessSlotUnlockedEvent>>(EventManager_QuickAccessSlotUnlocked, __instance));
                    EventManager_OptionsApplied_Event = new EventManager.EventDelegate<OptionsAppliedEvent>(AccessTools.MethodDelegate<Action<OptionsAppliedEvent>>(EventManager_OptionsApplied, __instance));
                    EventManager.AddListener(EventManager_QuickAccessSlotUnlocked_Event);
                    EventManager.AddListener(EventManager_OptionsApplied_Event);
                    LoadOptions.Invoke(__instance, null);
                    return false;
                }
                catch (Exception e)
                {
                    Logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(HotkeyController), "OnDestroy")]
        private class HotkeyController_OnDestroy_Patch
        {
            private static readonly AccessTools.FieldRef<HotkeyController, InventoryRadialMenuPresenter> _inventoryRadialMenuRef = AccessTools.FieldRefAccess<HotkeyController, InventoryRadialMenuPresenter>("_inventoryRadialMenu");
            private static readonly AccessTools.FieldRef<HotkeyController, bool> _subbedToInventoryEventsRef = AccessTools.FieldRefAccess<HotkeyController, bool>("_subbedToInventoryEvents");
            private static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");
            private static readonly AccessTools.FieldRef<HotkeyController, HotkeyView> _viewRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyView>("_view");
            private static readonly AccessTools.FieldRef<HotkeyController, IPlayer> _playerRef = AccessTools.FieldRefAccess<HotkeyController, IPlayer>("_player");

            private static readonly AccessTools.FieldRef<PlatformHotkeyViewProvider, HotkeyView> _kbViewRef = AccessTools.FieldRefAccess<PlatformHotkeyViewProvider, HotkeyView>("_kbView");
            private static readonly AccessTools.FieldRef<PlatformHotkeyViewProvider, HotkeyView> _dpViewRef = AccessTools.FieldRefAccess<PlatformHotkeyViewProvider, HotkeyView>("_dpView");

            private static readonly AccessTools.FieldRef<UHotkeyView, IList<HotkeyElementView>> _elementsRef = AccessTools.FieldRefAccess<UHotkeyView, IList<HotkeyElementView>>("_elements");

            private static readonly MethodInfo UnsubscribeFromInputEvents = AccessTools.Method(typeof(HotkeyController), "UnsubscribeFromInputEvents");
            private static readonly MethodInfo Holder_Dropped = AccessTools.Method(typeof(HotkeyController), "Holder_Dropped");

            private static bool Prefix(HotkeyController __instance)
            {
                if (!Enabled) return true;

                try
                {
                    foreach (HotkeyData hotkeyData in _hotkeysRef(__instance))
                    {
                        hotkeyData.Locked.ClearSubscribers();
                        hotkeyData.CraftingType.ClearSubscribers();
                        _rememberHotkey[hotkeyData].ClearSubscribers();
                        _quantity[hotkeyData].ClearSubscribers();

                        _occupied.Remove(hotkeyData);
                        _reserved.Remove(hotkeyData);
                        _rememberHotkey.Remove(hotkeyData);
                        _quantity.Remove(hotkeyData);
                    }

                    if (_playerRef(__instance).IsNullOrDestroyed())
                    {
                        return false;
                    }

                    slotStorage_HotkeyController.Remove(_playerRef(__instance).Inventory.GetSlotStorage());

                    foreach (HotkeyElementView hotkeyElementView in _elementsRef(_kbViewRef(_viewRef(__instance) as PlatformHotkeyViewProvider) as UHotkeyView))
                    {
                        _quantityGroup.Remove(hotkeyElementView);
                        _quantityLabel.Remove(hotkeyElementView);
                    }

                    foreach (HotkeyElementView hotkeyElementView in _elementsRef(_dpViewRef(_viewRef(__instance) as PlatformHotkeyViewProvider) as UHotkeyView))
                    {
                        _quantityGroup.Remove(hotkeyElementView);
                        _quantityLabel.Remove(hotkeyElementView);
                    }

                    if (_playerRef(__instance).Holder != null)
                    {
                        _playerRef(__instance).Holder.Dropped -= AccessTools.MethodDelegate<Action<IPickupable>>(Holder_Dropped, __instance);
                    }
                    if (_subbedToInventoryEventsRef(__instance) && _inventoryRadialMenuRef(__instance) != null)
                    {
                        _inventoryRadialMenuRef(__instance).View.ShowView -= __instance.Show;
                        _inventoryRadialMenuRef(__instance).View.HideView -= __instance.Hide;
                    }
                    IPlayer player = _playerRef(__instance);
                    UnsubscribeFromInputEvents.Invoke(__instance, new object[] { player?.Input });
                    EventManager.RemoveListener(EventManager_QuickAccessSlotUnlocked_Event);
                    EventManager.RemoveListener(EventManager_OptionsApplied_Event);
                    return false;
                }
                catch (Exception e)
                {
                    Logger.LogException(e);
                    return true;
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
                if (!Enabled) return true;

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
                    Logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(HotkeyController), "Holder_Dropped")]
        private class HotkeyController_Holder_Dropped_Patch
        {
            private static bool Prefix()
            {
                if (!Enabled) return true;

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

            private static readonly AccessTools.FieldRef<SlotStorage, List<StorageSlot<IPickupable>>> _slotDataRef = AccessTools.FieldRefAccess<SlotStorage, List<StorageSlot<IPickupable>>>("_slotData");

            private static readonly MethodInfo GetInventoryData = AccessTools.Method(typeof(InventoryMenuPresenter), "GetInventoryData");
            private static readonly MethodInfo BlockHotkeyInput = AccessTools.Method(typeof(HotkeyController), "BlockHotkeyInput");

            private static bool Prefix(HotkeyController __instance, int number)
            {
                if (!Enabled) return true;

                try
                {
                    if ((bool)BlockHotkeyInput.Invoke(__instance, null))
                    {
                        return false;
                    }

                    IPickupable currentObject = _playerRef(__instance).Holder.CurrentObject;
                    HotkeyData hotkeyData = _hotkeysRef(__instance)[number];

                    if (_reserved[hotkeyData].Count == 0)
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
                    if (!currentObject.IsNullOrDestroyed() && _reserved[hotkeyData].Contains(currentObject.ReferenceId))
                    {
                        return false;
                    }
                    _viewRef(__instance).OnAssignmentFailed();
                    return false;
                }
                catch (Exception e)
                {
                    Logger.LogException(e);
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

            private static readonly MethodInfo GetInventoryData = AccessTools.Method(typeof(InventoryMenuPresenter), "GetInventoryData");

            private static bool Prefix(HotkeyController __instance, int number, IPickupable obj)
            {
                if (!Enabled) return true;

                try
                {
                    if ((bool)BlockHotkeyInput.Invoke(__instance, null))
                    {
                        return false;
                    }

                    SlotStorage storage = _playerRef(__instance).Inventory.GetSlotStorage() as SlotStorage;
                    HotkeyData hotkeyData = _hotkeysRef(__instance)[number];

                    if (obj.IsNullOrDestroyed() && _reserved[hotkeyData].Count == 0)
                    {
                        _viewRef(__instance)?.OnAssignmentFailed();
                        return false;
                    }

                    if (_reserved[hotkeyData].Count > 0)
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
                    Logger.LogException(e);
                    return true;
                }
            }

            private static bool ClearHotkeySlot(HotkeyController __instance, int number, bool abortOnFail = true)
            {
                SlotStorage storage = _playerRef(__instance).Inventory.GetSlotStorage() as SlotStorage;
                StorageSlot<IPickupable> hotkeySlot = _slotDataRef(storage)[number + 10];
                HotkeyData hotkeyData = _hotkeysRef(__instance)[number];

                if (hotkeySlot != null)
                {
                    IList<IPickupable> transferedPickupables = new List<IPickupable>();
                    bool refresh = false;
                    bool success = true;

                    silentSlotStorageTransfer = true;

                    while (hotkeySlot.Objects.Count > 0)
                    {
                        IPickupable pickupable = hotkeySlot.Objects[0];

                        if (!CanPush(storage, StorageType.Inventory, pickupable, false))
                        {
                            success = false;
                            break;
                        }

                        Pop(storage, pickupable, false, false);
                        Push(storage, StorageType.Inventory, pickupable, false);
                        transferedPickupables.Add(pickupable);
                        refresh = true;
                    }

                    if (!success && abortOnFail)
                    {
                        for (int i = transferedPickupables.Count - 1; i >= 0; i--)
                        {
                            IPickupable pickupable = transferedPickupables[i];

                            Pop(storage, pickupable, false, false);
                            Push(storage, StorageType.Hotkeys, pickupable, false);

                            hotkeySlot.Objects.Remove(pickupable);
                            hotkeySlot.Objects.Insert(0, pickupable);

                            _reserved[hotkeyData].Remove(pickupable.ReferenceId);
                            _reserved[hotkeyData].AddFirst(pickupable.ReferenceId);
                        }
                    }
                    else if (refresh)
                    {
                        IList<IList<InventoryData>> inventoryData = GetInventoryData.Invoke(_inventoryRadialMenuRef(__instance).Inventory, null) as IList<IList<InventoryData>>;
                        _inventoryRadialMenuRef(__instance).Initialize(inventoryData);
                        _inventoryRadialMenuRef(__instance).Refresh();
                    }

                    silentSlotStorageTransfer = false;

                    if (success)
                    {
                        ClearHotkey(hotkeyData);
                        return true;
                    }
                }

                return false;
            }

            private static bool ReplaceHotkeySlot(HotkeyController __instance, int number, IPickupable obj)
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

                        if (_occupied[data].Contains(obj.ReferenceId))
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
                        pickupables.AddRange(storageSlot.Objects.Take(GetHotkeyStackSize(storage, storageSlot.CraftingType)));

                        silentSlotStorageTransfer = true;

                        foreach (IPickupable pickupable in pickupables)
                        {
                            Pop(storage, pickupable, false, false);
                        }

                        silentSlotStorageTransfer = false;
                    }
                }

                if (ClearHotkeySlot(__instance, number))
                {
                    if (existingHotkeyData != null)
                    {
                        _occupied[existingHotkeyData].Remove(obj.ReferenceId);
                        _reserved[existingHotkeyData].Remove(obj.ReferenceId);

                        _quantity[existingHotkeyData].Value = _occupied[existingHotkeyData].Count;

                        if (_occupied[existingHotkeyData].Count == 0)
                        {
                            if (_reserved[existingHotkeyData].Count > 0 && Settings.rememberToolbelt)
                            {
                                _rememberHotkey[hotkeyData].Value = true;
                            }
                            else
                            {
                                ClearHotkey(existingHotkeyData);
                            }
                        }
                    }

                    silentSlotStorageTransfer = true;

                    foreach (IPickupable pickupable in pickupables)
                    {
                        _occupied[hotkeyData].Add(pickupable.ReferenceId);
                        _reserved[hotkeyData].AddLast(pickupable.ReferenceId);

                        if (transferPickupables)
                        {
                            Push(storage, StorageType.Hotkeys, pickupable, false);
                        }
                    }

                    silentSlotStorageTransfer = false;

                    if (transferPickupables)
                    {
                        IList<IList<InventoryData>> inventoryData = GetInventoryData.Invoke(_inventoryRadialMenuRef(__instance).Inventory, null) as IList<IList<InventoryData>>;
                        _inventoryRadialMenuRef(__instance).Initialize(inventoryData);
                        _inventoryRadialMenuRef(__instance).Refresh();

                        storage.SortSlots();
                    }

                    _quantity[hotkeyData].Value = _occupied[hotkeyData].Count;
                    hotkeyData.CraftingType.Value = obj.CraftingType;
                    return true;
                }
                else
                {
                    if (transferPickupables)
                    {
                        silentSlotStorageTransfer = true;

                        foreach (IPickupable pickupable in pickupables)
                        {
                            Push(storage, StorageType.Inventory, pickupable, false);
                        }

                        silentSlotStorageTransfer = false;
                    }

                    return false;
                }
            }

            private static void AddHotkeySlot(HotkeyController __instance, int number, IPickupable obj)
            {
                SlotStorage storage = _playerRef(__instance).Inventory.GetSlotStorage() as SlotStorage;
                HotkeyData hotkeyData = _hotkeysRef(__instance)[number];
                bool refresh = false;

                if (obj.Equals(_playerRef(__instance).Holder.CurrentObject))
                {
                    for (int i = 0; i < _hotkeysRef(__instance).Length; i++)
                    {
                        HotkeyData data = _hotkeysRef(__instance)[i];

                        if (_occupied[data].Contains(obj.ReferenceId))
                        {
                            _occupied[data].Remove(obj.ReferenceId);
                            _reserved[data].Remove(obj.ReferenceId);

                            _quantity[data].Value = _occupied[data].Count;

                            if (_occupied[data].Count == 0)
                            {
                                if (_reserved[data].Count > 0 && Settings.rememberToolbelt)
                                {
                                    _rememberHotkey[hotkeyData].Value = true;
                                }
                                else
                                {
                                    ClearHotkey(data);
                                }
                            }

                            break;
                        }
                    }

                    _occupied[hotkeyData].Add(obj.ReferenceId);
                    _reserved[hotkeyData].AddLast(obj.ReferenceId);
                }
                else
                {
                    StorageSlot<IPickupable> storageSlot = storage.FindSlot(obj);

                    if (storageSlot != null)
                    {
                        int pickupablesToTransfer = Math.Min(storageSlot.Objects.Count, GetHotkeyStackSize(storage, storageSlot.CraftingType));

                        silentSlotStorageTransfer = true;

                        for (int i = 0; i < pickupablesToTransfer; i++)
                        {
                            // Take from the beginning of the list
                            IPickupable pickupable = storageSlot.Objects[0];

                            _occupied[hotkeyData].Add(pickupable.ReferenceId);
                            _reserved[hotkeyData].AddLast(pickupable.ReferenceId);

                            Pop(storage, pickupable, true, false);
                            Push(storage, StorageType.Hotkeys, pickupable, false);
                            refresh = true;
                        }

                        silentSlotStorageTransfer = false;
                    }
                }

                if (refresh)
                {
                    IList<IList<InventoryData>> inventoryData = GetInventoryData.Invoke(_inventoryRadialMenuRef(__instance).Inventory, null) as IList<IList<InventoryData>>;
                    _inventoryRadialMenuRef(__instance).Initialize(inventoryData);
                    _inventoryRadialMenuRef(__instance).Refresh();
                }

                _quantity[hotkeyData].Value = _occupied[hotkeyData].Count;
                hotkeyData.CraftingType.Value = obj.CraftingType;
            }
        }

        [HarmonyPatch(typeof(HotkeyController), nameof(HotkeyController.Save))]
        private class HotkeyController_Save_Patch
        {
            private static readonly AccessTools.FieldRef<HotkeyController, HotkeyData[]> _hotkeysRef = AccessTools.FieldRefAccess<HotkeyController, HotkeyData[]>("_hotkeys");
            private static readonly AccessTools.FieldRef<HotkeyController, IPlayer> _playerRef = AccessTools.FieldRefAccess<HotkeyController, IPlayer>("_player");
            private static readonly AccessTools.FieldRef<HotkeyController, int> _levelRef = AccessTools.FieldRefAccess<HotkeyController, int>("_level");

            private static readonly MethodInfo GetStackSize = AccessTools.Method(typeof(SlotStorage), "GetStackSize");

            private static bool Prefix(HotkeyController __instance, ref JObject __result)
            {
                if (!Enabled) return true;

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
                        jobject3.AddField("ReferenceId", _reserved[hotkeyData].LastOrDefault().ToString());

                        if (_reserved[hotkeyData].Count > 1)
                        {
                            JObject jobject4 = new JObject();

                            foreach (MiniGuid miniGuid in _reserved[hotkeyData].Take((int)GetStackSize.Invoke(storage, new object[] { craftingType })))
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
                    Logger.LogException(e);
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
                if (!Enabled) return true;

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
                                    _reserved[hotkeyData].AddLast(miniGuid);
                                }
                            }
                        }
                        else
                        {
                            JObject referenceIdData = jobject.GetField("ReferenceId");
                            MiniGuid miniGuid = referenceIdData.GetValue<string>().ToMiniGuid();

                            if (!miniGuid.IsDefault())
                            {
                                _reserved[hotkeyData].AddLast(miniGuid);
                            }
                        }

                        hotkeyData.Locked.Value = value;
                    }

                    LevelLoader.CurrentLoader.DeferredLoading.Add(() => Timing.RunCoroutine(DeferredLoad(__instance, hotkeysData)));
                    return false;
                }
                catch (Exception e)
                {
                    Logger.LogException(e);
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
                        Logger.Error(string.Format("Timeout while waiting for Holder & Inventory ({0}) to load. Cannot proceed with loading the toolbelt.", _playerRef(__instance).Inventory.GetSlotStorage().LoadState));
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

                silentSlotStorageTransfer = true;

                for (int i = 0; i < hotkeysData.Children.Count; i++)
                {
                    HotkeyData hotkeyData = _hotkeysRef(__instance)[i];
                    JObject jobject = hotkeysData.Children[i];
                    if (hotkeyData.Locked.Value)
                    {
                        break;
                    }
                    if (_reserved[hotkeyData].Count == 0)
                    {
                        continue;
                    }

                    List<IPickupable> pickupables = new List<IPickupable>();
                    CraftingType craftingType = CraftingType.Empty;

                    foreach (MiniGuid miniGuid in _reserved[hotkeyData])
                    {
                        IPickupable pickupable = list.FirstOrDefault_NonAlloc((IPickupable o, MiniGuid referenceId) => o.ReferenceId.Equals(referenceId), miniGuid);

                        if (!pickupable.IsNullOrDestroyed())
                        {
                            _occupied[hotkeyData].Add(pickupable.ReferenceId);
                            craftingType = pickupable.CraftingType;
                            list.Remove(pickupable);

                            if (!pickupable.Equals(_playerRef(__instance).Holder.CurrentObject))
                            {
                                Pop(storage, pickupable, true, false);
                                pickupables.Add(pickupable);
                            }
                        }
                    }

                    foreach (IPickupable pickupable in pickupables)
                    {
                        Push(storage, StorageType.Hotkeys, pickupable, false);
                    }

                    if (_occupied[hotkeyData].Count > 0)
                    {
                        _quantity[hotkeyData].Value = _occupied[hotkeyData].Count;
                        hotkeyData.CraftingType.Value = craftingType;
                    }
                    else if (Settings.rememberToolbelt)
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
                            _rememberHotkey[hotkeyData].Value = true;
                        }
                        else
                        {
                            _reserved[hotkeyData].Clear();
                        }
                    }
                    else
                    {
                        _reserved[hotkeyData].Clear();
                    }
                }

                silentSlotStorageTransfer = false;
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
                if (!Enabled) return;

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
                    Logger.LogException(e);
                }
            }
        }

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
}
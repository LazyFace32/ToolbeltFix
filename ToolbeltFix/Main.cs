using Beam;
using Beam.Crafting;
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
using Beam.Developer.UI;
using Beam.Developer;
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
        //[Draw("Allow Container Crates", Height = 15)] public bool allowContainerCrates = false;
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
        private static readonly AccessTools.FieldRef<SlotStorage, Transform> _storageContainerRef = AccessTools.FieldRefAccess<SlotStorage, Transform>("_storageContainer");
        private static readonly AccessTools.FieldRef<SlotStorage, bool> _storeOtherStorageRef = AccessTools.FieldRefAccess<SlotStorage, bool>("_storeOtherStorage");
        private static readonly AccessTools.FieldRef<SlotStorage, LoadState> _loadStateRef = AccessTools.FieldRefAccess<SlotStorage, LoadState>("_loadState");
        private static readonly AccessTools.FieldRef<SlotStorage, bool> _modifiedRef = AccessTools.FieldRefAccess<SlotStorage, bool>("_modified");

        private static readonly MethodInfo CheckTypesMatch = AccessTools.Method(typeof(SlotStorage), "CheckTypesMatch");
        private static readonly MethodInfo GetStackSize = AccessTools.Method(typeof(SlotStorage), "GetStackSize");
        private static readonly MethodInfo OnPushed = AccessTools.Method(typeof(SlotStorage), "OnPushed");
        private static readonly MethodInfo OnPopped = AccessTools.Method(typeof(SlotStorage), "OnPopped");

        internal static readonly Dictionary<StorageRadialMenuPresenter, StorageMenuPresenter> storagePresenters = new Dictionary<StorageRadialMenuPresenter, StorageMenuPresenter>();

#if DEBUG
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
        public static readonly Dictionary<HotkeyElementView, GameObject> _quantityGroup = new Dictionary<HotkeyElementView, GameObject>();
        [SaveOnReload]
        public static readonly Dictionary<HotkeyElementView, ILabelViewAdapter> _quantityLabel = new Dictionary<HotkeyElementView, ILabelViewAdapter>();

        [SaveOnReload]
        public static GameObject _quantityGroupPrefab;
#else
        internal static readonly Dictionary<ISlotStorage<IPickupable>, HotkeyController> slotStorage_HotkeyController = new Dictionary<ISlotStorage<IPickupable>, HotkeyController>();

        internal static readonly Dictionary<HotkeyData, ICollection<MiniGuid>> _occupied = new Dictionary<HotkeyData, ICollection<MiniGuid>>();
        internal static readonly Dictionary<HotkeyData, LinkedList<MiniGuid>> _reserved = new Dictionary<HotkeyData, LinkedList<MiniGuid>>();
        internal static readonly Dictionary<HotkeyData, Property<bool>> _rememberHotkey = new Dictionary<HotkeyData, Property<bool>>();
        internal static readonly Dictionary<HotkeyData, Property<int>> _quantity = new Dictionary<HotkeyData, Property<int>>();

        internal static readonly Dictionary<HotkeyElementView, GameObject> _quantityGroup = new Dictionary<HotkeyElementView, GameObject>();
        internal static readonly Dictionary<HotkeyElementView, ILabelViewAdapter> _quantityLabel = new Dictionary<HotkeyElementView, ILabelViewAdapter>();

        internal static GameObject _quantityGroupPrefab;
#endif

        internal static Color hotkeyElementEmptyColor = new Color(0.4f, 0.4f, 0.4f, 0.65f);
        internal static Color hotkeyElementColor = Color.white;

        private static CoroutineHandle notificationHandler;
        private static string originalNotificationMessage;
        internal static bool silentSlotStorageTransfer;
        private static bool worldLoaded;

        private static InteractiveType InteractiveType_CONTAINER;

        internal static InventoryHotkeyQuantityComparer InventoryHotkeyQuantityComparison { get; } = new InventoryHotkeyQuantityComparer();

        internal static UnityModManager.ModEntry.ModLogger Logger { get; private set; }
        internal static Settings Settings { get; private set; }
        internal static bool Enabled { get; private set; }

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

        internal static IEnumerable<IPickupable> GetStored(SlotStorage __instance, StorageType storageType)
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

        internal static bool CanPush(SlotStorage __instance, StorageType storageType, IPickupable pickupable, bool force, bool notification = true)
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

        internal static bool Has(SlotStorage __instance, StorageType storageType, IPickupable pickupable)
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

        internal static void Pop(SlotStorage __instance, IPickupable pickupable, bool sort = true, bool allowClearHotkey = true)
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

        internal static bool Push(SlotStorage __instance, StorageType storageType, IPickupable pickupable, bool force)
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

        internal static StorageSlot<IPickupable> GetSlot(SlotStorage __instance, StorageType storageType, IPickupable pickupable, bool assign = true)
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

        internal static int GetHotkeyStackSize(SlotStorage __instance, CraftingType type)
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

        internal static void ClearHotkey(HotkeyData hotkeyData)
        {
            hotkeyData.CraftingType.Value = CraftingType.Empty;
            _rememberHotkey[hotkeyData].Value = false;
            _quantity[hotkeyData].Value = 0;

            _occupied[hotkeyData].Clear();
            _reserved[hotkeyData].Clear();
        }

        internal static void CreateQuantityGroup(HotkeyElementView __instance)
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

        internal static DelegateType CreateMethodDelegate<DelegateType>(MethodInfo method, object instance) where DelegateType : Delegate
        {
            return (DelegateType)Delegate.CreateDelegate(typeof(DelegateType), instance, method.GetBaseDefinition());
        }
    }
}
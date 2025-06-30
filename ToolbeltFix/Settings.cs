using Beam;
using Beam.Utilities;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

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
}

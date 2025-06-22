using HarmonyLib;
using System;

namespace ToolbeltFix.Patches
{
    class GeneralSettings_Patches
    {
        [HarmonyPatch(typeof(GeneralSettings), nameof(GeneralSettings.Load))]
        class GeneralSettings_Load_Patch
        {
            static void Postfix(GeneralSettings __instance)
            {
                if (!Main.Enabled) return;

                try
                {
                    __instance.AlwaysShowHotkeyBar = Main.Settings.alwaysShowHotkeyBar;
                }
                catch (Exception e)
                {
                    Main.Logger.LogException(e);
                }
            }
        }
    }
}

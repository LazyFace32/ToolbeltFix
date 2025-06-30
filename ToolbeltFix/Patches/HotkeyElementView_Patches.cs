using Beam.UI;
using Beam;
using HarmonyLib;
using System;

namespace ToolbeltFix.Patches
{
    class HotkeyElementView_Patches
    {
        [HarmonyPatch(typeof(HotkeyElementView), nameof(HotkeyElementView.Initialize))]
        class HotkeyElementView_Initialize_Patch
        {
            static readonly AccessTools.FieldRef<HotkeyElementView, UImageViewAdapter> _iconRef = AccessTools.FieldRefAccess<HotkeyElementView, UImageViewAdapter>("_icon");

            static void Postfix(HotkeyElementView __instance, HotkeyData hotkeyData)
            {
                if (!Main.Enabled) return;

                try
                {
                    Main.CreateQuantityGroup(__instance);

                    Main._rememberHotkey[hotkeyData].ValueChanged += (rememberHotkey) => HotkeyData_RememberHotkeyValueChanged(__instance, rememberHotkey);
                    Main._quantity[hotkeyData].ValueChanged += (quantity) => HotkeyData_QuantityValueChanged(__instance, quantity);
                }
                catch (Exception e)
                {
                    Main.Logger.LogException(e);
                }
            }

            static void HotkeyData_RememberHotkeyValueChanged(HotkeyElementView __instance, bool rememberHotkey)
            {
                if (rememberHotkey)
                {
                    _iconRef(__instance).Color = Main.hotkeyElementEmptyColor;
                }
                else
                {
                    _iconRef(__instance).Color = Main.hotkeyElementColor;
                }
            }

            static void HotkeyData_QuantityValueChanged(HotkeyElementView __instance, int quantity)
            {
                if (quantity == 0)
                {
                    Main._quantityGroup[__instance].SetActive(false);
                    return;
                }

                Main._quantityLabel[__instance].Text = string.Format("{0}", quantity);
                Main._quantityGroup[__instance].SetActive(quantity > 1);
            }
        }
    }
}

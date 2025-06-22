using Beam;
using HarmonyLib;
using System;

namespace ToolbeltFix.Patches
{
    class SlotStorageAudio_Patches
    {
        [HarmonyPatch(typeof(SlotStorageAudio), "SlotStorage_Pushed")]
        class SlotStorageAudio_SlotStorage_Pushed_Patch
        {
            static readonly AccessTools.FieldRef<SlotStorageAudio, ISlotStorage<IPickupable>> _slotStorageRef = AccessTools.FieldRefAccess<SlotStorageAudio, ISlotStorage<IPickupable>>("_slotStorage");

            static bool Prefix(SlotStorageAudio __instance)
            {
                if (!Main.Enabled) return true;

                try
                {
                    if (!Main.slotStorage_HotkeyController.ContainsKey(_slotStorageRef(__instance))) return true;

                    return !Main.silentSlotStorageTransfer;
                }
                catch (Exception e)
                {
                    Main.Logger.LogException(e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SlotStorageAudio), "SlotStorage_Popped")]
        class SlotStorageAudio_SlotStorage_Popped_Patch
        {
            static readonly AccessTools.FieldRef<SlotStorageAudio, ISlotStorage<IPickupable>> _slotStorageRef = AccessTools.FieldRefAccess<SlotStorageAudio, ISlotStorage<IPickupable>>("_slotStorage");

            static bool Prefix(SlotStorageAudio __instance)
            {
                if (!Main.Enabled) return true;

                try
                {
                    if (!Main.slotStorage_HotkeyController.ContainsKey(_slotStorageRef(__instance))) return true;

                    return !Main.silentSlotStorageTransfer;
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

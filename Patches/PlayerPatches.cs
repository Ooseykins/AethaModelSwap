using HarmonyLib;

namespace AethaModelSwapMod.Patches;

[HarmonyPatch(typeof(Player))]
public class PlayerPatches
{
    [HarmonyPatch("TryTriggerItem")]
    private static bool Prefix(Player __instance, int itemId, Player.triggerType triggerType)
    {
        // Disable using items when the editor menu is open
        return !ModelParamsEditor.IsEditorOpen;
    }
}
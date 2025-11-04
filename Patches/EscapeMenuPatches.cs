using HarmonyLib;

namespace AethaModelSwapMod.Patches;

[HarmonyPatch(typeof(EscapeMenu))]
public class EscapeMenuPatches
{
    [HarmonyPatch("Open")]
    private static void Postfix(EscapeMenu __instance)
    {
        ModelParamsEditor.ExitEditor();
    }
}
using HarmonyLib;

namespace AethaModelSwapMod.Patches;

[HarmonyPatch(typeof(EscapeMenuMainPage))]
public class EscapeMenuMainPagePatches
{
    [HarmonyPatch("OnSaveQuitButtonClicked")]
    private static void Postfix(EscapeMenuMainPage __instance)
    {
        AethaModelSwap.UnloadAllBundles();
    }
}
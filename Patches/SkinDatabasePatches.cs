using HarmonyLib;
using Landfall.Haste;

namespace AethaModelSwapMod.Patches;

[HarmonyPatch(typeof(SkinDatabase))]
public class SkinDatabasePatches
{
    [HarmonyPatch("Awake")]
    private static void Postfix(SkinDatabase __instance)
    {
        AethaModelSwap.RegisterToSkinManager(__instance);
    }
}
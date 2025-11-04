using HarmonyLib;
using Landfall.Haste;

namespace AethaModelSwapMod.Patches;

[HarmonyPatch(typeof(SkinManager))]
public class SkinManagerPatches
{
    // If setting the body skin to a mod skin, also set the head
    [HarmonyPrefix]
    [HarmonyPatch("SetBodySkin")]
    private static void SetBodySkin(Player __instance, SkinManager.Skin skin)
    {
        if (!AethaModelSwap.HasSkin((int)skin)) return;
        SkinManager.HeadSkin = skin;
        FactSystem.SetFact(SkinManager.EquippedSkinHeadFact, (float) SkinManager.HeadSkin);
    }
    
    // If setting the head skin to a mod skin, also set the body
    [HarmonyPrefix]
    [HarmonyPatch("SetHeadSkin")]
    private static bool SetHeadSkin(Player __instance, SkinManager.Skin skin)
    {
        if (!AethaModelSwap.HasSkin((int)skin)) return true;
        SkinManager.HeadSkin = skin;
        FactSystem.SetFact(SkinManager.EquippedSkinHeadFact, (float) SkinManager.HeadSkin);
        SkinManager.SetBodySkin(skin);
        return false;
    }
}
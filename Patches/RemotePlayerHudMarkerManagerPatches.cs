using HarmonyLib;
using Landfall.Haste;

namespace AethaModelSwapMod.Patches;

[HarmonyPatch(typeof(RemotePlayerHudMarkerManager))]
public class RemotePlayerHudMarkerManagerPatches {
    [HarmonyPatch("SetPosition")]
    private static void Postfix(RemotePlayerHudMarkerManager __instance, RemotePlayerHudMarker p, SkinManager.Skin headSkin)
    {
        if (AethaModelSwap.HasSkin((int)headSkin) && AethaModelSwap.GetSprite((int)headSkin))
        {
            p.color.sprite = AethaModelSwap.GetSprite((int)headSkin);
        }
    }
}
using System.Reflection;
using HarmonyLib;
using Landfall.Haste;
using Unity.Mathematics;
using UnityEngine;

namespace AethaModelSwapMod.Patches;

[HarmonyPatch(typeof(PlayerSkinSetter))]
public class SkinSetterPatches
{
    [HarmonyPrefix]
    [HarmonyPatch("SetHipAndNeckVisuals")]
    private static void SetHipAndNeckVisuals(PlayerSkinSetter __instance, ref SkinManager.Skin skin, ref SkinManager.Skin neckSkin)
    {
        if (AethaModelSwap.HasSkin((int)skin))
        {
            neckSkin = skin;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("SetNeckVisuals")]
    private static void SetNeckVisuals(PlayerSkinSetter __instance, SkinManager.Skin skin)
    {
        Debug.Log($"AethaModelSwap Postfix on {__instance}: switching to skin {(int)skin}");

        var currentHipField =
            typeof(PlayerSkinSetter).GetField("currentHip", BindingFlags.Instance | BindingFlags.NonPublic);
        if (currentHipField == null)
        {
            Debug.LogError($"Something went wrong with reflection, AethaModelSwap is NOT swapping to {skin}");
            return;
        }
        
        var currentHip = currentHipField.GetValue(__instance) as GameObject;
        if (!currentHip)
        {
            Debug.LogError($"Something different went wrong with reflection, AethaModelSwap is NOT swapping to {skin}");
            return;
        }
        
        if (!AethaModelSwap.HasSkin((int)skin))
        {
            foreach (var r in currentHip.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                r.enabled = true;
            }
            foreach (var r in currentHip.GetComponentsInChildren<Renderer>())
            {
                r.enabled = true;
            }
            return;
        }

        var clone = AethaModelSwap.InstantiateSkin(currentHip.transform, (int)skin, __instance.IsLocalPlayer);

        // This handles the SkinPreview3d layer setting logic for the clone
        if (clone && __instance.transform.root.name == "GAME")
        {
            var preview = __instance.transform.root.GetComponentInChildren<SkinPreview3d>();
            if (!preview) return;
            var layer = math.tzcnt(preview.cam.cullingMask);
            SetLayer(clone.transform.root);
            void SetLayer(Transform tf)
            {
                tf.gameObject.layer = layer;
                for (int index = 0; index < tf.childCount; ++index)
                    SetLayer(tf.GetChild(index));
            }
        }
    }
}
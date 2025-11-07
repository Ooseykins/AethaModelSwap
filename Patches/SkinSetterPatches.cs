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
    private static void SetHipAndNeckVisuals(PlayerSkinSetter __instance, SkinManager.Skin skin, ref SkinManager.Skin neckSkin)
    {
        if (AethaModelSwap.HasSkin((int)skin))
        {
            neckSkin = skin;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch("SetNeckVisuals")]
    private static void SetNeckVisualsPrefix(PlayerSkinSetter __instance, SkinManager.Skin skin)
    {
        // Set the neck's parent to null, cause it's about to be destroyed and we don't want two necks on the instantiated model
        var currentNeck = GetCurrentNeck(__instance);
        if (currentNeck)
        {
            currentNeck.transform.parent = null;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("SetNeckVisuals")]
    private static void SetNeckVisualsPostfix(PlayerSkinSetter __instance, SkinManager.Skin skin)
    {
        Debug.Log($"AethaModelSwap Postfix on {__instance}: switching to skin {(int)skin}");

        var currentHip = GetCurrentHip(__instance);
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
            SetLayer(clone.transform);
            void SetLayer(Transform tf)
            {
                tf.gameObject.layer = layer;
                for (int index = 0; index < tf.childCount; ++index)
                    SetLayer(tf.GetChild(index));
            }
        }
    }

    static GameObject GetCurrentHip(PlayerSkinSetter playerSkinSetter)
    {
        var currentHipField = typeof(PlayerSkinSetter).GetField("currentHip", BindingFlags.Instance | BindingFlags.NonPublic);
        if (currentHipField == null)
        {
            Debug.LogError($"Something went wrong with reflection, AethaModelSwap is NOT swapping skins");
            return null;
        }
        return currentHipField.GetValue(playerSkinSetter) as GameObject;
    }
    
    static GameObject GetCurrentNeck(PlayerSkinSetter playerSkinSetter)
    {
        var currentNeckField = typeof(PlayerSkinSetter).GetField("currentNeck", BindingFlags.Instance | BindingFlags.NonPublic);
        if (currentNeckField == null)
        {
            Debug.LogError($"Something went wrong with reflection, AethaModelSwap is NOT swapping skins");
            return null;
        }
        return currentNeckField.GetValue(playerSkinSetter) as GameObject;
    }
}
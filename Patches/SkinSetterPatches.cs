using System.Reflection;
using Unity.Mathematics;
using UnityEngine;

namespace AethaModelSwapMod.Patches;

public static class SkinSetterPatches
{
    public static void Patch()
    {
        On.PlayerSkinSetter.SetHipAndNeckVisuals += (orig, self, skin, neckSkin) =>
        {
            if (AethaModelSwap.HasSkin((int)skin))
            {
                neckSkin = skin;
            }
            orig(self, skin, neckSkin);
        };

        On.PlayerSkinSetter.SetNeckVisuals += (orig, self, skin) =>
        {
            // Set the neck's parent to null, cause it's about to be destroyed and we don't want two necks on the instantiated model
            var currentNeck = GetCurrentNeck(self);
            if (currentNeck)
            {
                currentNeck.transform.parent = null;
            }
            
            orig(self, skin);
            
            Debug.Log($"AethaModelSwap Postfix on {self}: switching to skin {(int)skin}");

            var currentHip = GetCurrentHip(self);
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

            var clone = AethaModelSwap.InstantiateSkin(currentHip.transform, (int)skin, self.IsLocalPlayer);

            // This handles the SkinPreview3d layer setting logic for the clone
            if (clone && self.transform.root.name == "GAME")
            {
                var preview = self.transform.root.GetComponentInChildren<SkinPreview3d>();
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
        };
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
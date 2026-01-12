using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Landfall.Haste;
using Unity.Mathematics;
using UnityEngine;
using Zorro.Core;

namespace AethaModelSwapMod.Patches;

public static class SkinSetterPatches
{
    public static void Patch()
    {
        
        // Shuffle the player's skin
        On.PlayerSkinSetter.Awake += (orig, self) =>
        {
            if (self.IsLocalPlayer && SkinDatabase.me && FavouriteSkinButton.ShuffleEnabled)
            {
                HashSet<SkinManager.Skin> favouriteSkins = new ();
                foreach (var entry in SkinDatabase.me.Skins)
                {
                    if (FavouriteSkinButton.IsSkinFavourite(entry.Skin))
                    {
                        favouriteSkins.Add(entry.Skin);
                    }
                }
                if (favouriteSkins.Any())
                {
                    var newSkin = favouriteSkins.RandomElement();
                    if (SkinManager.HeadSkin == newSkin)
                    {
                        // Roll a second time if we get the same skin twice in a row, just for fun!
                        newSkin = favouriteSkins.RandomElement();
                    }
                    SkinManager.BodySkin = newSkin;
                    SkinManager.HeadSkin = SkinManager.BodySkin;
                    FactSystem.SetFact(SkinManager.EquippedSkinBodyFact, (float) SkinManager.BodySkin);
                    FactSystem.SetFact(SkinManager.EquippedSkinHeadFact, (float) SkinManager.HeadSkin);
                }
            }
            orig(self);
        };
        
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
                    r.forceRenderingOff = false;
                }
                foreach (var r in currentHip.GetComponentsInChildren<Renderer>())
                {
                    r.forceRenderingOff = false;
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
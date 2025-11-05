using System;
using System.Reflection;
using HarmonyLib;
using Landfall.Haste;
using UnityEngine;

namespace AethaModelSwapMod.Patches;

[HarmonyPatch(typeof(SkinSelectionListEntry))]
public class SkinSelectionListEntryPatches
{
    private static SkinManager.Skin _selectedHead;
    private static SkinManager.Skin _selectedBody;
    
    [HarmonyPostfix]
    [HarmonyPatch("AssignSkinToListEntry")]
    private static void AssignSkinToListEntry(SkinSelectionListEntry __instance, SkinDatabaseEntry skinEntry, int id, SkinManager.SkinSlot slot)
    {
        _selectedHead = SkinManager.GetHeadSkinFromFacts();
        _selectedBody = SkinManager.GetBodySkinFromFacts();
        __instance.gameObject.SetActive(slot == SkinManager.SkinSlot.Head || !AethaModelSwap.HasSkin((int)skinEntry.Skin));
    }
    
    [HarmonyPrefix]
    [HarmonyPatch("HandleButtonPress")]
    private static bool HandleButtonPress(SkinSelectionListEntry __instance)
    {
        // Only handle things differently if the skin is unlocked
        var status = SkinManager.GetSkinStatus(__instance.skin.Skin);
        if (status == SkinManager.SkinStatus.Locked)
            return true;
        if (status == SkinManager.SkinStatus.Unlocked)
            return true;

        var updateBoth = AethaModelSwap.HasSkin((int)_selectedBody) || AethaModelSwap.HasSkin((int)_selectedHead) || AethaModelSwap.HasSkin((int)__instance.skin.Skin);

        switch(__instance.slot)
        {
            case SkinManager.SkinSlot.Body:
                _selectedBody = __instance.skin.Skin;
                break;
            case SkinManager.SkinSlot.Head:
                _selectedHead = __instance.skin.Skin;
                break;
        }

        if (!updateBoth) return true;

        var setSkinInfo = typeof(SkinSelectionListEntry).GetMethod("SelectSkin", BindingFlags.Instance | BindingFlags.NonPublic);
        if (setSkinInfo == null)
        {
            Debug.LogError("No method found on SkinSelectionListEntry");
            return true;
        }
        
        _selectedBody = __instance.skin.Skin;
        _selectedHead = __instance.skin.Skin;
        
        var prevSlot = __instance.slot;
        __instance.slot = SkinManager.SkinSlot.Head;
        setSkinInfo.Invoke(__instance, Array.Empty<object>());
        __instance.slot = SkinManager.SkinSlot.Body;
        setSkinInfo.Invoke(__instance, Array.Empty<object>());
        __instance.slot = prevSlot;
        return false;
    }
}
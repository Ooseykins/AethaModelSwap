using HarmonyLib;
using UnityEngine;

namespace AethaModelSwapMod.Patches;

[HarmonyPatch(typeof(PlayerCharacter.PlayerInput))]
public class PlayerCharacterPatches {
    [HarmonyPatch("SampleInput")]
    private static void Postfix(PlayerCharacter.PlayerInput __instance, PlayerCharacter character, bool autoRun = false)
    {
        if (!ModelParamsEditor.IsEditorOpen) return;
        // Disable ability when the editor menu is open
        __instance.abilityIsPressed = false;
        __instance.abilityWasPressed = false;
        if (HasteInputSystem.Item1.GetKey())
        {
            // Hold right-click to move the camera in editor
            __instance.lookInput *= 1f;
        }
        else
        {
            // Keep camera still when in the editor
            __instance.lookInput = Vector3.zero;
        }
    }
}
using System.Reflection;
using HarmonyLib;

namespace AethaModelSwapMod.Patches;

[HarmonyPatch]
public class SkinSelectionUIPatches
{
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method("Landfall.Haste.SkinSelectionUI:OpenUI");
    }
    public static void Prefix()
    {
        AethaModelSwap.ValidateLocalSkin();
        if (ModelParamsEditor.IsEditorOpen)
        {
            ModelParamsEditor.ExitEditor();
        }
    }
}
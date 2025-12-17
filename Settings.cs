using System.IO;
using System.Reflection;
using Landfall.Haste;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using Zorro.Settings;

namespace AethaModelSwapMod;

[HasteSetting]
public class ShowFashionableWeebohHelpUI : BoolSetting, IExposedSetting
{
    public static bool value;
    public override void ApplyValue()
    {
        if (FashionableWeebohHelpUI.instance)
        {
            FashionableWeebohHelpUI.instance.gameObject.SetActive(Value);
        }
        value = Value;
    }

    protected override bool GetDefaultValue() => true;
    public override LocalizedString OffString => new ((TableReference) "Settings", (TableEntryReference) "DisabledGraphicOption");
    public override LocalizedString OnString => new ((TableReference) "Settings", (TableEntryReference) "EnabledGraphicOption");
    public LocalizedString GetDisplayName() => new UnlocalizedString("Show Fashionable Weeboh icon in hub");
    public string GetCategory() => "General";
}

[HasteSetting]
public class OpenEditor : ButtonSetting, IExposedSetting
{
    public override void OnClicked(ISettingHandler settingHandler)
    {
        if (!EscapeMenu.Instance) return;
        var pageHandlerField =
            typeof(EscapeMenu).GetField("m_pageHandler", BindingFlags.Instance | BindingFlags.NonPublic);
        if (pageHandlerField != null)
        {
            var pageHandler = pageHandlerField.GetValue(EscapeMenu.Instance) as EscapeMenuPageHandler;
            if (pageHandler)
            {
                pageHandler.TransistionToPage<EscapeMenuMainPage>();
            }
        }
        EscapeMenu.Instance.Close();
        ModelParamsEditor.OpenEditor();
    }
    public override string GetButtonText() => "Open";
    public LocalizedString GetDisplayName() => new UnlocalizedString("Model Editor");
    public string GetCategory() => "General";
}

[HasteSetting]
public class OpenModPath : ButtonSetting, IExposedSetting
{
    public override void OnClicked(ISettingHandler settingHandler)
    {
        Application.OpenURL(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
    }
    public override string GetButtonText() => "Open";
    public LocalizedString GetDisplayName() => new UnlocalizedString("AethaModelSwap Directory");
    public string GetCategory() => "General";
}

[HasteSetting]
public class UnlimitedSkinWeights : BoolSetting, IExposedSetting
{
    public override void ApplyValue() => QualitySettings.skinWeights = Value ? SkinWeights.Unlimited : SkinWeights.FourBones;

    protected override bool GetDefaultValue() => true;
    public override LocalizedString OffString => new UnlocalizedString("4 bones");
    public override LocalizedString OnString => new UnlocalizedString("Unlimited");
    public LocalizedString GetDisplayName() => new UnlocalizedString("Skinned Mesh Renderer quality");
    public string GetCategory() => "Graphics";
}
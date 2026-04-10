using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Landfall.Haste;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using Zorro.Core;
using Zorro.Settings;
using Zorro.Settings.DebugUI;

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

public class SparkStyleSetting : Setting, IEnumSetting, IExposedSetting
{
    public LocalizedString GetDisplayName() => new UnlocalizedString("Spark Model");

    public string GetCategory() => "General";
    public override void Load(ISettingsSaveLoad loader)
    {
        if (loader.TryLoadString("ModelSwapSparkSelection", out var result))
        {
            AethaModelSwap.selectedSpark = AethaModelSwap.RegisteredSparks.FirstOrDefault(x => x.name == result);
            if (AethaModelSwap.selectedSpark == null && result != "default")
            {
                AethaModelSwap.selectedSpark = AethaModelSwap.RegisteredSparks.RandomElement();
            }
        }
        else if(AethaModelSwap.RegisteredSparks.Any())
        {
            AethaModelSwap.selectedSpark = AethaModelSwap.RegisteredSparks.RandomElement();
        }
    }

    public override void Save(ISettingsSaveLoad saver)
    {
        saver.SaveString("ModelSwapSparkSelection", AethaModelSwap.selectedSpark == null ? "default" : AethaModelSwap.selectedSpark.name);
    }

    public override void ApplyValue()
    {
    }

    public override SettingUI GetDebugUI(ISettingHandler settingHandler) => new EnumSettingsUI(this, settingHandler);

    public override GameObject GetSettingUICell() => InputCellMapper.Instance.EnumSettingCell;

    public List<LocalizedString> GetLocalizedChoices() => AethaModelSwap.RegisteredSparks.Select(x => x.LocalizedName).Prepend(new UnlocalizedString("Default Spark")).ToList();

    public List<string> GetUnlocalizedChoices() => AethaModelSwap.RegisteredSparks.Select(x => x.name).Prepend("Default Spark").ToList();

    public int GetValue()
    {
        if (AethaModelSwap.selectedSpark == null)
        {
            return 0;
        }
        return AethaModelSwap.RegisteredSparks.IndexOf(AethaModelSwap.selectedSpark) + 1;
    }

    public void SetValue(int v, ISettingHandler settingHandler, bool fromUI)
    {
        AethaModelSwap.selectedSpark = v == 0 ? null : AethaModelSwap.RegisteredSparks[v - 1];
        settingHandler.SaveSetting(this);
    }
}
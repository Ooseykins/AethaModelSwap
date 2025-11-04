using System.Reflection;
using Landfall.Haste;
using UnityEngine.Localization;
using Zorro.Settings;

namespace AethaModelSwapMod;

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
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AethaModelSwapMod;

[DefaultExecutionOrder(1000)]
public class FashionableWeebohHelpUI : LocationHelpUI<FashionableWeebohHelpUI>
{
    private static Sprite _icon;
    public static FashionableWeebohHelpUI instance;
    private static string IconPath => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/FashionableWeebohIcon.png";

    public static void AddToScene()
    {
        GameObject fashionableWeeboh = null;
        
        // Search based on if the weeboh enables/disables based on the fact
        // This might be more future proof or compatible with other addons
        // Easy change otherwise tbh
        foreach (var element in FindObjectsByType<DisableBasedOnFact>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (element.disableIf.fact == "fashion_weeboh_in_hub")
            {
                fashionableWeeboh = element.gameObject;
                element.gameObject.SetActive(true);
            }
        }

        // If she's in the scene, add the UI marker!
        if (fashionableWeeboh)
        {
            var helpTemplate = FindObjectsByType<PlayerNextShardHelpUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (!helpTemplate) return;

            // Toggle off the template to ensure no singleton awake shenanigans
            var enabledState = helpTemplate.gameObject.activeSelf;
            helpTemplate.gameObject.SetActive(false);
            var fashionHelp = Instantiate(helpTemplate.gameObject);
            helpTemplate.gameObject.SetActive(enabledState);
            DestroyImmediate(fashionHelp.GetComponent<PlayerNextShardHelpUI>());
            
            // Set references
            var helpUI = fashionHelp.AddComponent<FashionableWeebohHelpUI>();
            helpUI.point = fashionableWeeboh.transform;
            helpUI.p = helpUI.GetComponentInChildren<Image>(true).rectTransform;
            helpUI.text = helpUI.GetComponentInChildren<TextMeshProUGUI>(true);
            helpUI.scaleCurve = helpTemplate.scaleCurve;
            helpUI.fade = helpUI.GetComponentInChildren<CanvasGroup>(true);
            helpUI.GetComponentInChildren<Canvas>().sortingOrder -= 1;
            if (!_icon)
            {
                _icon = AethaModelSwap.LoadSprite(IconPath);
            }
            if (_icon)
            {
                foreach (var img in helpUI.GetComponentsInChildren<Image>(true))
                {
                    img.sprite = _icon;
                    img.color = Color.white;
                }
            }
            instance = helpUI;
            
            // If the setting is off or the icon is missing, don't enable it
            fashionHelp.SetActive(ShowFashionableWeebohHelpUI.value && _icon);
        }
    }
}
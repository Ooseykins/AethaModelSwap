using System.Linq;
using Landfall.Haste;
using UnityEngine;
using UnityEngine.UI;
using Zorro.Localization;

namespace AethaModelSwapMod;

public static class TutorialHelper
{
    public static void TriggerTutorial(int id, string header, string belowHeaderText = "", string mainText1 = "", string videoPath = "", string imagePath = "", float duration = 60f, bool ignoreFact = false)
    {
        if (TutorialPopUpHandler.Instance.tutorialPopUps.Any(x => (int)x.tutorialType == id))
        {
            TutorialPopUpHandler.Instance.TriggerPopUp((TutorialType)id, ignoreFact: ignoreFact);
            return;
        }
        var template = TutorialPopUpHandler.Instance.tutorialPopUps.FirstOrDefault(x => x.tutorialType == TutorialType.FindShard);
        if (template == null)
        {
            Debug.LogError($"No tutorial available for extra-tutorial {header} {belowHeaderText} {mainText1}");
            return;
        }

        var tutorial = Object.Instantiate(template.gameObject);
        SetText("Title",header);
        SetText("Text_1",belowHeaderText);
        SetText("Text_2",mainText1);
        SetText("Text_3", "");

        void SetText(string gameObjectName, string text)
        {
            var target = tutorial.GetComponentsInChildren<LocalizeUIText>(true).FirstOrDefault(x => x.name == gameObjectName);
            if (target)
            {
                target.gameObject.SetActive(!string.IsNullOrEmpty(text));
                target.String = new UnlocalizedString(text);
            }
        }
        
        var tutorialImage = tutorial.GetComponentsInChildren<RawImage>(true).FirstOrDefault(x => x.transform.parent.name == "ImageMask_1");
        if (tutorialImage)
        {
            var sprite = AethaModelSwap.LoadSprite(imagePath);
            tutorialImage.transform.parent.gameObject.SetActive(sprite);
            if (sprite)
            {
                tutorialImage.texture = sprite.texture;
            }
        }
        
        var videoPlayer = tutorial.GetComponentsInChildren<UnityEngine.Video.VideoPlayer>(true).FirstOrDefault(x => x.name == "Image");
        if (videoPlayer)
        {
            videoPlayer.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(videoPath));
            videoPlayer.url = videoPath;
            // Allow fallback to image-only on error
            videoPlayer.errorReceived += (source, message) =>
            {
                source.transform.parent.gameObject.SetActive(false);
            };
        }
        
        var tutorialPopup = tutorial.GetComponent<TutorialPopUp>();
        tutorialPopup.tutorialType = (TutorialType)id;
        tutorialPopup.duration = duration;
        TutorialPopUpHandler.Instance.tutorialPopUps.Add(tutorialPopup);
        TutorialPopUpHandler.Instance.TriggerPopUp((TutorialType)id, ignoreFact: ignoreFact);
    }
}
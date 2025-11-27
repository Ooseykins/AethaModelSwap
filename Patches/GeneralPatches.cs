using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Landfall.Haste;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using Zorro.ControllerSupport;
using Object = UnityEngine.Object;

namespace AethaModelSwapMod;

public static class GeneralPatches
{
    // For SkinSelectionListEntry patch
    public static SkinManager.Skin selectedHead;
    private static SkinManager.Skin _selectedBody;
    public static SkinManager.Skin prevSelectedHead;
    private static float _previewChangeTime;
    
    public static void Patch()
    {

        // Disable triggering items when the model editor is open
        On.Player.TryTriggerItem += (orig, self, id, type) => !ModelParamsEditor.IsEditorOpen && orig(self, id, type);
        
        // Disable the player's input when the model editor is open
        On.PlayerCharacter.PlayerInput.SampleInput += (orig, self, character, run) =>
        {
            orig(self, character, run);
            if (!ModelParamsEditor.IsEditorOpen) return;
            // Disable ability when the editor menu is open
            self.abilityIsPressed = false;
            self.abilityWasPressed = false;
            if (HasteInputSystem.Item1.GetKey())
            {
                // Hold right-click to move the camera in editor
                self.lookInput *= 1f;
            }
            else
            {
                // Keep camera still when in the editor
                self.lookInput = Vector3.zero;
            }
        };
        
        // Close the model editor on open the escape menu
        On.EscapeMenu.Open += (orig, self, disconnected) =>
        {
            orig(self, disconnected);
            ModelParamsEditor.ExitEditor();
        };

        // Move the scrollbar in the skin selection screen and give it a new material
        On.Landfall.Haste.SkinSelectionUI.Awake += (orig, self) =>
        {
            orig(self);
            
            // Get the base material from a button element of the UI
            Material baseMaterial =
                (from image in self.gameObject.GetComponentsInChildren<Image>(true)
                    where image.material.name == "M_UI_ItemTooltip_UnlockScreenRow"
                    select image.material).FirstOrDefault();

            // Make new materials to replace the scrollbars
            var bgMaterial = new Material(baseMaterial);
            var handleMaterial = new Material(baseMaterial);
            
            var bgColor = new Color(0.0483f, 0.0495f, 0.0566f, 1);
            var handleColor = new Color(0.0837f, 0.2672f, 0.3774f, 1);

            foreach (var scrollbar in self.gameObject.GetComponentsInChildren<Scrollbar>(true))
            {
                var bg = scrollbar.GetComponent<Image>();
                if (bg)
                {
                    bg.sprite = null;
                    bg.color = bgColor;
                    bg.material = bgMaterial;
                }
                var handle = scrollbar.targetGraphic.gameObject.GetComponent<Image>();
                if (handle)
                {
                    handle.sprite = null;
                    handle.color = handleColor;
                    handle.material = handleMaterial;
                }
                if (scrollbar.direction is Scrollbar.Direction.BottomToTop or Scrollbar.Direction.TopToBottom)
                {
                    ((RectTransform)scrollbar.transform).anchoredPosition = new Vector2(-94f, 0f);
                }
                scrollbar.navigation = new Navigation
                {
                    mode = Navigation.Mode.None
                };
            }
            foreach (var scrollRect in self.gameObject.GetComponentsInChildren<ScrollRect>(true))
            {
                scrollRect.gameObject.AddComponent<ScrollRectAutoScroller>();
            }
        };

        #region Unimportant
        #region Here be spoilers

        // Poisson d'avril !
        // If you've spoiled yourself, don't spoil it for anyone else!
        if (DateTime.Now.Month == 4 && DateTime.Now.Day == 1)
        {
            var verySpecialFont = TMP_FontAsset.CreateFontAsset("Comic Sans MS", "Bold");
            if (verySpecialFont)
            {
                var verySpecialFallbacks = new HashSet<TMP_FontAsset>();
                verySpecialFont.fallbackFontAssetTable = new List<TMP_FontAsset>();
                On.TMPro.TextMeshProUGUI.OnEnable += (orig, self) =>
                {
                    if (!verySpecialFont) return;
                    if (!verySpecialFallbacks.Contains(self.font))
                    {
                        verySpecialFallbacks.Add(self.font);
                        verySpecialFont.fallbackFontAssetTable.Add(self.font);
                    }
                    self.font = verySpecialFont;
                    self.fontStyle = FontStyles.UpperCase;
                    orig(self);
                };

                On.Febucci.UI.Core.TAnimCore.ConvertText += (orig, self, text, mode) =>
                {
                    if (verySpecialFont && 
                        LocalizationSettings.SelectedLocale.Identifier.Code.StartsWith("en") &&
                        !string.IsNullOrEmpty(text))
                    {
                        text = text.Replace("'", "");
                        text = text.Replace(",", "");
                        text = text.Replace("!", "!!!");
                        text = text.Replace("?", "?!");
                        text = text.Replace("...", "!!");
                        text = text.Replace(".", "!");
                    }
                    orig(self, text, mode);
                };
            }
        }

        #endregion
        #endregion
        // Make sure the user is a valid skin if they open the skin selection UI to prevent any index errors
        On.Landfall.Haste.SkinSelectionUI.RefreshUI += (orig, self) =>
        {
            AethaModelSwap.ValidateLocalSkin();
            if (ModelParamsEditor.IsEditorOpen)
            {
                ModelParamsEditor.ExitEditor();
            }
            _previewChangeTime = Time.realtimeSinceStartup;
            orig(self);
        };

        // Register all skins when the SkinDatabase is ready
        On.Landfall.Haste.SkinDatabase.Awake += (orig, self) =>
        {
            orig(self);
            AethaModelSwap.RegisterToSkinManager(self);
        };

        // Set the head skin as well when setting the body skin
        On.Landfall.Haste.SkinManager.SetBodySkin += (orig, skin) =>
        {
            if (Enum.IsDefined(typeof(SkinManager.Skin), skin))
            {
                orig(skin);
                return;
            }
            if (!AethaModelSwap.HasSkin((int)skin))
            {
                orig(SkinManager.Skin.Default);
                return;
            }
            SkinManager.HeadSkin = skin;
            FactSystem.SetFact(SkinManager.EquippedSkinHeadFact, (float) SkinManager.HeadSkin);
            orig(skin);
        };
        
        // Set the body skin after setting the head skin
        On.Landfall.Haste.SkinManager.SetHeadSkin += (orig, skin) =>
        {
            if (Enum.IsDefined(typeof(SkinManager.Skin), skin))
            {
                orig(skin);
                return;
            }
            if (!AethaModelSwap.HasSkin((int)skin))
            {
                orig(SkinManager.Skin.Default);
                return;
            }
            SkinManager.HeadSkin = skin;
            FactSystem.SetFact(SkinManager.EquippedSkinHeadFact, (float) SkinManager.HeadSkin);
            SkinManager.SetBodySkin(skin);
        };

        // When UI is refreshed reset the selected head/body and disable the button if it's a ModelSwap skin in the body slot
        On.Landfall.Haste.SkinSelectionListEntry.AssignSkinToListEntry += (orig, self, entry, id, slot) =>
        {
            orig(self, entry, id, slot);
            selectedHead = SkinManager.GetHeadSkinFromFacts();
            _selectedBody = SkinManager.GetBodySkinFromFacts();
            prevSelectedHead = selectedHead;
            self.gameObject.SetActive(slot == SkinManager.SkinSlot.Head || !AethaModelSwap.HasSkin((int)entry.Skin));
            foreach (var button in self.gameObject.GetComponentsInChildren<Button>(true))
            {
                button.gameObject.AddComponent<ScrollRectAutoScrollerElement>();
            }
            if (!ModelParamsEditor.GetFavouriteStarPrefab() || SkinManager.GetSkinStatus(entry.Skin) != SkinManager.SkinStatus.Purchased || slot == SkinManager.SkinSlot.Body)
            {
                return;
            }
            var newStarButton = Object.Instantiate(ModelParamsEditor.GetFavouriteStarPrefab(), new Vector3(0f, 0f, 0f), Quaternion.identity, self.transform).AddComponent<FavouriteSkinButton>();
            newStarButton.skin = entry.Skin;
            newStarButton.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            newStarButton.transform.localPosition = new Vector3(36f, 35f, 0f);
            newStarButton.gameObject.SetActive(true);
        };

        // Handle setting both head/body skin at once with a single button press
        On.Landfall.Haste.SkinSelectionListEntry.HandleButtonPress += (orig, self) =>
        {
            // Only handle things differently if the skin is unlocked and purchased
            var status = SkinManager.GetSkinStatus(self.skin.Skin);
            if (status == SkinManager.SkinStatus.Locked)
            {
                orig(self);
                return;
            }
            if (status == SkinManager.SkinStatus.Unlocked)
            {
                orig(self); 
                return;
            }
            
            _previewChangeTime = Time.realtimeSinceStartup;
            
            // Set both if we already have most recently selected or are now selecting a ModelSwap skin
            var updateBoth = AethaModelSwap.HasSkin((int)_selectedBody) || AethaModelSwap.HasSkin((int)selectedHead) || AethaModelSwap.HasSkin((int)self.skin.Skin);

            switch(self.slot)
            {
                case SkinManager.SkinSlot.Body:
                    _selectedBody = self.skin.Skin;
                    break;
                case SkinManager.SkinSlot.Head:
                    prevSelectedHead = selectedHead;
                    selectedHead = self.skin.Skin;
                    break;
            }

            if (!updateBoth)
            {
                orig(self);
                return;
            }
            
            var setSkinInfo = typeof(SkinSelectionListEntry).GetMethod("SelectSkin", BindingFlags.Instance | BindingFlags.NonPublic);
            if (setSkinInfo == null)
            {
                Debug.LogError("Reflection error no method SelectSkin found in SkinSelectionListEntry");
                orig(self);
                return;
            }

            _selectedBody = self.skin.Skin;
            selectedHead = self.skin.Skin;
        
            var prevSlot = self.slot;
            self.slot = SkinManager.SkinSlot.Head;
            setSkinInfo.Invoke(self, Array.Empty<object>());
            self.slot = SkinManager.SkinSlot.Body;
            setSkinInfo.Invoke(self, Array.Empty<object>());
            self.slot = prevSlot;
            // Does not call original method
        };
        
        On.SkinPreview3d.Update += (orig, self) =>
        {
            self.rectTransform.sizeDelta = new Vector2(600, 700);
            var angle = (Time.realtimeSinceStartup - _previewChangeTime) * 0.5f + (Mathf.PI * 0.5f);
            const float radius = 10f;
            self.cam.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, 2.5f, Mathf.Sin(angle) * radius);
            self.cam.transform.LookAt(self.courierRoot.transform.position + new Vector3(0f, 2.5f, 0f));
            orig(self);
        };
    }
}
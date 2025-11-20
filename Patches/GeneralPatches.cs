using System;
using System.Reflection;
using Landfall.Haste;
using UnityEngine;

namespace AethaModelSwapMod;

public static class GeneralPatches
{
    // For SkinSelectionListEntry patch
    private static SkinManager.Skin _selectedHead;
    private static SkinManager.Skin _selectedBody;
    
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
        
        // Make sure the user is a valid skin if they open the skin selection UI to prevent any index errors
        On.Landfall.Haste.SkinSelectionUI.RefreshUI += (orig, self) =>
        {
            AethaModelSwap.ValidateLocalSkin();
            if (ModelParamsEditor.IsEditorOpen)
            {
                ModelParamsEditor.ExitEditor();
            }
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
            _selectedHead = SkinManager.GetHeadSkinFromFacts();
            _selectedBody = SkinManager.GetBodySkinFromFacts();
            self.gameObject.SetActive(slot == SkinManager.SkinSlot.Head || !AethaModelSwap.HasSkin((int)entry.Skin));
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
            
            // Set both if we already have most recently selected or are now selecting a ModelSwap skin
            var updateBoth = AethaModelSwap.HasSkin((int)_selectedBody) || AethaModelSwap.HasSkin((int)_selectedHead) || AethaModelSwap.HasSkin((int)self.skin.Skin);

            switch(self.slot)
            {
                case SkinManager.SkinSlot.Body:
                    _selectedBody = self.skin.Skin;
                    break;
                case SkinManager.SkinSlot.Head:
                    _selectedHead = self.skin.Skin;
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
            _selectedHead = self.skin.Skin;
        
            var prevSlot = self.slot;
            self.slot = SkinManager.SkinSlot.Head;
            setSkinInfo.Invoke(self, Array.Empty<object>());
            self.slot = SkinManager.SkinSlot.Body;
            setSkinInfo.Invoke(self, Array.Empty<object>());
            self.slot = prevSlot;
            // Does not call original method
        };


    }
}
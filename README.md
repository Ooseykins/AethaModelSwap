
# Aetha Model Swap
This mod is a base for replacing the default Zoe model in Haste with user-imported models. Adjustments are done when the model is imported to match closely to the Zoe skeleton rig, then the model will update to match Zoe's animations. Some further configurable adjustments to arm, hand, and head angles. Then IK handles placing the feet to try and maintain a stride that matches Zoe's.

## On the Steam Workshop
The base mod: https://steamcommunity.com/sharedfiles/filedetails/?id=3508742571

Some example models: https://steamcommunity.com/sharedfiles/filedetails/?id=3599240067

## Creating models

[Here's the step-by-step guide!](Guides/StepByStep.md)

Models are exported/imported via Unity Asset Bundles. The asset bundle must have the .hastemodel filename extension. The Unity version shouldn't matter too much, but I tested with 2022.x and 6000.x. To be a valid model, the prefab name must have a number suffix (eg: Aetha.32100) to indicate which skin ID it uses. This should be totally unique otherwise it will fail to load. The model must also have an animator component with a humanoid avatar set.

There is a script for exporting the asset bundles included in this repository. Make sure BuildAssetBundles.cs is inside of a folder called Editor when putting it in Unity.

The UI icon should be a small square (256x256) .png file, named the same as the prefab (eg: Aetha.32100.png)

The configuration file can be created in game using the configuration interface (Settings -> General -> Open Model Editor)

## Multiplayer
Yep it works in multiplayer! If a player is using a custom skin that you don't have installed, they will appear as the default Zoe Courier skin. If another player doesn't have this mod, they will see you as the default skin.

## Extending this mod
Other mods can register models without using the .hastemodel filename extension by calling AethaModelSwap.RegisterSkin and AethaModelSwap.RegisterToSkinManager. It's preferred to use the overloads that take Func parameters, to allow the models to lazily load only when needed.

## Thanks!
If you create any mods using this, please let me see it! You can contact me on Discord @ooseykins or on Twitter @Ooseykins or @Aetha_Azazie

![](Guides/Screenshots/RizaSurfing.png)
![](Guides/Screenshots/CaptainSelect.png)
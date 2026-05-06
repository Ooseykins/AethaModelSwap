using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Landfall.Haste;
using UnityEngine;

namespace AethaModelSwapMod;

public static class AntiRiza
{
    public static InteractionCharacter antiRiza = null;
    public static InteractionPoolObject interactionPoolObject = null;
    private const string AntiRizaInteractionName = "AntiRiza_Intro";

    static void ExpandInteractionUICapacity(Sprite sprite, Dictionary<string,Sprite> faceSprites)
    {
        var prefabField = typeof(InteractionUI).GetField("m_characterContainer", BindingFlags.Instance | BindingFlags.NonPublic);
        if (prefabField == null)
        {
            Debug.LogError($"Unable to find image container prefab field for interactions");
            return;
        }
        var prefab = prefabField.GetValue(InteractionUI.Instance) as Transform;
        if (prefab == null)
        {
            Debug.LogError($"Unable to find image container prefab for interactions");
            return;
        }
        var prefabTemplate = prefab.GetChild(2); // Riza's entry
        if (prefabTemplate == null)
        {
            Debug.LogError($"Unable to find template image prefab for interactions");
            return;
        }
        var newEntry = UnityEngine.Object.Instantiate(prefabTemplate, prefab);
        newEntry.name = sprite.name;
        var image = newEntry.GetComponentInChildren<UnityEngine.UI.Image>(includeInactive: true);
        if (!image)
        {
            Debug.LogError($"No image exists on cloned template: {newEntry.name}");
            return;
        }
        image.sprite = sprite;
        if (faceSprites != null)
        {
            var face = image.GetComponentInChildren<InteractionCharacterFacialHolder>();
            foreach (var reaction in face.FacialHolders)
            {
                if (faceSprites.TryGetValue(reaction.expression.name, out var faceSprite))
                {
                    reaction.expression = faceSprite;
                }
            }
        }
    }

    static InteractionCharacter CreateNewInteractionCharacter(string name, Color color, Sprite mainSprite, Sprite icon, Dictionary<string, Sprite> reactionReplacements, InteractionVocalBank vocalBank)
    {
        var c = ScriptableObject.CreateInstance<InteractionCharacter>();
        c.name = name;
        c.DisplayName = new UnlocalizedString(name);
        c.textBoxIsOnRightSide = true;
        c.TalkingSpeed = InteractionCharacterDatabase.Instance.courier.TalkingSpeed;
        c.CharacterSprite = mainSprite;
        c.TalkingSprite = icon;
        c.CharacterColor = color;
        c.VocalBank = vocalBank;
        c.HasAbilityUnlock = false;
        
        ExpandInteractionUICapacity(c.CharacterSprite, reactionReplacements);
        return c;
    }

    static Interaction CreateInteraction(string name, InteractionCharacter owner, InteractionPriority priority, bool canBePlayedSeveralTimes, int cooldown = 1, bool hideCharacterAfterInteraction = false, FactSetter[] factsToSet = null, params (InteractionCharacter owner, string text)[] lines)
    {
        var i = ScriptableObject.CreateInstance<Interaction>();
        i.name = name;
        i.owningCharacter = owner;
        i.priority = priority;
        i.canBePlayedSeveralTimes = canBePlayedSeveralTimes;
        i.requirements = Array.Empty<Landfall.Haste.FactRequirement>();
        i.gameplayRequirements = Array.Empty<EncounterGameplayRequirement>();
        i.interactionPoolCooldown = cooldown;
        i.hideCharacterAfterInteraction = hideCharacterAfterInteraction;
        i.factsToSet = factsToSet ?? Array.Empty<FactSetter>();
        var newLines = new List<InteractionLine>();
        foreach (var line in lines)
        {
            newLines.Add(new InteractionLine
            {
                character = line.owner,
                line = new UnlocalizedString(line.text),
                requirements = Array.Empty<Landfall.Haste.FactRequirement>(),
            });
        }
        i.Lines = newLines.ToArray();
        var list = InteractionDatabase.Instance.Interactions.ToList();
        list.Add(i);
        InteractionDatabase.Instance.Interactions = list.ToArray();
        return i;
    }

    public static void CreateAntiRizaInteraction()
    {
        if (InteractionDatabase.GetInteraction(AntiRizaInteractionName))
        {
            return;
        }
        
        var zoe = InteractionCharacterDatabase.Instance.courier;
        var riza = InteractionCharacterDatabase.Instance.keeper;
        var hungryWeeboh = InteractionCharacterDatabase.Instance.animalHungry;
        
        var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var reactionDict = new Dictionary<string, Sprite>
        {
            ["Keeper_Reaction01"] = AethaModelSwap.LoadSprite(path + "/AntiRizaReaction01"),
            ["Keeper_Reaction02"] = AethaModelSwap.LoadSprite(path + "/AntiRizaReaction02")
        };
        antiRiza = CreateNewInteractionCharacter("Anti-Riza",
            new Color(0.5f, 0.2f, 0.7f, 1f),
            AethaModelSwap.LoadSprite(path + "/AntiRizaPortrait"),
            AethaModelSwap.LoadSprite(path + "/AntiRizaChibi"),
            reactionDict,
            InteractionCharacterDatabase.Instance.keeper.VocalBank);

        var interaction = CreateInteraction(AntiRizaInteractionName, riza, InteractionPriority.MainStoryOptional, true, 0, false, 
            new []{ new FactSetter()
            {
                factSetType = FactSetter.FactSetType.Set,
                factKey = "skin_status_16000131",
                value = 2f,
            }},
            (zoe, "<!?>Woah! Riza have you done something with your hair?"),
            (riza, "<confused>This is how it always looks."),
            (antiRiza, "<quiet>I think she meant me..."),
            (riza, "<astonished>Another Riza!? Are you my evil twin? <quiet>Or maybe I'm the evil twin?</quiet>"),
            (antiRiza, "<sad>I've been wandering around through fragments ever since The Collapse started. I'm not even sure how many realities I've been through."),
            (antiRiza, "<stars>You're the first other people I've met besides this wonderful accordionist."),
            (hungryWeeboh, "<blush>Scritch scritch!"),
            (zoe, "<confident-smile>You should come back with us! I know what it's like to have an evil twin, I'm sure you two can get used to it!"),
            (riza, "<smile>Yeah! And bring your weeboh friend too!"),
            (hungryWeeboh, "<angry>Snooot."),
            (antiRiza, "<uncertain>I think this weeboh would rather stay. If it's really OK with you I'd like to go meet the rest of your friends."),
            (antiRiza, "<quiet><size=small>And for the record: the green one is the evil twin.</size>"),
            (riza, "<angry><size=large>Hey!</size>"));
        
        interactionPoolObject = ScriptableObject.CreateInstance<InteractionPoolObject>();
        interactionPoolObject.name = "AntiRizaEasterEgg";
        interactionPoolObject.interactions = new List<Interaction> { interaction };
        interactionPoolObject.requirements = new Landfall.Haste.FactRequirement[] { new()
        {
            factSource = Landfall.Haste.FactRequirement.KeySource.Fact,
            condition = Landfall.Haste.FactRequirement.Condition.Equal,
            valueType = Landfall.Haste.FactRequirement.ValueType.Float,
            fact = SkinManager.EquippedSkinBodyFact.key,
            value = 16000124, // Playing as this skin
        }};
    }
}
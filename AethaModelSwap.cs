using UnityEngine;
using HarmonyLib;
using Landfall.Modding;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Landfall.Haste;
using Zorro.Core.CLI;
using Object = UnityEngine.Object;

namespace AethaModelSwapMod;

[LandfallPlugin]
public class AethaModelSwap
{
    private const string Guid = "Aetha.ModelSwap";
    private static string AssemblyDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    public static string EditorAssetBundlePath =>  $"{AssemblyDirectory}\\modeleditorui.assetbundle";
    public static HasteClone LocalClone { get; internal set; }

    private static readonly Dictionary<int, RegisteredSkin> RegisteredSkins = new();
    private static readonly HashSet<AssetBundle> LoadedBundles = new();

    private class RegisteredSkin
    {
        public string name;
        public Sprite sprite;
        public Func<ModelIKParameters> modelIKParameters;
        public Func<GameObject> loadPrefab;
        public GameObject cachedPrefab;
    }

    public static bool HasSkin(int index) => RegisteredSkins.ContainsKey(index);
    public static string GetName(int index) => RegisteredSkins.ContainsKey(index) ? RegisteredSkins[index].name : "";
    public static Sprite GetSprite(int index) => RegisteredSkins.ContainsKey(index) ? RegisteredSkins[index].sprite : null;

    static AethaModelSwap()
    {
        ConsoleCommands.ConsoleCommandMethods.Add(new ConsoleCommand(new Action(ModelParamsEditor.OpenEditor).Method));
        // Load skins from all mod directories (including this one)
        foreach (var item in Modloader.LoadedItemDirectories)
        {
            LoadSkins(item.Value.directory);
        }
        // Hot load newly subscribed models
        Modloader.OnItemLoaded += t =>
        {
            if (Modloader.GetDirectoryFromFileId(t, out var directory, out var isOverride))
            {
                LoadSkins(directory);
                if (SkinDatabase.me)
                {
                    RegisterToSkinManager(SkinDatabase.me);
                }
            }
        };
        new Harmony(Guid).PatchAll();
    }

    public static void ValidateLocalSkin()
    {
        if (!SkinDatabase.me) return;
        var bodySkin = (SkinManager.Skin)FactSystem.GetFact(SkinManager.EquippedSkinBodyFact);
        var headSkin = (SkinManager.Skin)FactSystem.GetFact(SkinManager.EquippedSkinHeadFact);
        if ((!HasSkin((int)bodySkin) && !Enum.IsDefined(typeof(SkinManager.Skin), bodySkin)) 
            || (!HasSkin((int)headSkin) && !Enum.IsDefined(typeof(SkinManager.Skin), headSkin)))
        {
            Debug.LogWarning($"Correcting local skin assigned to unavailable index {bodySkin} and {headSkin}");
            SkinManager.BodySkin = SkinManager.Skin.Default;
            SkinManager.HeadSkin = SkinManager.Skin.Default;
            FactSystem.SetFact(SkinManager.EquippedSkinBodyFact, (float) SkinManager.Skin.Default);
            FactSystem.SetFact(SkinManager.EquippedSkinHeadFact, (float) SkinManager.Skin.Default);
            if (Player.localPlayer && PlayerCharacter.localPlayer.refs != null && PlayerCharacter.localPlayer.refs.playerSkinSetter)
            {
                SkinManager.SetBodySkin(SkinManager.Skin.Default);
            }
        }
    }

    public static void ResetSkin()
    {
        if (Player.localPlayer && PlayerCharacter.localPlayer.refs != null && PlayerCharacter.localPlayer.refs.playerSkinSetter)
        {
            SkinManager.SetFullOutfit(SkinManager.Skin.Default);
        }
    }

    public static void NextSkin()
    {
        if (RegisteredSkins.Count == 0)
        {
            ResetSkin();
        }
        int currentSkin = (int)FactSystem.GetFact(SkinManager.EquippedSkinBodyFact);
        foreach (var kv in RegisteredSkins.OrderBy(x => x.Key))
        {
            if (kv.Key > currentSkin)
            {
                SetSkin(kv.Key);
                return;
            }
        }
        ResetSkin();
    }
    
    public static void PreviousSkin()
    {
        if (RegisteredSkins.Count == 0)
        {
            ResetSkin();
        }
        int currentSkin = (int)FactSystem.GetFact(SkinManager.EquippedSkinBodyFact);
        if (currentSkin == 0)
        {
            currentSkin = Int32.MaxValue;
        }
        foreach (var kv in RegisteredSkins.OrderBy(x => x.Key).Reverse())
        {
            if (kv.Key < currentSkin)
            {
                SetSkin(kv.Key);
                return;
            }
        }
        ResetSkin();
    }
    
    public static void SetSkin(int index)
    {
        if (Player.localPlayer && PlayerCharacter.localPlayer.refs != null && PlayerCharacter.localPlayer.refs.playerSkinSetter)
        {
            SkinManager.SetBodySkin((SkinManager.Skin)index);
            SkinManager.SetHeadSkin((SkinManager.Skin)index);
        }
    }

    public static void LoadSkins(string directory)
    {
        foreach (var path in Directory.GetFiles(directory).Where(x => x.Contains(".hastemodel")))
        {
            Debug.Log($"Loading model bundle {path}");
            var bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
            {
                Debug.Log($"Failed to load bundle {path}");
                continue;
            }

            foreach (var prefab in bundle.LoadAllAssets<GameObject>())
            {
                Debug.Log($"Loading prefab {prefab.name}");
                var anim = prefab.GetComponentInChildren<Animator>();
                if (!anim)
                {
                    Debug.Log($"No animator on prefab {prefab.name}");
                    continue;
                }
                if (!anim.avatar || !anim.avatar.isHuman)
                {
                    Debug.Log($"No human avatar on prefab {prefab.name}");
                    continue;
                }

                var splitPoint = prefab.name.LastIndexOf('.');
                if (splitPoint <= -1)
                {
                    Debug.Log($"Bad prefab naming for {prefab.name}, format should be Name.Number like Aetha.41");
                    continue;
                }
                var prefabName = prefab.name.Substring(0, splitPoint);
                var prefabIndex = prefab.name.Substring(splitPoint+1);
                
                if (string.IsNullOrEmpty(prefabName) || string.IsNullOrEmpty(prefabIndex))
                {
                    Debug.Log($"Bad prefab naming for {prefab.name}, format should be Name.Number like Aetha.41");
                    continue;
                }
                if (!int.TryParse(prefabIndex, out var index))
                {
                    Debug.Log($"Could not parse index number for {prefab.name}");
                    continue;
                }
                if (Enum.IsDefined(typeof(SkinManager.Skin), index))
                {
                    Debug.Log($"Skin enum value index {index} is already defined by {(SkinManager.Skin)index}");
                    continue;
                }
                if (RegisteredSkins.ContainsKey(index))
                {
                    Debug.Log($"Skin prefabs already contains index {index}");
                    continue;
                }

                var regDirectory = directory;
                var regPath = path;
                var regName = prefab.name;

                var sprite = LoadSprite($"{regDirectory}\\{regName}");
                
                RegisterSkin(index, 
                    prefabName, 
                    sprite, 
                    () =>
                    {
                        var modelIKParametersPath = $"{regDirectory}\\{regName}.json";
                        var modelIKParameters = ModelIKParameters.LoadModelIKParameters(modelIKParametersPath);
                        modelIKParameters.savePath = modelIKParametersPath;
                        return modelIKParameters;
                    }, 
                    () =>
                    {
                        if (!bundle)
                        {
                            bundle = AssetBundle.LoadFromFile(regPath);
                        }
                        LoadedBundles.Add(bundle);
                        Debug.Log($"Lazily loading prefab: {regName}");
                        var skin = bundle.LoadAsset<GameObject>(regName);
                        return skin;
                    });
            }
            bundle.Unload(true);
        }
    }
    
    public static void UnloadAllBundles()
    {
        foreach (var bundle in LoadedBundles)
        {
            if (bundle)
            {
                bundle.Unload(true);
            }
        }
    }

    // For other mod devs, this is a simpler way to just register a skin
    public static void RegisterSkin(int index, string name, Sprite sprite, ModelIKParameters modelIKParameters, GameObject prefab)
    {
        RegisterSkin(index, name, sprite, () => modelIKParameters, () => prefab);
    }

    // Passing a function in here to get the prefab allows it to lazily load
    public static void RegisterSkin(int index, string name, Sprite sprite, Func<ModelIKParameters> modelIKParameters, Func<GameObject> prefab)
    {
        if (RegisteredSkins.ContainsKey(index))
        {
            Debug.LogError($"A skin is already registered to index {index}");
        }

        RegisteredSkins[index] = new RegisteredSkin
        {
            name = name,
            sprite = sprite,
            modelIKParameters = modelIKParameters,
            loadPrefab = prefab,
        };

        Debug.Log($"Successfully registered skin {index}: {name}");
    }

    // Add all skins to the skin manager
    // Call this again if your mod registers skins later
    public static void RegisterToSkinManager(SkinDatabase instance)
    {
        ValidateLocalSkin();
        // Instantiate new skin database entries
        // These are scriptable objects so we use the default skin as a base
        Dictionary<int, SkinDatabaseEntry> newEntries = new();
        foreach (var skin in RegisteredSkins)
        {
            var newEntry = Object.Instantiate(instance.Skins[0]);
            newEntry.Name = new UnlocalizedString(skin.Value.name);
            newEntry.Skin = (SkinManager.Skin)skin.Key;
            newEntry.BodyPrefab = instance.GetSkin(0).BodyPrefab;
            newEntry.HeadPrefab = instance.GetSkin(0).HeadPrefab;
            if (skin.Value.sprite)
            {
                newEntry.PlayerIcon = skin.Value.sprite;
                newEntry.UIIcon = skin.Value.sprite;
                newEntry.UIBodyIcon = skin.Value.sprite;
            }
            newEntry.skinPurchaseCost = 0;
            newEntries[skin.Key] = newEntry;
        }

        // Overwrite existing skins
        for (int i = 0; i < instance.Skins.Length; i++)
        {
            if (newEntries.TryGetValue((int)instance.Skins[i].Skin, out var newEntry))
            {
                instance.Skins[i] = newEntry;
                newEntries.Remove((int)instance.Skins[i].Skin);
            }
        }

        // Done this way because Odin Inspector was being funny with linq
        List<SkinDatabaseEntry> skinsList = new();
        foreach (var entry in instance.Skins)
        {
            skinsList.Add(entry);
        }
        foreach (var entry in newEntries.Values)
        {
            skinsList.Add(entry);
        }
        instance.Skins = skinsList.ToArray();

        foreach (var entry in instance.Skins)
        {
            // Automatically unlock all modded skins. Consider changing this if we want mod skins to have unlock conditions
            if (!Enum.IsDefined(typeof(SkinManager.Skin), entry.Skin))
            {
                SkinManager.UnlockSkin(entry.Skin, false);
                SkinManager.PurchaseSkin(entry.Skin);
            }
        }
    }
    
    // Helper method to just load an image file as a sprite
    public static Sprite LoadSprite(string path)
    {
        string[] extensions = { "", ".png", ".jpg", ".jpeg", ".exr" };
        foreach (var ext in extensions)
        {
            string altPath = path + ext;
            if (File.Exists(altPath))
            {
                byte[] data = File.ReadAllBytes(altPath);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(data, true))
                {
                    return Sprite.Create(tex, new Rect(0,0,tex.width,tex.height), new Vector2(tex.width/2f,tex.height/2f));
                }
            }
        }
        return null;
    }

    // Instantiate a skin, 
    public static HasteClone InstantiateSkin(Transform instance, int index, bool isLocalPlayer)
    {
        if (!HasSkin(index))
        {
            Debug.Log($"Tried to set skin to invalid index {index}");
            return null;
        }

        // Load and cache a skin's prefab
        var skin = RegisteredSkins[index];
        var prefab = skin.cachedPrefab;
        if (!prefab)
        {
            skin.cachedPrefab = skin.loadPrefab?.Invoke();
            prefab = skin.cachedPrefab;
            if (!prefab)
            {
                Debug.Log($"Something went wrong loading skin {index}");
                return null;
            }
        }
        
        var newObj = Object.Instantiate(prefab);
        var newClone = newObj.AddComponent<HasteClone>();
        newClone.name = skin.name;
        newClone.modelIKParameters = skin.modelIKParameters?.Invoke();
        newClone.Setup(instance, newObj.transform, index);
        
        if (isLocalPlayer)
        {
            LocalClone = newClone;
            ModelParamsEditor.ResetFields();
        }
        return newClone;
    }
}


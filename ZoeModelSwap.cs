using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Landfall.Haste;
using UnityEngine;

namespace AethaModelSwapMod;

public static class ZoeModelSwap
{
    private static string AssemblyDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    private static Dictionary<int, (string name, GameObject head, GameObject body)> _instantiatedPrefabs = new();
    private static Dictionary<int, (string name, int id, int baseSkinId, Sprite playerIcon, Sprite uiIcon, Sprite bodyIcon)> _baseSkinVariants = new();

    private static GameObject HeadSkin;
    private static GameObject BodySkin;
    private static string ZoeAssetBundle() {
        switch(Application.platform)
        {
            case RuntimePlatform.WindowsPlayer:
                return $"{AssemblyDirectory}/zoe.windows.assetbundle";
            case RuntimePlatform.LinuxPlayer:
                return $"{AssemblyDirectory}/zoe.linux.assetbundle";
            case RuntimePlatform.OSXPlayer:
                return $"{AssemblyDirectory}/zoe.mac.assetbundle";
            default:
                return $"{AssemblyDirectory}/zoe.windows.assetbundle";
        }
    }
    
    public static void RegisterZoeSkins(SkinDatabase instance)
    {
        foreach (var (skinId, value) in _instantiatedPrefabs)
        {
            AddToSkinDatabase(instance, value.name, skinId, value.head, value.body, AethaModelSwap.LoadSprite($"{AssemblyDirectory}/{value.name}HeadIcon"), AethaModelSwap.LoadSprite($"{AssemblyDirectory}/{value.name}BodyIcon"), AethaModelSwap.LoadSprite($"{AssemblyDirectory}/{value.name}PlayerIcon"));
            SkinManager.PurchaseSkin((SkinManager.Skin)skinId);
        }
        foreach (var (_, variant) in _baseSkinVariants)
        {
            var baseSkin = instance.GetSkin((SkinManager.Skin)variant.baseSkinId);
            AddToSkinDatabase(instance, variant.name, variant.id, baseSkin.HeadPrefab, baseSkin.BodyPrefab, variant.uiIcon, variant.bodyIcon, variant.playerIcon);
            SkinManager.PurchaseSkin((SkinManager.Skin)variant.id);
        }
    }

    public static void AddSkinVariant(string name, int id, int baseSkinId, Sprite playerIcon = null, Sprite uiIcon = null, Sprite bodyIcon = null)
    {
        _baseSkinVariants[id] = (name, id, baseSkinId, playerIcon, uiIcon, bodyIcon);
    }

    public static void AddToSkinDatabase(SkinDatabase skinDatabase,
        string name,
        int id,
        GameObject headPrefab,
        GameObject bodyPrefab,
        Sprite UIIcon = null,
        Sprite UIBodyIcon = null,
        Sprite PlayerIcon = null,
        int skinPurchaseCost = 0)
    {
        var newEntry = Object.Instantiate(skinDatabase.Skins[0]);
        newEntry.name = name;
        newEntry.Name = new UnlocalizedString(name);
        newEntry.Skin = (SkinManager.Skin)id;
        newEntry.HeadPrefab = headPrefab;
        newEntry.BodyPrefab = bodyPrefab;
        if (UIIcon)
        {
            newEntry.UIIcon = UIIcon;
        }
        if (UIBodyIcon)
        {
            newEntry.UIBodyIcon = UIBodyIcon;
        }
        if (PlayerIcon)
        {
            newEntry.PlayerIcon = PlayerIcon;
        }
        newEntry.skinPurchaseCost = 0;
        
        // Done this way because Odin Inspector was being funny with linq
        List<SkinDatabaseEntry> skinsList = new();
        foreach (var entry in skinDatabase.Skins)
        {
            skinsList.Add(entry);
        }
        var original = skinDatabase.GetSkin((SkinManager.Skin)id);
        if (original && original.Skin != SkinManager.Skin.Default)
        {
            skinsList.Remove(original);
            Debug.LogWarning($"Replaced already loaded skin: {original.name} {id}");
        }
        skinsList.Add(newEntry);
        skinDatabase.Skins = skinsList.ToArray();
    }

    public static void InstantiatePrefab(string name, int id)
    {
        if (_instantiatedPrefabs.ContainsKey(id))
        {
            return;
        }
        if (!SkinDatabase.me)
        {
            Debug.LogWarning($"Skin database is not available for skin template for {name}");
            return;
        }
        if (SkinDatabase.me.GetSkin((SkinManager.Skin)id) && SkinDatabase.me.GetSkin((SkinManager.Skin)id).Skin != SkinManager.Skin.Default)
        {
            Debug.LogWarning($"Skin id already registered to database for {id}");
            return;
        }
        var templateSkin = SkinDatabase.me.GetSkin(SkinManager.Skin.Default);
        var newPrefabs = InstantiateZoePrefabs(templateSkin.HeadPrefab, templateSkin.BodyPrefab, name, false);
        if (newPrefabs != null)
        {
            newPrefabs.Value.head.SetActive(false);
            newPrefabs.Value.body.SetActive(false);
            _instantiatedPrefabs[id] = (name, newPrefabs.Value.head, newPrefabs.Value.body);
        }
    }
    
    public static (GameObject head, GameObject body)? InstantiateZoePrefabs(GameObject templateHead, GameObject templateBody, string prefabName, bool applyToOriginal = false)
    {
        var bundle = AssetBundle.LoadFromFile(ZoeAssetBundle());
        if (bundle == null)
        {
            Debug.LogWarning($"Failed to load bundle {ZoeAssetBundle()}");
            return null;
        }
        var peakZoePrefab = bundle.LoadAsset<GameObject>(prefabName);
        if (!peakZoePrefab)
        {
            Debug.LogWarning($"No prefab in bundle {bundle.name} named {prefabName}");
            bundle.Unload(false);
            return null;
        }
        var templateMaterial = templateBody.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(x => x.name == "Body")?.material;
        if (!templateMaterial)
        {
            Debug.LogWarning($"No template material for Zoe");
            bundle.Unload(false);
            return null;
        }
        
        var peakZoeHead = peakZoePrefab.transform.Find("Head")?.gameObject;
        var peakZoeBody = peakZoePrefab.transform.Find("Body")?.gameObject;
        bundle.Unload(false);
        
        var head = templateHead;
        if (!applyToOriginal)
        {
            head = Object.Instantiate(templateHead);
            Object.DontDestroyOnLoad(head);
        }
        AddExtras(peakZoeHead, head.transform, templateMaterial, new []{ "Body", "Hat", "Camera (1)", "BagLid" });
        SkinnedMeshRendererBonesByName.FixZoeBones(head.transform.root.gameObject);
        
        var body = templateBody;
        if (!applyToOriginal)
        {
            body = Object.Instantiate(templateBody);
            Object.DontDestroyOnLoad(body);
        }
        AddExtras(peakZoeBody, body.transform, templateMaterial, new []{ "Body", "Hat", "Camera (1)", "BagLid" });
        SkinnedMeshRendererBonesByName.FixZoeBones(body.transform.root.gameObject);
        
        return (head, body);
    }

    public static void MainMenuApplySkin(string prefabName, bool hideHat = false)
    {
        var bodyTarget = GameObject.Find("Courier");
        if (!bodyTarget || !bodyTarget.activeInHierarchy)
        {
            Debug.LogWarning($"Could not find Zoe model in main scene for replacement");
            return;
        }
        InstantiateZoePrefabs(bodyTarget, bodyTarget, prefabName, true);
        if (hideHat)
        {
            var headRenderer = bodyTarget.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(x => x.name == "Head");
            if (headRenderer && headRenderer.materials.Length >= 3)
            {
                var noRenderMat = new Material(Shader.Find("Standard"));
                for (int i = 0; i < noRenderMat.passCount; i++)
                {
                    noRenderMat.SetShaderPassEnabled(noRenderMat.GetPassName(i), false);
                }
                var materials = headRenderer.materials.ToList();
                materials[0] = noRenderMat;
                materials[1] = noRenderMat;
                materials[2] = noRenderMat;
                headRenderer.SetMaterials(materials);
            }
        }
    }

    public static void AddExtras(GameObject prefab, Transform parent, Material templateMaterial, string[] disableRenderers = null)
    {
        if (prefab)
        {
            var newObj = Object.Instantiate(prefab, parent);
            foreach (var smr in newObj.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var newMaterials = new List<Material>();
                foreach (var m in smr.materials)
                {
                    if (m.IsKeywordEnabled("_ALPHATEST_ON"))
                    {
                        newMaterials.Add(m);
                        continue;
                    }
                    var newMat = new Material(templateMaterial);
                    newMat.mainTexture = m.mainTexture;
                    newMat.SetTexture("_Normal",m.GetTexture("_BumpMap"));
                    newMat.SetTexture("_Metal",m.GetTexture("_MetallicGlossMap"));
                    newMat.SetTexture("_Emission",m.GetTexture("_EmissionMap"));
                    newMaterials.Add(newMat);
                }
                smr.SetMaterials(newMaterials);
            }
        }
        if (disableRenderers != null)
        {
            foreach (var smr in parent.GetComponentsInChildren<Renderer>())
            {
                if (disableRenderers.Contains(smr.name))
                {
                    smr.enabled = false;
                }
            }
        }
    }
}
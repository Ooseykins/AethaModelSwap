using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Landfall.Haste;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace AethaModelSwapMod;

public static class TextureSwap
{
    private static readonly Dictionary<string, string> AvailableTextures = new();
    private static readonly Dictionary<(int skin, int palette), SkinReplacementParams> AvailableSkinPalettes = new();

    private static readonly HashSet<(GameObject root, List<Texture2D> textures)> InstantiatedTextures = new();

    private static readonly string[] MainTexturePropertyNames = { "_MainTex", "_MainTex1" };
    
    // Renderers that shouldn't be part of a skin
    private static readonly string[] ExcludeRendererNames = {
        "Intense_L",
        "Intense_R",
        "Intense_1",
        "Intense_2",
        "Camera (1)",
        "hb_handles_low",
        "hb_handles_low (1)",
        "hb_main_low",
        "hb_main_low.001",
        "hb_center_disc_low",
        "hb_center_panel_low",
        
        "sheath_low (1)", // Niada's spear
        "sword_low (1)",
        "spear (1)",
        "sword_low",
        "spear",
        
        "evilcoin_wobble_1", // Ava's anti-spark
    };
    
    // Simplified names to make it easier to 
    private static readonly (string actual,string simplified)[] TextureSimplifiedNamesBase = {
        // Default & other basic skins
        ("courier_c_clothes_BaseColor", "Clothes"),
        ("courier_c_eyesmouth_BaseColor", "EyesAndMouth"),
        ("2x2_Texture", "Teeth"),
        ("c_bags_basecolor_24", "Bag"),
        ("T_Agency_World_MetalGranulatedFinal_Diffuse_Pink", "Tongue"),
        ("c_hair_basecolor_24", "Hair"),
        ("T_Hub_RockLayered_Diffuse_1", "GlassesShine"),
        ("courier_c_skin_BaseColor", "SkinAndFace"),
        ("courier_c_bags_BaseColor", "Bag"),
        ("courier_c_hair_BaseColor", "Hair"),
        
        // Wobbler
        ("wobblerTexturing_Wobbler_Body_BaseColor", "WobblerOutfit"),
        ("wobblerTexturing_c_bags_BaseColor", "WobblerBag"),

        // Clown
        ("clowntexturing2_c_skin_BaseColor", "SkinAndFace"),
        ("clowntexturing2_c_clothes_BaseColor", "ClownOutfit"),
        ("clowntexturing2_clownzoe_BaseColor", "ClownAccessories"),
        ("clowntexturing2_clownzoe_ALTCOLOR", "ClownAccessoriesAlt"),
        ("clowntexturing2_c_clothes_ALTCOLOR", "ClownOutfitAlt"),
        ("clowntexturing2_c_hair_BaseColor", "Hair"),
        
        // Weeboh
        ("weebohtexturing_weeboh_body_BaseColor", "WeebohOutfit"),

        // 64
        ("courier_c_clothes_BaseColor n64", "64Outfit"),
        ("c_bags_basecolor_24 n64", "64Bag"),
        ("c_hair_basecolor_24 n64", "64Hair"),
        ("t_64Zoe", "64Reference"),
    };

    private static readonly (string actual, string simplified)[] TextureSimplifiedNamesHub =
    {
        ("capttexturing2_captmain_BaseColor", "TheCaptain"),
        ("capttexturing2_captEyes_BaseColor", "HisBeautifulEyes"),
        
        ("sagetexturing3_sage_BaseColor", "Daro"),
        ("sagetexturing3_sage2_BaseColor", "DaroSkinAndFace"),
        
        ("NEWHEADFORTEXTURING_body_BaseColor", "Niada"),
        ("NEWHEADFORTEXTURING_eyes_BaseColor", "NiadaEyes"),
        
        ("riza_main_Base_color", "Riza"),
        ("riza_acc_Base_color", "RizaAccessories"),
        
        ("Ava_Main_Base_color", "Ava"),
        ("M_Ava_Accessories_Base_color", "AvaAccessories"),
        
        ("researcherTexturing3normalcheck_r_main_BaseColor", "Gan"),
        ("researcherTexturing3normalcheck_r_props_BaseColor", "GanAccessoriesAndDalil"),
        
        ("wraithMetalCloth_Base_color", "WraithMetal"),
        ("wraithOrganic_Base_color", "Wraith"),
        ("wraithArms_Base_color", "WraithArms"),
        ("wraithCloth_Base_color", "WraithCloth"),
        ("wraithArms_Base_color", "WraithArmsFade"),
        
        ("m_grunt_Base_color", "GruntMasc"),
        ("f_grunt_Base_color", "GruntFem"),
        ("sharedUVs_Base_color", "GruntAccessories"),
        
        ("fashionweeboh_Base_color", "FashionableWeeboh"),
    };

    // Textures excluded for being probably not worth modifying
    private static readonly string[] SimpleExcludeTextures = new[]
    {
        "2x2_Texture", // Teeth
        "T_Agency_World_MetalGranulatedFinal_Diffuse_Pink", // Tongue
        "T_Hub_RockLayered_Diffuse_1", // Glasses shine?
    };
    
    private static string NewSkinDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)+"/Skins";
    private const string SkinPaletteSuffix = "_SkinPaletteConfig.json";

    private static int _overridePalette = -1;

    public static void SetOverridePalette(int value) => _overridePalette = value;

    public enum SkinCreationMode
    {
        Simple, // Copy just _MainTex type textures
        Textures, // Copy all textures
        Advanced, // Copy all parameters with null values
        CopyFull, // Copy all parameters with copied values
        CopyLess, // Ignore _ST, _TexelSize, and _HDR properties
    }
    
    private class SkinReplacementParams
    {
        [JsonProperty] public string skinName = "";
        [JsonProperty] public int skinId = -1;
        [JsonProperty] public int paletteId = -1;
        [JsonProperty] public List<RendererReplacementParams> rendererReplacementParamsList = new();

        public void Apply(GameObject root, bool body = true, bool head = true)
        {
            Dictionary<string, Texture2D> createdTextures = new();
            List<(RendererReplacementParams replacement, Renderer renderer)> rendererPairs = new();
            foreach (var renderer in GetRenderers(root))
            {
                if (!body || !head)
                {
                    var isHead = IsHead(renderer.transform);
                    if (!body && !isHead)
                    {
                        continue;
                    }
                    if (!head && isHead)
                    {
                        continue;
                    }
                }
                var replacementParams = rendererReplacementParamsList.FirstOrDefault(x => x.rendererName == renderer.name);
                if (replacementParams != null)
                {
                    rendererPairs.Add((replacementParams, renderer));
                }
            }
            foreach (var pair in rendererPairs)
            {
                pair.replacement.Apply(pair.renderer, createdTextures);
            }
            InstantiatedTextures.Add((root, createdTextures.Values.ToList()));
        }
    }

    private static void CleanupAll()
    {
        foreach (var entry in InstantiatedTextures)
        {
            if (!entry.root)
            {
                foreach (var texture in entry.textures)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
        }
        InstantiatedTextures.RemoveWhere(x => !x.root);
    }

    private class RendererReplacementParams
    {
        [JsonProperty] public string rendererName = "";
        [JsonProperty] public List<MaterialReplacementParams> materialParams = new();

        public void Apply(Renderer renderer, Dictionary<string, Texture2D> createdTextures)
        {
            for (int i = 0; i < renderer.materials.Length; i++)
            {
                if (!renderer.materials[i])
                {
                    continue;
                }
                if (i >= materialParams.Count)
                {
                    Debug.LogError($"Material params count mismatch: {renderer.materials.Length} on renderer, {materialParams.Count} in config");
                    return;
                }
                materialParams[i].Apply(renderer.materials[i], createdTextures);
            }
        }
    }
    
    private class MaterialReplacementParams
    {
        [JsonProperty] public string materialName = "";
        [JsonProperty] public string shader = "";
        [JsonProperty] public Dictionary<string, float?> floats = new();
        [JsonProperty] public Dictionary<string, Vector4?> vectors = new();
        [JsonProperty] public Dictionary<string, string> textures = new();

        public MaterialReplacementParams()
        {
            
        }

        public MaterialReplacementParams(Material original, SkinCreationMode mode)
        {
            materialName = original.name;
            shader = original.shader.name;
            if (mode is SkinCreationMode.Advanced or SkinCreationMode.CopyFull or SkinCreationMode.CopyLess)
            {
                foreach (var prop in original.GetPropertyNames(MaterialPropertyType.Float))
                {
                    floats[prop] = null;
                    if (mode is not (SkinCreationMode.CopyFull or SkinCreationMode.CopyLess))
                    {
                        continue;
                    }
                    if (mode is SkinCreationMode.CopyLess && (prop.EndsWith("_ST") || prop.EndsWith("_TexelSize") || prop.EndsWith("_HDR")))
                    {
                        continue;
                    }
                    floats[prop] = original.GetFloat(prop);
                }
                foreach (var prop in original.GetPropertyNames(MaterialPropertyType.Vector))
                {
                    vectors[prop] = null;
                    if (mode is not (SkinCreationMode.CopyFull or SkinCreationMode.CopyLess))
                    {
                        continue;
                    }
                    if ((mode is SkinCreationMode.CopyLess) && (prop.EndsWith("_ST") || prop.EndsWith("_TexelSize") || prop.EndsWith("_HDR")))
                    {
                        continue;
                    }
                    vectors[prop] = original.GetVector(prop);
                }
            }
            foreach (var prop in original.GetTexturePropertyNames())
            {
                if (mode is SkinCreationMode.Simple && !MainTexturePropertyNames.Contains(prop)) {
                    continue;
                }
                textures[prop] = null;
            }
        }

        public void Apply(Material material, Dictionary<string, Texture2D> createdTextures)
        {
            if (floats != null)
            {
                foreach (var kvp in floats)
                {
                    if (kvp.Value == null)
                    {
                        continue;
                    }
                    material.SetFloat(kvp.Key, kvp.Value.Value);
                }
            }
            if (vectors != null)
            {
                foreach (var kvp in vectors)
                {
                    if (kvp.Value == null)
                    {
                        continue;
                    }
                    material.SetVector(kvp.Key, kvp.Value.Value);
                }
            }
            if (textures != null)
            {
                foreach (var (property, filename) in textures)
                {
                    if (string.IsNullOrEmpty(filename))
                    {
                        continue;
                    }
                    if (createdTextures.ContainsKey(filename))
                    {
                        material.SetTexture(property, createdTextures[filename]);
                    }
                    else
                    {
                        var newTexture = LoadTexture(filename);
                        if (newTexture)
                        {
                            if (material.HasProperty(property))
                            {
                                newTexture.name = material.GetTexture(property).name;
                            }
                            material.SetTexture(property, newTexture);
                            createdTextures[filename] = newTexture;
                        }
                    }
                }
            }
        }
    }
    public static void Reapply()
    {
        SearchDirectory(NewSkinDirectory);
        SkinManager.SetFullOutfit(SkinManager.HeadSkin);
    }

    public static void ApplyPalette(GameObject root, int skinId, int paletteId, bool body, bool head)
    {
        if (_overridePalette >= 0)
        {
            paletteId = _overridePalette;
        }
        if (paletteId <= 0)
        {
            return;
        }
        CleanupAll();
        if (AvailableSkinPalettes.TryGetValue((skinId, paletteId), out var skinReplacementParams))
        {
            Debug.Log($"Attempting to apply palette: {paletteId}");
            skinReplacementParams.Apply(root, body, head);
        }
    }

    public static void SearchDirectory(string directory)
    {
        foreach (var path in Directory.GetFiles(directory, "*"+SkinPaletteSuffix, SearchOption.AllDirectories))
        {
            var skinReplacementParams = JsonConvert.DeserializeObject<SkinReplacementParams>(File.ReadAllText(path));
            if (skinReplacementParams != null)
            {
                Debug.Log($"Created skin variant for {skinReplacementParams.skinName} {skinReplacementParams.skinId} : {skinReplacementParams.paletteId}");
                AvailableSkinPalettes[(skinReplacementParams.skinId, skinReplacementParams.paletteId)] = skinReplacementParams;
            }
        }
        foreach (var path in Directory.GetFiles(directory, "*_skin_*_*.png", SearchOption.AllDirectories))
        {
            AvailableTextures[Path.GetFileName(path)] = path;
        }
    }

    static IEnumerable<Renderer> GetRenderers(GameObject root)
    {
        return root.GetComponentsInChildren<MeshRenderer>(true)
            .Concat(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Cast<Renderer>())
            .Where(x => !x.forceRenderingOff && !ExcludeRendererNames.Contains(x.name));
    }

    static bool IsHead(Transform transform)
    {
        for (int i = 0; i < 10; i++)
        {
            if (transform == null)
            {
                return false;
            }
            if (transform.name == "Neck")
            {
                return true;
            }
            transform = transform.parent;
        }
        return false;
    }

    private static Texture2D LoadTexture(string fileName)
    {
        if (AvailableTextures.ContainsKey(fileName))
        {
            var path = AvailableTextures[fileName];
            if (File.Exists(path))
            {
                var data = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2);
                if (texture.LoadImage(data, false))
                {
                    return texture;
                }
                else
                {
                    Debug.LogError($"Something went wrong loading image file {fileName}");
                }
            }
        }
        return null;
    }
    
    public static void CreateNewSkin(int id, SkinCreationMode mode = SkinCreationMode.Simple)
    {
        if (!Player.localPlayer || !Player.localPlayer.character.refs.playerSkinSetter)
        {
            Debug.LogError("No PlayerSkinSetter to create skin from");
            return;
        }
        SkinManager.UpdateSkinFromFacts();
        if (SkinManager.BodySkin != SkinManager.HeadSkin)
        {
            Debug.LogError("Set the head and body skin to match before creating a new skin");
            return;
        }

        var skin = (int)SkinManager.BodySkin;
        if (AethaModelSwap.IsBaseSkin(skin))
        {
            CreateNewSkin(Player.localPlayer.character.refs.playerSkinSetter.gameObject, skin, id, mode);
        }
        else
        {
            CreateNewSkin(AethaModelSwap.LocalClone.gameObject, skin, id, mode);
        }
        SearchDirectory(NewSkinDirectory);
    }

    static void CreateNewSkin(GameObject root, int skinIndex, int paletteIndex, SkinCreationMode mode)
    {
        var fileSuffix = $"_skin_{skinIndex}_{paletteIndex}.png";
        var skinName = SkinDatabase.me.Skins.First(x => x.Skin == (SkinManager.Skin)skinIndex).name;
        var skinReplacementParams = new SkinReplacementParams
        {
            skinName = skinName,
            skinId = skinIndex,
            paletteId = paletteIndex,
        };
        HashSet<Texture2D> textures = new();
        var renderers = GetRenderers(root).ToList();
        foreach (Renderer r in renderers)
        {
            Debug.Log($"Found renderer: {r.name}");
            skinReplacementParams.rendererReplacementParamsList.Add(new RendererReplacementParams());
            skinReplacementParams.rendererReplacementParamsList.Last().rendererName = r.name;
            foreach (var m in r.sharedMaterials)
            {
                if (!m)
                {
                    continue;
                }
                var materialReplacementParams = new MaterialReplacementParams(m, mode);
                if (!materialReplacementParams.textures.Any() && mode == SkinCreationMode.Simple)
                {
                    continue;
                }
                skinReplacementParams.rendererReplacementParamsList.Last().materialParams.Add(materialReplacementParams);
                foreach (var name in m.GetTexturePropertyNames())
                {
                    if (m.HasProperty(name) && m.HasTexture(name) && m.GetTexture(name) is Texture2D texture && texture != null)
                    {
                        if (mode == SkinCreationMode.Simple && SimpleExcludeTextures.Contains(texture.name))
                        {
                            continue;
                        }
                        if (materialReplacementParams.textures.ContainsKey(name))
                        {
                            //Debug.Log($"{r.name}: {texture.name}");
                            textures.Add(texture);
                            var fileName = GetSimplifiedName(texture.name);
                            materialReplacementParams.textures[name] = fileName + fileSuffix;
                        }
                    }
                }
            }
            if (!skinReplacementParams.rendererReplacementParamsList.Last().materialParams.Any())
            {
                skinReplacementParams.rendererReplacementParamsList.RemoveAt(skinReplacementParams.rendererReplacementParamsList.Count-1);
            }
        }
        var directory = $"{NewSkinDirectory}/{skinName}_{skinIndex}_{paletteIndex}";
        
        Debug.Log($"Dumping {textures.Count} textures for skin {skinIndex} to {directory}");

        if (!Directory.Exists(NewSkinDirectory))
        {
            Directory.CreateDirectory(NewSkinDirectory);
            if (!Directory.Exists(NewSkinDirectory))
            {
                Debug.LogError($"Failed to create directory for texture dump at {NewSkinDirectory}");
                return;
            }
        }
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            if (!Directory.Exists(directory))
            {
                Debug.LogError($"Failed to create directory for new skin at {directory}");
                return;
            }
        }

        var configPath = $"{directory}/{skinName}_{paletteIndex}{SkinPaletteSuffix}";
        JsonSerializerSettings settings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new IgnoreVectorPropertiesContractResolver(),
        };
        File.WriteAllText(configPath, JsonConvert.SerializeObject(skinReplacementParams, Formatting.Indented, settings));

        foreach (var t in textures)
        {
            var copiedTexture = BlitCopy(t);
            var name = GetSimplifiedName(t.name);
            var filePath = $"{directory}/{name}{fileSuffix}";
            var bytes = copiedTexture.EncodeToPNG();
            File.WriteAllBytes(filePath, bytes);
        }
        
        string GetSimplifiedName(string textureName)
        {
            if (mode == SkinCreationMode.Simple)
            {
                if (AethaModelSwap.IsBaseSkin(skinIndex))
                {
                    foreach (var pair in TextureSimplifiedNamesBase)
                    {
                        if (pair.actual == textureName)
                        {
                            return pair.simplified;
                        }
                    }
                    Debug.LogWarning($"No simplified name for base skin texture: {textureName}");
                }
                if (AethaModelSwap.IsHubSkin(skinIndex))
                {
                    foreach (var pair in TextureSimplifiedNamesHub)
                    {
                        if (pair.actual == textureName)
                        {
                            return pair.simplified;
                        }
                    }
                    Debug.LogWarning($"No simplified name for hub skin texture: {textureName}");
                }
            }
            return textureName;
        }
    }

    class IgnoreVectorPropertiesContractResolver : DefaultContractResolver
    {

        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            IList<JsonProperty> properties = base.CreateProperties(type, memberSerialization);
            var propsToIgnore = typeof(Vector4).GetProperties().Select(p => p.Name).ToList();

            properties =
                properties.Where(p => !propsToIgnore.Contains( p.PropertyName) ).ToList();

            return properties;
        }
    }

    static Texture2D BlitCopy(Texture2D source)
    {
        var renderTex = RenderTexture.GetTemporary(
            source.width,
            source.height,
            0,
            RenderTextureFormat.Default,
            RenderTextureReadWrite.Default);
        Graphics.Blit(source, renderTex);
        var previous = RenderTexture.active;
        RenderTexture.active = renderTex;
        var readableText = new Texture2D(source.width, source.height);
        readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
        readableText.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTex);
        return readableText;
    }
}
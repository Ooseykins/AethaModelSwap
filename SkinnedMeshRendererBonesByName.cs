using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json;

namespace AethaModelSwapMod
{
    public class SkinnedMeshRendererBonesByName : MonoBehaviour
    {
    
        private static string AssemblyDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static string ZoeBonePath = AssemblyDirectory + "/ZoeBoneList.json";

        [Serializable]
        private class BoneNamesJson
        {
            public Dictionary<string, List<string>> dictionary = new();
        }

        private void Awake()
        {
            if (Application.isEditor)
            {
                SaveToFile(transform.root.gameObject, "E:/HasteMods/AethaModelSwap/Project/AethaModelSwapMod/Plugin/ZoeBoneList.json");
            }
        }

        public static void FixZoeBones(GameObject root)
        {
            LoadFromFile(root, ZoeBonePath);
        }

        private static void SaveToFile(GameObject root, string path)
        {
            var data = new BoneNamesJson();
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                data.dictionary[smr.name] = smr.bones.Select(x => x.name).ToList();
            }
            File.WriteAllText(path, JsonConvert.SerializeObject(data));
        }

        private static void LoadFromFile(GameObject root, string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"No bone names file exists at {path}");
                return;
            }
            var data = JsonConvert.DeserializeObject<BoneNamesJson>(File.ReadAllText(path));
            if (data == null)
            {
                Debug.LogWarning($"Something wrong with json data at {path}");
                return;
            }
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (data.dictionary.TryGetValue(smr.name, out var boneList))
                {
                    var bonesArr = new Transform[boneList.Count];
                    var allBones = root.GetComponentsInChildren<Transform>(includeInactive: true);
                    for(int i = 0; i < boneList.Count; i++)
                    {
                        var boneName = boneList[i];
                        var bone = allBones.FirstOrDefault(x => x.name == boneName);
                        if (bone)
                        {
                            bonesArr[i] = bone;
                        }
                    }
                    if (bonesArr[0] == null)
                    {
                        bonesArr[0] = root.transform;
                    }
                    if (bonesArr[1] == null)
                    {
                        bonesArr[1] = root.transform;
                    }
                    smr.bones = bonesArr;
                }
                
            }
        }
    }
}
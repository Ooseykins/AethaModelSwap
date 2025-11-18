using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Landfall.Haste;
using UnityEngine;
using UnityEngine.Localization;
using Object = UnityEngine.Object;

namespace AethaModelSwapMod;

public static class HubCharacters
{
    private class CharacterInfo
    {
        public string name = "Default Name";
        public string fileName = "Default";
        public LocalizedString localizedName = new UnlocalizedString("Default Name");
        public int skinIndex;
        public Func<HumanBodyBones, string> boneNameFunction;
        public Action<GameObject> adjustmentsFunction = null;
        public string[] animatedBoneRoots;
        public GameObject cachedObject; // calculated
        public AnimationParameters animationParameters = new(); // bone names are calculated
    }

    private const int BaseIndex = 16000120;
    private const string HubCharactersPrefabName = "Hub_Characters";
    private static GameObject _hubCharactersPrefab;
    private static readonly HashSet<CharacterInfo> _characterInfos = new();

    public static void RegisterAllSkins()
    {
        _characterInfos.Add(new CharacterInfo()
        {
            name = "Captain",
            fileName = "Captain",
            localizedName = new LocalizedString("TableReference","TableReferenceEntry"),
            skinIndex = BaseIndex + 1,
            boneNameFunction = GetCaptainBoneName,
            animatedBoneRoots = new []
            {
                "Head",
            },
            animationParameters = new AnimationParameters()
            {
                offsetPosition = new Vector3(0f, 0f, 0f),
                offsetRotation = Quaternion.Euler(0f,15f,0f),
            }
        });
        _characterInfos.Add(new CharacterInfo()
        {
            name = "Sage",
            fileName = "Daro",
            localizedName = new UnlocalizedString("Daro"),
            skinIndex = BaseIndex + 2,
            boneNameFunction = GetDaroBoneName,
            adjustmentsFunction = x => 
            {
                HasteClone.FindRecursive("Arm_L", x.transform).localScale = Vector3.zero;
                HasteClone.FindRecursive("Arm_R", x.transform).localScale = Vector3.zero;
            },
            animatedBoneRoots = new[]
            {
                "Head",
            },
            animationParameters = new AnimationParameters()
            {
                playAnimation = 1,
            },
        });
        _characterInfos.Add(new CharacterInfo()
        {
            name = "Heir",
            fileName = "Niada",
            localizedName = new UnlocalizedString("Niada"),
            skinIndex = BaseIndex + 3,
            boneNameFunction = GetCaptainBoneName, // shared bone names
            animatedBoneRoots = new[]
            {
                "Head",
            },
            animationParameters = new AnimationParameters()
            {
                disableIdle = true,
            },
        });
        _characterInfos.Add(new CharacterInfo()
        {
            name = "BL_Keeper_Riza",
            fileName = "Riza",
            localizedName = new UnlocalizedString("Riza"),
            skinIndex = BaseIndex + 4,
            boneNameFunction = GetRizaBoneName,
            animatedBoneRoots = new[]
            {
                "head",
                "wingConnector.L",
                "wingConnector.R",
                "shoeCrystals1.001.R",
                "shoeCrystals1.R",
                "shoeFloaty.002",
                "shoeFloaty.003",
                "shoeCrystals1.001.L",
                "shoeCrystals1.L",
                "shoeFloaty",
                "shoeFloaty.001",
            },
            animationParameters = new AnimationParameters()
            {
                offsetPosition = new Vector3(0f, 0f, 0.25f),
                offsetRotation = Quaternion.Euler(-25f,-25f,0f),
            }
        });
        _characterInfos.Add(new CharacterInfo()
        {
            name = "BL_Ava",
            fileName = "Ava",
            localizedName = new UnlocalizedString("Ava"),
            skinIndex = BaseIndex + 5,
            boneNameFunction = GetAvaBoneName,
            animatedBoneRoots = new[]
            {
                "Head",
                "ThingimajigBelt.001",
            },
        });
        _characterInfos.Add(new CharacterInfo()
        {
            name = "C_Researcher_Shop",
            fileName = "Gan",
            localizedName = new UnlocalizedString("Gan"),
            skinIndex = BaseIndex + 6,
            boneNameFunction = GetGanBoneName,
            animatedBoneRoots = new[]
            {
                "head",
            },
            animationParameters = new AnimationParameters()
            {
                offsetPosition = new Vector3(0f, 0f, 0.15f),
                offsetRotation = Quaternion.Euler(-25f,0f,0f),
            },
        });

        foreach (var c in _characterInfos)
        {
            var path = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\{c.fileName}.{c.skinIndex}.json";
            ModelIKParameters ModelIkParameters() => ModelIKParameters.LoadModelIKParameters(path, true);
            AethaModelSwap.RegisterSkin(c.skinIndex, c.fileName, null, ModelIkParameters, () => GetModelPrefab(c.name), GetBoneDictionary(c.boneNameFunction), () => c.animationParameters);
        }
        if (SkinDatabase.me)
        {
            AethaModelSwap.RegisterToSkinManager(SkinDatabase.me);
        }
    }

    static GameObject GetModelPrefab(string name)
    {
        var info = _characterInfos.FirstOrDefault(x => x.name == name);
        if (info == null)
        {
            Debug.LogError($"No model info to match name: {name}");
            return null;
        }
        if (info.cachedObject)
        {
            return info.cachedObject;
        }
        info.cachedObject = LoadHubCharacterFromResources(name);
        if (!info.cachedObject)
        {
            Debug.LogError($"Unable to load model: {name}");
            return null;
        }
        info.adjustmentsFunction?.Invoke(info.cachedObject);
        if (info.animatedBoneRoots != null && info.animationParameters != null)
        {
            info.animationParameters.animatedBoneNames = GetAnimatedBones(info.cachedObject.transform, info.animatedBoneRoots);
        }
        return info.cachedObject;
    }

    static GameObject LoadHubCharacterFromResources(string name)
    {
        if (!_hubCharactersPrefab)
        {
            _hubCharactersPrefab = Resources.Load<GameObject>(HubCharactersPrefabName);
            if (!_hubCharactersPrefab)
            {
                Debug.LogError($"No resources found with name: {HubCharactersPrefabName}");
                return null;
            }
        }

        var characterPrefab = _hubCharactersPrefab.GetComponentsInChildren<Animator>(true).FirstOrDefault(x => x.name == name);
        if (!characterPrefab)
        {
            Debug.LogError($"Could not find hub character with name: {name}");
            return null;
        }
        return LoadHubCharacter(characterPrefab.gameObject);
    }

    static GameObject LoadHubCharacter(GameObject rootObject)
    {
        rootObject.SetActive(false);
        var copy = Object.Instantiate(rootObject, Vector3.zero, Quaternion.identity, null);
        Object.DontDestroyOnLoad(copy);
        var allComponents = copy.GetComponentsInChildren<Component>(true);
        foreach (var c in allComponents)
        {
            if (c.name is "riza_ipad" or "riza_pen" or "spear (1)")
            {
                c.gameObject.SetActive(false);
            }
            if (c is Animator animator)
            {
                animator.enabled = false;
            }
            if (c is Transform or SkinnedMeshRenderer or MeshRenderer or MeshFilter or Animator)
            {
                continue;
            }
            Object.DestroyImmediate(c);
        }
        return copy;
    }

    private static HashSet<string> GetAnimatedBones(Transform root, params string[] names)
    {
        HashSet<string> output = new();
        foreach (var name in names)
        {
            var targetRoot = HasteClone.FindRecursive(name, root, true);
            if (targetRoot)
            {
                foreach (var t in targetRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (t == targetRoot && targetRoot.childCount > 1)
                    {
                        continue;
                    }
                    output.Add(t.name);
                }
            }
        }
        return output;
    }

    private static Dictionary<HumanBodyBones, string> GetBoneDictionary(Func<HumanBodyBones, string> getNameFunc)
    {
        Dictionary<HumanBodyBones, string> boneNames = new();
        foreach (var bone in Enum.GetValues(typeof(HumanBodyBones)).Cast<HumanBodyBones>())
        {
            var boneName = getNameFunc(bone);
            if (!string.IsNullOrEmpty(boneName))
            {
                boneNames[bone] = boneName;
            }
        }
        return boneNames;
    }
    
    private static string GetRizaBoneName(HumanBodyBones bone)
    {
        #region Bunch of Bones
        switch (bone)
        {
            case HumanBodyBones.Hips: return "hip";
            case HumanBodyBones.LeftUpperLeg: return "thigh.L";
            case HumanBodyBones.RightUpperLeg: return "thigh.R";
            case HumanBodyBones.LeftLowerLeg: return "shin.L";
            case HumanBodyBones.RightLowerLeg: return "shin.R";
            case HumanBodyBones.LeftFoot: return "foot.L";
            case HumanBodyBones.RightFoot: return "foot.R";
            case HumanBodyBones.Spine: return "spine";
            case HumanBodyBones.Chest: return"spine.001";
            case HumanBodyBones.UpperChest: return "spine.002";
            case HumanBodyBones.Neck: return "neck";
            case HumanBodyBones.Head: return "head";
            case HumanBodyBones.LeftShoulder: return "shoulder.L";
            case HumanBodyBones.RightShoulder: return "shoulder.R";
            case HumanBodyBones.LeftUpperArm: return "upper_arm.L";
            case HumanBodyBones.RightUpperArm: return "upper_arm.R";
            case HumanBodyBones.LeftLowerArm: return "forearm.L";
            case HumanBodyBones.RightLowerArm: return "forearm.R";
            case HumanBodyBones.LeftHand: return "hand.L";
            case HumanBodyBones.RightHand: return "hand.R";
            case HumanBodyBones.LeftToes: return "toe.L";
            case HumanBodyBones.RightToes: return "toe.R";
            case HumanBodyBones.LeftEye: return "eye.L";
            case HumanBodyBones.RightEye: return "eye.R";
            default:
                return "";
        }
        #endregion
    }
    
    private static string GetGanBoneName(HumanBodyBones bone)
    {
        #region Bunch of Bones
        switch (bone)
        {
            case HumanBodyBones.Hips: return "hip";
            case HumanBodyBones.LeftUpperLeg: return "rleg";
            case HumanBodyBones.RightUpperLeg: return "lleg";
            case HumanBodyBones.LeftLowerLeg: return "rknee";
            case HumanBodyBones.RightLowerLeg: return "lknee";
            case HumanBodyBones.LeftFoot: return "rfoot";
            case HumanBodyBones.RightFoot: return "lfoot";
            case HumanBodyBones.Spine: return "lowerSpine";
            case HumanBodyBones.Chest: return"middleSpine";
            case HumanBodyBones.UpperChest: return "upperSpine";
            case HumanBodyBones.Neck: return "neck";
            case HumanBodyBones.Head: return "head";
            case HumanBodyBones.LeftShoulder: return "lShoulder";
            case HumanBodyBones.RightShoulder: return "rShoulder";
            case HumanBodyBones.LeftUpperArm: return "rarm";
            case HumanBodyBones.RightUpperArm: return "larm";
            case HumanBodyBones.LeftLowerArm: return "relbow";
            case HumanBodyBones.RightLowerArm: return "lelbow";
            case HumanBodyBones.LeftHand: return "rhand";
            case HumanBodyBones.RightHand: return "lhand";
            case HumanBodyBones.LeftEye: return "eye";
            case HumanBodyBones.RightEye: return "eye_1";
            default:
                return "";
        }
        #endregion
    }
    
    private static string GetCaptainBoneName(HumanBodyBones bone)
    {
        #region Bunch of Bones
        switch (bone)
        {
            case HumanBodyBones.Hips: return "Hip";
            case HumanBodyBones.LeftUpperLeg: return "Leg_L";
            case HumanBodyBones.RightUpperLeg: return "Leg_R";
            case HumanBodyBones.LeftLowerLeg: return "Knee_L";
            case HumanBodyBones.RightLowerLeg: return "Knee_R";
            case HumanBodyBones.LeftFoot: return "Foot_L";
            case HumanBodyBones.RightFoot: return "Foot_R";
            case HumanBodyBones.Spine: return "Spine_1";
            case HumanBodyBones.Chest: return "Spine_2";
            case HumanBodyBones.UpperChest: return "Spine_3";
            case HumanBodyBones.Neck: return "Neck";
            case HumanBodyBones.Head: return "Head";
            case HumanBodyBones.LeftShoulder: return "Shoulder_L";
            case HumanBodyBones.RightShoulder: return "Shoulder_R";
            case HumanBodyBones.LeftUpperArm: return "Arm_L";
            case HumanBodyBones.RightUpperArm: return "Arm_R";
            case HumanBodyBones.LeftLowerArm: return "Elbow_L";
            case HumanBodyBones.RightLowerArm: return "Elbow_R";
            case HumanBodyBones.LeftHand: return "Hand_L";
            case HumanBodyBones.RightHand: return "Hand_R";
            case HumanBodyBones.LeftToes: return "Toe_L1";
            case HumanBodyBones.RightToes: return "Toe_R1";
            case HumanBodyBones.LeftEye: return "";
            case HumanBodyBones.RightEye: return "";
            default:
                return "";
        }
        #endregion
    }
    
    private static string GetGruntBoneName(HumanBodyBones bone)
    {
        #region Bunch of Bones
        switch (bone)
        {
            case HumanBodyBones.Hips: return "Hip";
            case HumanBodyBones.LeftUpperLeg: return "Leg_L";
            case HumanBodyBones.RightUpperLeg: return "Leg_R";
            case HumanBodyBones.LeftLowerLeg: return "Knee_L";
            case HumanBodyBones.RightLowerLeg: return "Knee_R";
            case HumanBodyBones.LeftFoot: return "Foot_l";
            case HumanBodyBones.RightFoot: return "Foot_1";
            case HumanBodyBones.Spine: return "Spine_1";
            case HumanBodyBones.Chest: return "Spine_2";
            case HumanBodyBones.UpperChest: return "Spine_3";
            case HumanBodyBones.Neck: return "Neck";
            case HumanBodyBones.Head: return "Head";
            case HumanBodyBones.LeftShoulder: return "Shoulder_L";
            case HumanBodyBones.RightShoulder: return "Shoulder_R";
            case HumanBodyBones.LeftUpperArm: return "Arm_L";
            case HumanBodyBones.RightUpperArm: return "Arm_R";
            case HumanBodyBones.LeftLowerArm: return "Elbow_L";
            case HumanBodyBones.RightLowerArm: return "Elbow_R";
            case HumanBodyBones.LeftHand: return "Hand_L";
            case HumanBodyBones.RightHand: return "Hand_R";
            default:
                return "";
        }
        #endregion
    }
    
    private static string GetAvaBoneName(HumanBodyBones bone)
    {
        #region Bunch of Bones
        switch (bone)
        {
            case HumanBodyBones.Hips: return "Hip";
            case HumanBodyBones.LeftUpperLeg: return "Leg.L";
            case HumanBodyBones.RightUpperLeg: return "Leg.R";
            case HumanBodyBones.LeftLowerLeg: return "Knee.L";
            case HumanBodyBones.RightLowerLeg: return "Knee.R";
            case HumanBodyBones.LeftFoot: return "Foot.L";
            case HumanBodyBones.RightFoot: return "Foot.R";
            case HumanBodyBones.Spine: return "spine1";
            case HumanBodyBones.Chest: return "spine2";
            case HumanBodyBones.UpperChest: return "spine 3";
            case HumanBodyBones.Neck: return "Neck";
            case HumanBodyBones.Head: return "Head";
            case HumanBodyBones.LeftShoulder: return "Shoulder.L";
            case HumanBodyBones.RightShoulder: return "Shoulder.R";
            case HumanBodyBones.LeftUpperArm: return "Arm.L";
            case HumanBodyBones.RightUpperArm: return "Arm.R";
            case HumanBodyBones.LeftLowerArm: return "Elbow.L";
            case HumanBodyBones.RightLowerArm: return "Elbow.R";
            case HumanBodyBones.LeftHand: return "Hand.L";
            case HumanBodyBones.RightHand: return "Hand.R";
            case HumanBodyBones.LeftToes: return "Foot.001.L";
            case HumanBodyBones.RightToes: return "Foot.001.R";
            case HumanBodyBones.LeftEye: return "Eye_Root.L";
            case HumanBodyBones.RightEye: return "Eye_Root.R";
            default:
                return "";
        }
        #endregion
    }
    
    private static string GetDaroBoneName(HumanBodyBones bone)
    {
        #region Bunch of Bones
        switch (bone)
        {
            case HumanBodyBones.Hips: return "Hip";
            case HumanBodyBones.LeftUpperLeg: return "Leg_L";
            case HumanBodyBones.RightUpperLeg: return "Leg_R";
            case HumanBodyBones.LeftLowerLeg: return "Knee_L";
            case HumanBodyBones.RightLowerLeg: return "Knee_R";
            case HumanBodyBones.LeftFoot: return "Foot_L";
            case HumanBodyBones.RightFoot: return "Foot_R";
            case HumanBodyBones.Spine: return "Spine_1";
            case HumanBodyBones.Chest: return "Spine_2";
            case HumanBodyBones.UpperChest: return "Spine_3";
            case HumanBodyBones.Neck: return "Neck_1";
            case HumanBodyBones.Head: return "Head";
            case HumanBodyBones.LeftShoulder: return "Shoulder_L";
            case HumanBodyBones.RightShoulder: return "Shoulder_R";
            case HumanBodyBones.LeftUpperArm: return "ArmSleve_L";
            case HumanBodyBones.RightUpperArm: return "ArmSleve_R";
            case HumanBodyBones.LeftLowerArm: return "Bone.001_R.022";
            case HumanBodyBones.RightLowerArm: return "Bone.001_L.022";
            case HumanBodyBones.LeftHand: return "Bone.001_R.023";
            case HumanBodyBones.RightHand: return "Bone.001_L.023";
            case HumanBodyBones.LeftToes: return "Toe_L1";
            case HumanBodyBones.RightToes: return "Toe_R1";
            case HumanBodyBones.LeftEye: return "";
            case HumanBodyBones.RightEye: return "";
            default:
                return "";
        }
        #endregion
    }
}
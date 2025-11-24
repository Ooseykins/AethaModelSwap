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
        public Action<GameObject> adjustmentsFunction;
        public string[] animatedBoneRoots;
        public GameObject cachedObject; // calculated
        public AnimationParameters animationParameters = new(); // bone names are calculated
    }

    private const int BaseIndex = 16000120;
    private const string HubCharactersPrefabName = "Hub_Characters";
    private static GameObject _hubCharactersPrefab;
    private static readonly HashSet<CharacterInfo> CharacterInfos = new();

    public static void RegisterAllSkins()
    {
        
        /* All the loc guids
        Leader|a0fc5dc1-b8e5-1564-9a83-581fdfd9b045|22074061722697728
        Wraith|a0fc5dc1-b8e5-1564-9a83-581fdfd9b045|22073954092662784
        Sage|a0fc5dc1-b8e5-1564-9a83-581fdfd9b045|22073643676418048
        Captain|a0fc5dc1-b8e5-1564-9a83-581fdfd9b045|22073701830443008
        Keeper|a0fc5dc1-b8e5-1564-9a83-581fdfd9b045|22074025861398528
        Grunt|a0fc5dc1-b8e5-1564-9a83-581fdfd9b045|22074176738902016
        Researcher|a0fc5dc1-b8e5-1564-9a83-581fdfd9b045|22073786869956608
        AnimalFashion|44065c2a-82b7-be04-c881-aec850e8c32a|2801620542504960
        Heir|a0fc5dc1-b8e5-1564-9a83-581fdfd9b045|22073612047171584
         */
        CharacterInfos.Add(new CharacterInfo()
        {
            name = "Captain",
            fileName = "Captain",
            localizedName = new LocalizedString(new Guid("a0fc5dc1-b8e5-1564-9a83-581fdfd9b045"),22073701830443008),
            skinIndex = BaseIndex + 1,
            boneNameFunction = GetCaptainBoneName,
            adjustmentsFunction = x =>
            {
                var capeChains = Bone("Cape.001").gameObject.AddComponent<BasicBoneChain>();
                capeChains.gravity = new Vector3(0f, -9.81f, 0f);
                capeChains.maxAngle = 25f;
                capeChains.maxSpeed = 12f;
                capeChains.AddConstraintPlane(Bone("Spine_2"), new Vector3(0f, 0.15f, -1f).normalized, new Vector3(0f, 0.3f, -0.3f));
                capeChains.AddChainFromRoot(Bone("Cape.005"));
                capeChains.AddChainFromRoot(Bone("Cape.011"));
                Transform Bone(string name) => HasteClone.FindRecursive(name, x.transform, true);
            },
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
        CharacterInfos.Add(new CharacterInfo()
        {
            name = "Sage",
            fileName = "Daro",
            localizedName = new LocalizedString(new Guid("a0fc5dc1-b8e5-1564-9a83-581fdfd9b045"),22073643676418048),
            skinIndex = BaseIndex + 2,
            boneNameFunction = GetDaroBoneName,
            adjustmentsFunction = x => 
            {
                // She has fully modeled arms under her sleeves so I scale them down to zero to hide them, TF2 style
                HasteClone.FindRecursive("Arm_L", x.transform).localScale = Vector3.zero;
                HasteClone.FindRecursive("Arm_R", x.transform).localScale = Vector3.zero;
                
                // This sucks.
                var skirtChains = x.gameObject.AddComponent<BasicBoneChain>();
                skirtChains.gravity = new Vector3(0f, -9.81f, 0f);
                skirtChains.maxAngle = 25f;
                skirtChains.maxSpeed = 12f;
                skirtChains.AddChainFromEnd(Bone("Bone.016_end"), 4);
                skirtChains.AddChainFromEnd(Bone("Bone.005_L.008_end"),4);
                skirtChains.AddChainFromEnd(Bone("Bone.005_R.010_end"),4);
                Transform Bone(string name) => HasteClone.FindRecursive(name, x.transform, true);
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
        CharacterInfos.Add(new CharacterInfo()
        {
            name = "Heir",
            fileName = "Niada",
            localizedName = new LocalizedString(new Guid("a0fc5dc1-b8e5-1564-9a83-581fdfd9b045"),22073612047171584),
            skinIndex = BaseIndex + 3,
            boneNameFunction = GetCaptainBoneName, // shared bone names
            adjustmentsFunction = x =>
            {
                // Front braids
                var hairChains = x.AddComponent<BasicBoneChain>();
                hairChains.gravity = new Vector3(0f, -9.81f, 0f);
                hairChains.maxAngle = 25f;
                hairChains.maxSpeed = 12f;
                hairChains.AddConstraintPlane(Bone("Spine_3"), new Vector3(0f, 0.3f, 1f).normalized, new Vector3(0f, 0.07f, 0.12f));
                hairChains.AddChainFromRoot(Bone("Hip.031_R.009"));
                hairChains.AddChainFromRoot(Bone("Hip.031_L.009"));
                
                // Blue cape
                var capeChains = Bone("Cape.001").gameObject.AddComponent<BasicBoneChain>();
                capeChains.gravity = new Vector3(0f, -9.81f, 0f);
                capeChains.maxAngle = 25f;
                capeChains.maxSpeed = 12f;
                capeChains.AddConstraintPlane(Bone("Spine_3"), new Vector3(0f, 0.1f, -1f).normalized, new Vector3(0f, 0f, -0.16f));
                capeChains.AddChainFromRoot(Bone("Cape.003"));
                Transform Bone(string name) => HasteClone.FindRecursive(name, x.transform, true);
            },
            animatedBoneRoots = new[]
            {
                "Head",
            },
            animationParameters = new AnimationParameters()
            {
                disableIdle = true,
            },
        });
        CharacterInfos.Add(new CharacterInfo()
        {
            name = "BL_Keeper_Riza",
            fileName = "Riza",
            localizedName = new LocalizedString(new Guid("a0fc5dc1-b8e5-1564-9a83-581fdfd9b045"),22074025861398528),
            skinIndex = BaseIndex + 4,
            boneNameFunction = GetRizaBoneName,
            adjustmentsFunction = x =>
            {
                // Welcome the cHaOs ZoNe
                var leftLeg = Bone("thigh.L");
                var rightLeg = Bone("thigh.R");

                Vector3 frontNormals = new Vector3(0, -0.1f, -1f).normalized;
                Vector3 backNormals = new Vector3(0, -0.1f, 1f).normalized;
                Vector3 leftNormals = new Vector3(-1f, -0.2f, 0f).normalized;
                Vector3 rightNormals = new Vector3(1f, -0.5f, 0f).normalized;

                var frontCenter = AddSkirtFlap("skirt1.001", leftLeg, frontNormals, new Vector3(0,0,-0.1f)); // Front center
                frontCenter.AddConstraintPlane(rightLeg, frontNormals, new Vector3(0,0,-0.1f));
                AddSkirtFlap("skirt2.001.L", leftLeg, frontNormals - Vector3.right * 0.1f, new Vector3(0,0,-0.15f)).maxAngle = 90f; // Front left
                AddSkirtFlap("skirt2.001.R", rightLeg, frontNormals + Vector3.right * 0.1f, new Vector3(0,0,-0.15f)).maxAngle = 90f; // Front right
                
                var backCenter = AddSkirtFlap("skirt5.001", leftLeg, backNormals, new Vector3(0,0,0.075f)); // Back center
                backCenter.AddConstraintPlane(rightLeg, backNormals, new Vector3(0,0,0.075f));
                AddSkirtFlap("skirt4.001.L", leftLeg, backNormals - Vector3.right * 0.2f, new Vector3(0,0,0)); // Back left
                AddSkirtFlap("skirt4.001.R", rightLeg, backNormals + Vector3.right * 0.2f, new Vector3(0,0,0)); // Back right
                
                AddSkirtFlap("skirt3.001.L", leftLeg, leftNormals, new Vector3(-0.15f,0,0)); // Left side
                var sideFlap = AddSkirtFlap("skirtFlapMaster.001", leftLeg, rightNormals, new Vector3(0.15f,0,0), false); // Big green right side
                sideFlap.damping *= 0.5f;
                sideFlap.gravity *= 1.25f;
                sideFlap.planeForce = 3f;

                BasicBoneChain AddSkirtFlap(string rootName, Transform planeTransform, Vector3 planeNormal, Vector3 planeOffset, bool single = true)
                {
                    var flap = Bone(rootName).gameObject.AddComponent<BasicBoneChain>();
                    flap.gravity = new Vector3(0f, -9.81f, 0f);
                    flap.maxAngle = 25f;
                    flap.maxSpeed = 30f;
                    flap.planeForce = 1.5f;
                    if (single)
                    {
                        flap.AddSingleLink(flap.transform);
                    }
                    else
                    {
                        flap.AddChainFromRoot(flap.transform);
                    }
                    if (planeTransform)
                    {
                        flap.AddConstraintPlane(planeTransform, planeNormal, planeOffset);
                    }
                    flap.Init();
                    return flap;
                }
                Transform Bone(string name) => HasteClone.FindRecursive(name, x.transform, true);
            },
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
        CharacterInfos.Add(new CharacterInfo()
        {
            name = "BL_Ava",
            fileName = "Ava",
            localizedName = new LocalizedString(new Guid("a0fc5dc1-b8e5-1564-9a83-581fdfd9b045"),22074061722697728),
            skinIndex = BaseIndex + 5,
            boneNameFunction = GetAvaBoneName,
            animatedBoneRoots = new[]
            {
                "Head",
                "ThingimajigBelt.001",
                "ava_hourglass",
                "evilcoin_wobble",
                "evilcoin_wobble_1",
            },
            animationParameters = new AnimationParameters()
            {
                offsetPosition = new Vector3(0f, 0f, 0f),
                offsetRotation = Quaternion.Euler(-3f,0f,0f),
            },
        });
        CharacterInfos.Add(new CharacterInfo()
        {
            name = "C_Researcher_Shop",
            fileName = "Gan",
            localizedName = new LocalizedString(new Guid("a0fc5dc1-b8e5-1564-9a83-581fdfd9b045"),22073786869956608),
            skinIndex = BaseIndex + 6,
            boneNameFunction = GetGanBoneName,
            adjustmentsFunction = x =>
            {
                var dalil = HasteClone.FindRecursive("Fish", x.transform, true).gameObject.AddComponent<CompanionFollower>();
                dalil.gameObject.SetActive(true);
                dalil.followSpeed = 17f;
                dalil.followOffset = new Vector3(1.5f, 2.5f, 0.7f);
                dalil.driftVertical = 0.2f;
                dalil.driftVerticalSpeed = 0.65432f;
                dalil.driftHorizontal = 0.25f;
                dalil.driftHorizontalSpeed = 0.8376f;
                dalil.rotation = Quaternion.Euler(-90f,0f,0f);
            },
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
        CharacterInfos.Add(new CharacterInfo()
        {
            name = "C_Wraith",
            fileName = "Wraith",
            localizedName = new LocalizedString(new Guid("a0fc5dc1-b8e5-1564-9a83-581fdfd9b045"),22073954092662784),
            skinIndex = BaseIndex + 7,
            boneNameFunction = GetWraithBoneName,
            adjustmentsFunction = x =>
            {
                var butterfly = Bone("Butterfly").gameObject.AddComponent<CompanionFollower>();
                butterfly.followSpeed = 1f;
                butterfly.followOffset = new Vector3(0.6f, 2.5f, -1.5f);
                butterfly.driftVertical = 0.3f;
                butterfly.driftVerticalSpeed = 0.21321f;
                butterfly.driftHorizontal = 0.3f;
                butterfly.driftHorizontalSpeed = 0.507f;
                butterfly.rotation = Quaternion.Euler(-90f,25f,0f);

                var scythe = Bone("WraithArmRoot_2").gameObject.AddComponent<CompanionFollower>();
                scythe.followSpeed = 8f;
                scythe.followOffset = new Vector3(-0.6f, 2.3f, -2.2f);
                scythe.driftVertical = 0.2f;
                scythe.driftVerticalSpeed = 0.336f;
                scythe.driftHorizontal = 0.1f;
                scythe.driftHorizontalSpeed = 0.813f;
                scythe.rotation = Quaternion.Euler(-35f,0f,0f);

                Transform Bone(string name) => HasteClone.FindRecursive(name, x.transform, true);
            },
            animatedBoneRoots = new[]
            {
                "Head",
                "Mesh",
                "ButteflyCenter",
                "WraithArmRoot_0", // Lower arms
                "WraithArmRoot_2", // Scythe arms
            },
            animationParameters = new AnimationParameters()
            {
                offsetPosition = new Vector3(0f, 0f, 0.15f),
                offsetRotation = Quaternion.Euler(-25f,0f,0f),
            },
        });

        foreach (var c in CharacterInfos)
        {
            var path = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\{c.fileName}.{c.skinIndex}.json";
            ModelIKParameters ModelIkParameters() => ModelIKParameters.LoadModelIKParameters(path, true);
            var spritePath = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\{c.fileName}.{c.skinIndex}.png";
            AethaModelSwap.RegisterSkin(c.skinIndex, c.fileName, AethaModelSwap.LoadSprite(spritePath), ModelIkParameters, () => GetModelPrefab(c.name), GetBoneDictionary(c.boneNameFunction), () => c.animationParameters, c.localizedName);
        }
        if (SkinDatabase.me)
        {
            AethaModelSwap.RegisterToSkinManager(SkinDatabase.me);
        }
    }

    static GameObject GetModelPrefab(string name)
    {
        var info = CharacterInfos.FirstOrDefault(x => x.name == name);
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
        info.cachedObject.SetActive(true);
        if (info.animatedBoneRoots != null && info.animationParameters != null)
        {
            info.animationParameters.animatedBoneNames = GetAnimatedBones(info.cachedObject.transform, info.animatedBoneRoots);
        }
        info.adjustmentsFunction?.Invoke(info.cachedObject);
        info.cachedObject.SetActive(false);
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
        //HasteClone.LogTransformHierarchy(_hubCharactersPrefab.transform);

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
        CleanupCharacter(copy);
        return copy;
    }

    static void CleanupCharacter(GameObject character)
    {
        var allComponents = character.GetComponentsInChildren<Component>(true);
        foreach (var c in allComponents)
        {
            if (c.name is "riza_ipad" or "riza_pen" or "spear (1)")
            {
                if (c is Renderer renderer)
                {
                    renderer.enabled = false;
                }
                c.gameObject.SetActive(false);
            }
            if (c is Transform or SkinnedMeshRenderer or MeshRenderer or MeshFilter or Animator)
            {
                continue;
            }
            //Debug.Log($"Destroyed extra component: {c.name} of type {c.GetType()}");
            Object.DestroyImmediate(c);
        }
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
    
    private static string GetWraithBoneName(HumanBodyBones bone)
    {
        #region Bunch of Bones
        switch (bone)
        {
            case HumanBodyBones.Hips: return "Spine_2";
            case HumanBodyBones.LeftUpperLeg: return "";
            case HumanBodyBones.RightUpperLeg: return "";
            case HumanBodyBones.LeftLowerLeg: return "";
            case HumanBodyBones.RightLowerLeg: return "";
            case HumanBodyBones.LeftFoot: return "";
            case HumanBodyBones.RightFoot: return "";
            case HumanBodyBones.Spine: return "Spine_3";
            case HumanBodyBones.Chest: return"Spine_4";
            case HumanBodyBones.UpperChest: return "Spine_5";
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
            case HumanBodyBones.LeftEye: return "";
            case HumanBodyBones.RightEye: return "";
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
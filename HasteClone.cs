using System;
using System.Linq;
using System.Collections.Generic;
using Landfall.Haste;
using UnityEngine;

namespace AethaModelSwapMod;

// This class copies bone transforms from the Zoe model onto a humanoid model
// It also handles IK to correct non-matching poses to fix things like foot positioning
public class HasteClone : MonoBehaviour
{
    // Zoe model's measurements
    private const float ZoePrefabScale = 0.02f;
    private const float ZoeFootIKHeightOffset = -0.25f;
    private static float _zoeStanceWidth;
    private static float _zoeHipHeight;
    private static float _zoeArmLength;
    private static float _zoeTorsoLength;
    private static float _zoeArmAngle;

    // This model's measurements
    private float _measureStanceWidth;
    private float _measureHipHeight;
    private float _measureArmLength;
    private float _measureTorsoLength;
    private float _measureArmAngle;

    // Scaling value to fix differences between the skin preview and instantiated
    private float _instanceScale;

    // For translating the base Zoe model's bone rotations to the imported model
    private readonly List<(Transform source, Transform dest)> _correctiveBones = new();
    
    // For preventing bones drifting away due to some inaccuracies
    private readonly Dictionary<Transform, Vector3> _initialPositions = new();

    // For positioning the instantiated model
    private Transform _sourceRoot;
    private Transform _destRoot;
    private Transform _sourceHips;
    private Transform _destHips;

    // Instantiated transforms to use as ik targets
    private IKInstance _ikFootLeft;
    private IKInstance _ikFootRight;
    private IKInstance _ikHandLeft;
    private IKInstance _ikHandRight;

    // Animator of the instantiated clone, used for getting bone references
    private Animator _destAnimator;

    public ModelIKParameters modelIKParameters = new ();
    public int SkinIndex { get; private set; }
    
    public void Setup(Transform sourceRoot, Transform destRoot, int index)
    {
        Debug.Log($"Setting up a HasteClone copying {sourceRoot} to {destRoot}");
        SkinIndex = index;

        _sourceRoot = sourceRoot;
        _sourceHips = GetSourceBoneTransform(HumanBodyBones.Hips, _sourceRoot);

        // Clone should be scaled at 1 while posing and calculating measurements
        _destRoot = destRoot;
        destRoot.rotation = Quaternion.identity;
        _destRoot.localScale = Vector3.one;
        _destAnimator = _destRoot.GetComponentInChildren<Animator>();
        _destAnimator.enabled = false;
        _destHips = _destAnimator.GetBoneTransform(HumanBodyBones.Hips);
        _instanceScale = _sourceHips.lossyScale.x / ZoePrefabScale;
        
        SetMaterials();

        // Measure Zoe and the imported avatar to create scaling values for posing
        MeasureZoe();
        MeasureAvatar();

        // Disable renderers of the default skin
        foreach (var renderer in _sourceRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            renderer.enabled = false;
        }
        foreach (var renderer in _sourceRoot.GetComponentsInChildren<MeshRenderer>())
        {
            renderer.enabled = false;
        }

        AddCorrectiveBones();

        _ikFootLeft = AddLimbIK(
            GetSourceBoneTransform(HumanBodyBones.LeftToes, sourceRoot), 
            _destAnimator.GetBoneTransform(HumanBodyBones.LeftFoot), 
            _sourceHips, 
            _destHips);
        _ikFootRight = AddLimbIK(
            GetSourceBoneTransform(HumanBodyBones.RightToes, sourceRoot), 
            _destAnimator.GetBoneTransform(HumanBodyBones.RightFoot), 
            _sourceHips, 
            _destHips);
        _ikHandLeft = AddLimbIK(
            GetSourceBoneTransform(HumanBodyBones.LeftHand, sourceRoot), 
            _destAnimator.GetBoneTransform(HumanBodyBones.LeftHand), 
            GetSourceBoneTransform(HumanBodyBones.LeftUpperArm, sourceRoot), 
            _destAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm));
        _ikHandRight = AddLimbIK(
            GetSourceBoneTransform(HumanBodyBones.RightHand, sourceRoot), 
            _destAnimator.GetBoneTransform(HumanBodyBones.RightHand), 
            GetSourceBoneTransform(HumanBodyBones.RightUpperArm, sourceRoot), 
            _destAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm));
        
        // Attach the clone to the parent, so it works in SkinPreview3d
        _destRoot.parent = _sourceRoot.root;
    }

    // Allow replacing standard shader with the tone-dot character shader
    // useShared prevents subsequent prefab loads from having to replace materials
    // it can be disabled so that the editor toggle will allow you to turn them off
    public void SetMaterials(bool useShared = true)
    {
        if (!modelIKParameters.replaceStandardShader)
        {
            return;
        }
        var baseMat = SkinDatabase.me.GetSkin(0).BodyPrefab.GetComponentInChildren<SkinnedMeshRenderer>().material;

        foreach(var r in _destRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            foreach (var mat in useShared ? r.sharedMaterials : r.materials)
            {
                if (mat.shader.name != "Standard")
                {
                    continue;
                }

                var mainTexture = mat.mainTexture;
                var color = mat.color;

                // Special exception for materials with a colour but no main texture
                if (!mainTexture && color != Color.white)
                {
                    var newTex = new Texture2D(2, 2);
                    newTex.SetPixels(new [] {color, color, color, color});
                    newTex.Apply();
                    mainTexture = newTex;
                }
                var normalMap = mat.GetTexture("_BumpMap");
                var emissionMap = mat.GetTexture("_EmissionMap");
                var metalMap = mat.GetTexture("_MetallicGlossMap");

                mat.shader = baseMat.shader;
                mat.CopyPropertiesFromMaterial(baseMat);
                mat.mainTexture = mainTexture;
                mat.SetTexture("_Normal", normalMap ? normalMap : Texture2D.normalTexture);
                mat.SetTexture("_Emission", emissionMap ? emissionMap : Texture2D.blackTexture);
                mat.SetTexture("_Metal", metalMap ? metalMap : Texture2D.blackTexture);
            }
        }
    }

    void AddCorrectiveBones()
    {
        // Zoe prefab in A-Pose to use as reference
        var zoePrefab = SkinDatabase.me.GetSkin(0);
        var prefabBodyTransform = zoePrefab.BodyPrefab.transform;
        var prefabHeadTransform = zoePrefab.HeadPrefab.transform;

        // Fix the prefab rotations so things line up right
        var prefabBodyRotation = prefabBodyTransform.rotation;
        var prefabHeadRotation = prefabHeadTransform.rotation;
        prefabBodyTransform.rotation = Quaternion.Euler(0f, 0f, 0f);
        prefabHeadTransform.rotation = Quaternion.Euler(21f, 0f, 0f);
        
        // Fix the imported model to match Zoe's A-Pose
        _destAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm).Rotate(Vector3.forward, (_measureArmAngle-_zoeArmAngle), Space.World);
        _destAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm).Rotate(Vector3.forward, -(_measureArmAngle-_zoeArmAngle), Space.World);

        _sourceHips.rotation = Quaternion.identity;
        
        foreach (var bone in Enum.GetValues(typeof(HumanBodyBones)).Cast<HumanBodyBones>())
        {
            var prefabBone = GetSourceBoneTransform(bone, prefabBodyTransform);
            if (!prefabBone)
            {
                prefabBone = GetSourceBoneTransform(bone, prefabHeadTransform);
                // No bone on the body prefab? Check if it's on the head
                if (!prefabBone)
                {
                    Debug.Log($"Prefab has no bone: {bone}");
                    continue;
                }
            }
            var sourceBone = GetSourceBoneTransform(bone, _sourceRoot);
            if (!sourceBone)
            {
                Debug.Log($"Source has no bone: {bone}");
                continue;
            }
            var destBone = _destAnimator.GetBoneTransform(bone);
            if (!destBone)
            {
                // This case is OK, some bones are optional in Unity, it may make the pose a little bit off though
                Debug.Log($"Destination has no bone: {bone}");
                continue;
            }

            _initialPositions[destBone] = destBone.localPosition;

            if (sourceBone != _sourceHips)
            {
                sourceBone.localRotation = prefabBone.localRotation;
            }

            var correctiveBone = new GameObject($"CorrectiveBone: {bone}").transform;
            correctiveBone.position = sourceBone.position;
            correctiveBone.rotation = destBone.rotation;
            correctiveBone.parent = sourceBone;
            _correctiveBones.Add((correctiveBone, destBone));
        }

        // Return the prefab rotations to original, in case that could break something
        prefabBodyTransform.rotation = prefabBodyRotation;
        prefabHeadTransform.rotation = prefabHeadRotation;
    }
    
    IKInstance AddLimbIK(Transform sourceBone, Transform destBone, Transform sourceAnchor, Transform destAnchor)
    {
        var ikTarget = new GameObject($"IkTarget: {sourceBone}").transform;
        ikTarget.position = destBone.position;
        ikTarget.rotation = destBone.rotation;
        ikTarget.parent = destBone.root;
        var ikHint = new GameObject($"IkHint: {sourceBone}").transform;
        ikHint.parent = destBone.root;
        var ik = new SimpleLimbIK(destBone, ikTarget, ikHint);
        var destNormalized = new GameObject($"Normalized: {sourceBone}").transform;
        destNormalized.parent = ikTarget;
        destNormalized.localPosition = Vector3.zero;
        destNormalized.rotation = Quaternion.identity;
        return new IKInstance()
        {
            sourceBone = sourceBone,
            destBone = destBone,
            ikTarget = ikTarget,
            ikHint = ikHint,
            destAnchor = destAnchor,
            sourceAnchor = sourceAnchor,
            destNormalized = destNormalized,
            simpleLimbIK = ik,
        };
    }
    
    class IKInstance
    {
        public Transform sourceBone;
        public Transform destBone;
        public Transform ikTarget;
        public Transform ikHint;
        public Transform sourceAnchor;
        public Transform destAnchor;
        public Transform destNormalized;
        public SimpleLimbIK simpleLimbIK;
    }

    private void LateUpdate()
    {
        if (!_sourceRoot || !_destRoot || !_destHips || !_sourceHips)
        {
            Debug.Log("HasteClone destroyed due to missing bones");
            Destroy(gameObject);
            return;
        }
        if (!_destAnimator || !_destAnimator.avatar || !_destAnimator.isHuman)
        {
            Debug.Log("HasteClone destroyed due to broken animator");
            Destroy(gameObject);
            return;
        }

        _destRoot.localScale = Vector3.one;
        _destRoot.rotation = _sourceHips.rotation;
        _destRoot.position = _sourceHips.position;
        
        // Prevent bones from drifting away due to small errors by setting them back to their initial positions
        foreach (var kvp in _initialPositions)
        {
            if (!kvp.Key)
            {
                Debug.Log("HasteClone destroyed due to missing dest bone");
                Destroy(gameObject);
                return;
            }
            kvp.Key.localPosition = kvp.Value;
        }
        
        // Match hip position of the two models, then offset the clone upwards
        var up = _sourceHips.up;
        var offsetAmount = (_measureHipHeight * modelIKParameters.scale) + modelIKParameters.verticalOffset;
        _destHips.position = _sourceHips.position - (up * _zoeHipHeight) + (up * offsetAmount);
        
        RotateSourceArms();
        SetBoneRotations();
        RotateDestHands();
        SetIKHandles();
        
        // Tilt the clone's head up or down
        _destAnimator.GetBoneTransform(HumanBodyBones.Head).Rotate(_sourceHips.right, modelIKParameters.headAngleOffset, Space.World);

        _destRoot.localScale = Vector3.one * (modelIKParameters.scale * _instanceScale);
    }

    // Copy bone rotations from the source to the destination
    private void SetBoneRotations()
    {
        _destRoot.position = _sourceHips.position;
        _destRoot.rotation = _sourceHips.rotation;
        
        foreach (var entry in _correctiveBones)
        {
            if (!entry.source || !entry.dest)
            {
                Debug.Log("HasteClone destroyed due to missing pair");
                Destroy(gameObject);
                return;
            }
            entry.dest.rotation = entry.source.rotation;
        }
    }

    void RotateSourceArms()
    {
        // Rotate arms before hand IK
        RotateArm(_ikHandLeft,-1f);
        RotateArm(_ikHandRight,1f);
        
        void RotateArm(IKInstance instance, float sign)
        {
            var upperArm = instance.sourceBone.parent.parent;
            var hand = instance.sourceBone;
            var dirToHand = (upperArm.position - hand.position).normalized;
            var dot = 1f - Mathf.Clamp01(Vector3.Dot((_sourceHips.right * -sign).normalized, dirToHand));
            upperArm.Rotate(_sourceHips.forward, modelIKParameters.armAngleOffset * dot * sign, Space.World);
        }
    }

    void RotateDestHands()
    {
        // Rotate hands before hand IK
        _ikHandLeft.destBone.Rotate(_ikHandLeft.destNormalized.forward, modelIKParameters.handAngleOffset, Space.World);
        _ikHandRight.destBone.Rotate(_ikHandRight.destNormalized.forward, -modelIKParameters.handAngleOffset, Space.World);
    }
    
    void SetIKHandles()
    {
        // Prepare scaling values for the foot IK
        var footScale = new Vector3( _measureStanceWidth / _zoeStanceWidth, _measureHipHeight / _zoeHipHeight, (1f / modelIKParameters.scale) * modelIKParameters.strideLength);
        var manualFootScale = new Vector3(modelIKParameters.stanceWidth, modelIKParameters.stanceHeight, 1);
        var offsetPosition = new Vector3(0f, ZoeFootIKHeightOffset, modelIKParameters.footFrontBackOffset);
        MoveIkHandle(_ikFootLeft, footScale, manualFootScale, offsetPosition);
        MoveIkHandle(_ikFootRight, footScale, manualFootScale, offsetPosition);

        // Rotate foot IK targets for knees
        _ikFootLeft.ikTarget.Rotate(_ikFootLeft.destNormalized.up, modelIKParameters.kneesOut, Space.World);
        _ikFootRight.ikTarget.Rotate(_ikFootRight.destNormalized.up, -modelIKParameters.kneesOut, Space.World);
        
        // Set IK hint forward for knees
        MoveIKHint(_ikFootLeft, new Vector3(-modelIKParameters.kneesOut * _measureHipHeight / 2f, -_measureHipHeight / 2f, _measureHipHeight * 3f));
        MoveIKHint(_ikFootRight, new Vector3(modelIKParameters.kneesOut * _measureHipHeight / 2f, -_measureHipHeight / 2f, _measureHipHeight * 3f));

        // Rotate foot IK targets again for "pigeon toe"
        _ikFootLeft.ikTarget.Rotate(_ikFootLeft.destNormalized.up, modelIKParameters.footAngle, Space.World);
        _ikFootRight.ikTarget.Rotate(_ikFootRight.destNormalized.up, -modelIKParameters.footAngle, Space.World);

        // Resolve all IK
        _ikFootLeft.simpleLimbIK.ResolveIK();
        _ikFootRight.simpleLimbIK.ResolveIK();

        void MoveIkHandle(IKInstance instance, Vector3 destScale, Vector3 paramScale, Vector3 absolutePositioning)
        {
            instance.ikTarget.parent = null;
            instance.ikTarget.position = instance.sourceAnchor.position;
            instance.ikTarget.rotation = _sourceHips.rotation;
            instance.ikTarget.localScale = Vector3.one * _instanceScale;
            var local = instance.ikTarget.InverseTransformPoint(instance.sourceBone.position) + absolutePositioning;

            if (local.y > absolutePositioning.y)
            {
                local = new Vector3(local.x, absolutePositioning.y, local.z);
            }

            var x = local.x * destScale.x * paramScale.x;
            var y = local.y * destScale.y * paramScale.y;
            var z = local.z * destScale.z * paramScale.z;
            local = new Vector3(x, y, z);
            instance.ikTarget.localScale = Vector3.one;
            instance.ikTarget.position = instance.destAnchor.position;
            instance.ikTarget.position = instance.ikTarget.TransformPoint(local);
            instance.ikTarget.rotation = instance.destBone.rotation;
            instance.ikTarget.parent = _destRoot;
        }

        void MoveIKHint(IKInstance instance, Vector3 offset)
        {
            instance.ikHint.position = _destHips.TransformPoint(offset);
        }
    }
    
    private void MeasureAvatar()
    {
        // Imported avatars should be in an A-Pose or T-Pose when instantiated
        var leftFoot = _destAnimator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
        var rightFoot = _destAnimator.GetBoneTransform(HumanBodyBones.RightFoot).position;
        var footCenter = (leftFoot + rightFoot) / 2f;
        var hipsTransform = _destAnimator.GetBoneTransform(HumanBodyBones.Hips);
        var hips = hipsTransform.position;
        var leftHand = _destAnimator.GetBoneTransform(HumanBodyBones.LeftHand).position;
        var leftUpperArm = _destAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
        var rightUpperArm = _destAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
        var shoulderCenter = (leftUpperArm + rightUpperArm) / 2f;
        
        // Distance between feet
        _measureStanceWidth = Vector3.Distance(leftFoot, rightFoot);
        // Distance from floor to hips
        _measureHipHeight = Vector3.Distance(hips, footCenter);
        // Length of arm while outstretched
        _measureArmLength = Vector3.Distance(leftHand, leftUpperArm);
        // Distance from hips to arms
        _measureTorsoLength = Vector3.Distance(hips, shoulderCenter);

        // Arm angle, for correcting A-Pose or T-Pose differences in imported models
        var dirToHips = (leftUpperArm - hips).normalized;
        var dirToHand = (leftUpperArm - leftHand).normalized;
        _measureArmAngle = Vector3.Angle(dirToHips, dirToHand);
        
        Debug.Log($"Automatic {gameObject.name} measurements: Stance:{_measureStanceWidth}, Height:{_measureHipHeight}, ArmLength:{_measureArmLength}, TorsoLength:{_measureTorsoLength}, ArmAngle:{_measureArmAngle}");
    }
    
    private static void MeasureZoe()
    {
        // Only ever need to measure Zoe once
        if (_zoeStanceWidth != 0)
        {
            return;
        }
        // Get the default skin prefab from the SkinDatabase
        // It is in an A-Pose to appropriate for taking measurements from
        var prefabRootTransform = SkinDatabase.me.GetSkin(0).BodyPrefab.transform;
        
        var leftFoot = GetSourceBoneTransform(HumanBodyBones.LeftToes, prefabRootTransform).position;
        var rightFoot = GetSourceBoneTransform(HumanBodyBones.RightToes, prefabRootTransform).position;
        var footCenter = (leftFoot + rightFoot) / 2f;
        var hipsTransform = GetSourceBoneTransform(HumanBodyBones.Hips, prefabRootTransform);
        var hips = hipsTransform.position;
        var leftHand = GetSourceBoneTransform(HumanBodyBones.LeftHand, prefabRootTransform).position;
        var leftUpperArm = GetSourceBoneTransform(HumanBodyBones.LeftUpperArm, prefabRootTransform).position;
        var rightUpperArm = GetSourceBoneTransform(HumanBodyBones.RightUpperArm, prefabRootTransform).position;
        var shoulderCenter = (leftUpperArm + rightUpperArm) / 2f;
        
        // Distance between feet
        _zoeStanceWidth = Vector3.Distance(leftFoot, rightFoot) * ZoePrefabScale;
        // Distance from floor to hips
        _zoeHipHeight = Vector3.Distance(hips, footCenter) * ZoePrefabScale;
        // Length of arm while outstretched
        _zoeArmLength = Vector3.Distance(leftHand, leftUpperArm) * ZoePrefabScale;
        // Distance from hips to arms
        _zoeTorsoLength = Vector3.Distance(hips, shoulderCenter) * ZoePrefabScale;

        // Arm angle, for correcting A-Pose or T-Pose differences in imported models
        var dirToHips = (shoulderCenter - hips).normalized;
        var dirToHand = (shoulderCenter - leftHand).normalized;
        _zoeArmAngle = Vector3.Angle(dirToHips, dirToHand);

        Debug.Log($"Automatic Zoe measurements: Stance:{_zoeStanceWidth}, Height:{_zoeHipHeight}, ArmLength:{_zoeArmLength}, TorsoLength:{_zoeTorsoLength}, ArmAngle:{_zoeArmAngle}");
    }
    
    // Recursively find the bone on the Zoe model, either from the prefab or the active player
    private static Transform GetSourceBoneTransform(HumanBodyBones bone, Transform root)
    {
        var sourceName = GetSourceBoneName(bone);
        if (string.IsNullOrEmpty(sourceName))
        {
            return null;
        }
        return FindRecursive(sourceName, root);
    }

    private static Transform FindRecursive(string search, Transform t)
    {
        if (t.name.Contains(search))
        {
            return t;
        }
        var initialFind = t.Find(search);
        if (initialFind)
        {
            return initialFind;
        }
        for (int i = 0; i < t.childCount; i++)
        {
            var find = FindRecursive(search, t.GetChild(i));
            if (find)
            {
                return find;
            }
        }
        return null;
    }

    // Translate HumanBodyBones of a humanoid animator to Zoe's bone transform name
    private static string GetSourceBoneName(HumanBodyBones bone)
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
            case HumanBodyBones.Chest: return"Spine_2";
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
            case HumanBodyBones.LeftEye: return "Eye_L";
            case HumanBodyBones.RightEye: return "Eye_R";
            /*
            case HumanBodyBones.Jaw:
            case HumanBodyBones.LeftThumbProximal: return "Hip.003_R.007";
            case HumanBodyBones.LeftThumbIntermediate: return "Hip.003_R.008";
            case HumanBodyBones.LeftThumbDistal: return "Hip.003_R.009";
            case HumanBodyBones.LeftIndexProximal: return "Hip.003_R.010";
            case HumanBodyBones.LeftIndexIntermediate: return "Hip.003_R.020";
            case HumanBodyBones.LeftIndexDistal: return "Hip.003_R.021";
            case HumanBodyBones.LeftMiddleProximal: return "Hip.003_R.011";
            case HumanBodyBones.LeftMiddleIntermediate: return "Hip.003_R.018";
            case HumanBodyBones.LeftMiddleDistal: return "Hip.003_R.019";
            case HumanBodyBones.LeftRingProximal: return "Hip.003_R.012";
            case HumanBodyBones.LeftRingIntermediate: return "Hip.003_R.016";
            case HumanBodyBones.LeftRingDistal: return "Hip.003_R.017";
            case HumanBodyBones.LeftLittleProximal: return "Hip.003_R.013";
            case HumanBodyBones.LeftLittleIntermediate: return "Hip.003_R.014";
            case HumanBodyBones.LeftLittleDistal: return "Hip.003_R.015";
            case HumanBodyBones.RightThumbProximal: return "Hip.003_L.007";
            case HumanBodyBones.RightThumbIntermediate: return "Hip.003_L.008";
            case HumanBodyBones.RightThumbDistal: return "Hip.003_L.009";
            case HumanBodyBones.RightIndexProximal: return "Hip.003_L.010";
            case HumanBodyBones.RightIndexIntermediate: return "Hip.003_L.020";
            case HumanBodyBones.RightIndexDistal: return "Hip.003_L.021";
            case HumanBodyBones.RightMiddleProximal: return "Hip.003_L.011";
            case HumanBodyBones.RightMiddleIntermediate: return "Hip.003_L.018";
            case HumanBodyBones.RightMiddleDistal: return "Hip.003_L.019";
            case HumanBodyBones.RightRingProximal: return "Hip.003_L.012";
            case HumanBodyBones.RightRingIntermediate: return "Hip.003_L.016";
            case HumanBodyBones.RightRingDistal: return "Hip.003_L.017";
            case HumanBodyBones.RightLittleProximal: return "Hip.003_L.013";
            case HumanBodyBones.RightLittleIntermediate: return "Hip.003_L.014";
            case HumanBodyBones.RightLittleDistal: return "Hip.003_L.015";
            case HumanBodyBones.LastBone:
            */
            default:
                return "";
        }
        #endregion
    }

    // Show the base model renderers if the clone is destroyed somehow out of order
    private void OnDestroy()
    {
        if (_sourceRoot)
        {
            foreach (var r in _sourceRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                r.enabled = true;
            }
            foreach (var r in _sourceRoot.GetComponentsInChildren<MeshRenderer>())
            {
                r.enabled = true;
            }
        }
        
        // Quickly null out the LocalClone so the editor can be updated to show the default model
        if (AethaModelSwap.LocalClone == this)
        {
            AethaModelSwap.LocalClone = null;
            ModelParamsEditor.ResetFields();
        }
    }

    // Helper function for just logging a transform hierarchy
    // Zoe's skeleton is very weird so this can help figure out which bones are connected where
    private static void LogTransformHierarchy(Transform t, string gap = "")
    {
        Debug.Log(gap+t.name);
        for (int j = 0; j < t.name.Length; j++)
        {
            gap += " ";
        }
        for (int i = 0; i < t.childCount; i++)
        {
            LogTransformHierarchy(t.GetChild(i), gap);
        }
    }
}
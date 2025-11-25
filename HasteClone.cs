using System;
using System.Linq;
using System.Collections.Generic;
using Landfall.Haste;
using UnityEngine;

namespace AethaModelSwapMod;

// This class copies bone transforms from the Zoe model onto a humanoid model
// It also handles IK to correct non-matching poses to fix things like foot positioning
[DefaultExecutionOrder(-1)]
public class HasteClone : MonoBehaviour
{
    // Zoe model's measurements
    private const float ZoePrefabScale = 0.02f;
    private const float ZoeFootIKHeightOffset = -0.3f;
    private static float _zoeStanceWidth;
    private static float _zoeHipHeight;
    private static float _zoeArmLength;
    private static float _zoeTorsoLength;
    private static float _zoeArmAngle;

    // This model's measurements
    private float _measureStanceWidth = -1f;
    private float _measureHipHeight = -1f;
    private float _measureArmAngle = -1f;

    // Scaling value to fix differences between the skin preview and instantiated
    private float _instanceScale;

    // For translating the base Zoe model's bone rotations to the imported model
    private Transform _correctiveHips;
    private List<(Transform source, Transform dest)> _correctiveBones = new();
    private readonly Dictionary<HumanBodyBones, Transform> _destBones = new();
    
    // Lookup for non-humanoid animators
    private Dictionary<HumanBodyBones, string> _boneNameTable;

    // For preventing bones drifting away due to some inaccuracies and reset to base pose for animation
    private readonly Dictionary<Transform, (Vector3 localPosition, Quaternion localRotation)> _initialTransforms = new();
    private readonly HashSet<Transform> _animatedBones = new();
    private readonly Dictionary<Transform, (Vector3 localPosition, Quaternion localRotation)> _animatedTransforms = new();
    private float _smoothedIdleWeight;
    private const float SmoothIdleTime = 0.35f;
    private AnimationParameters _animationParameters;

    // For positioning the instantiated model
    private Transform _sourceRoot;
    private Transform _destRoot;
    private Transform _sourceHips;
    private Transform _destHips;
    private Transform _sourceHead; // For multiplayer crown

    // Instantiated transforms to use as ik targets
    private IKInstance _ikFootLeft;
    private IKInstance _ikFootRight;
    private IKInstance _ikHandLeft;
    private IKInstance _ikHandRight;
    
    // Store eye references so we can interpolate their rotations
    private Transform _destEyeLeft;
    private Transform _destEyeRight;
    private Quaternion _destEyeStartRotLeft;
    private Quaternion _destEyeStartRotRight;

    // Animator of the instantiated clone, used for getting bone references
    private Animator _destAnimator;
    private Animator _sourceAnimator;

    public ModelIKParameters modelIKParameters = new ();

    public int SkinIndex { get; private set; }
    
    public void Setup(Transform sourceRoot, Transform destRoot, int index, Dictionary<HumanBodyBones, string> boneNames = null, AnimationParameters animationParameters = null)
    {
        Debug.Log($"Setting up a HasteClone copying {sourceRoot} to {destRoot}");
        SkinIndex = index;

        _boneNameTable = boneNames;

        _sourceRoot = sourceRoot;
        _sourceHips = GetSourceBoneTransform(HumanBodyBones.Hips, _sourceRoot);
        _sourceAnimator = _sourceRoot.root.GetComponentInChildren<Animator>();
        _animationParameters = animationParameters;

        // Clone should be scaled at 1 while posing and calculating measurements
        _destRoot = destRoot;
        destRoot.rotation = Quaternion.identity;
        _destRoot.localScale = Vector3.one;
        _destAnimator = _destRoot.GetComponentInChildren<Animator>();
        _destHips = GetDestBoneTransform(HumanBodyBones.Hips);
        if (!_destHips)
        {
            _destHips = _destRoot;
            _correctiveHips = _destHips;
        }
        _instanceScale = _sourceHips.lossyScale.x / ZoePrefabScale;
        
        // Player character, in case we need refs later
        
        
        // Replace materials with Haste's to make things look a bit nicer
        SetMaterials();
        
        // Store a reference to Zoe's head, for moving the multiplayer crown
        _sourceHead = GetSourceBoneTransform(HumanBodyBones.Head, _sourceRoot);

        // Measure Zoe and the imported avatar to create scaling values for posing
        MeasureZoe();
        MeasureAvatar();

        AddCorrectiveBones();

        if (animationParameters != null)
        {
            foreach (var t in _destRoot.GetComponentsInChildren<Transform>(true))
            {
                if (_destBones.Values.Contains(t))
                {
                    continue;
                }
                if (animationParameters.animatedBoneNames.Contains(t.name))
                {
                    _animatedBones.Add(t);
                }
            }
        }

        if (GetDestBoneTransform(HumanBodyBones.LeftFoot))
        {
            _ikFootLeft = AddLimbIK(
                GetSourceBoneTransform(HumanBodyBones.LeftToes, sourceRoot), 
                GetDestBoneTransform(HumanBodyBones.LeftFoot), 
                _sourceHips, 
                _destHips);
        }
        if (GetDestBoneTransform(HumanBodyBones.RightFoot))
        {
            _ikFootRight = AddLimbIK(
                GetSourceBoneTransform(HumanBodyBones.RightToes, sourceRoot), 
                GetDestBoneTransform(HumanBodyBones.RightFoot), 
                _sourceHips, 
                _destHips);
        }
        if (GetDestBoneTransform(HumanBodyBones.LeftHand) && GetDestBoneTransform(HumanBodyBones.LeftUpperArm))
        {
            _ikHandLeft = AddLimbIK(
                GetSourceBoneTransform(HumanBodyBones.LeftHand, sourceRoot), 
                GetDestBoneTransform(HumanBodyBones.LeftHand), 
                GetSourceBoneTransform(HumanBodyBones.LeftUpperArm, sourceRoot), 
                GetDestBoneTransform(HumanBodyBones.LeftUpperArm));
        }
        if (GetDestBoneTransform(HumanBodyBones.RightHand) && GetDestBoneTransform(HumanBodyBones.RightUpperArm))
        {
            _ikHandRight = AddLimbIK(
                GetSourceBoneTransform(HumanBodyBones.RightHand, sourceRoot),
                GetDestBoneTransform(HumanBodyBones.RightHand),
                GetSourceBoneTransform(HumanBodyBones.RightUpperArm, sourceRoot),
                GetDestBoneTransform(HumanBodyBones.RightUpperArm));
        }

        // Attach the clone to the parent, so it works in SkinPreview3d
        _destRoot.parent = _sourceRoot.root;
        destRoot.gameObject.SetActive(true);
        
        // Set animator params
        if (_destAnimator)
        {
            _destAnimator.enabled = _animatedBones is { Count: > 0 };
            _destAnimator.applyRootMotion = false;
            _destAnimator.updateMode = AnimatorUpdateMode.Normal;
            _destAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (_animationParameters is { playAnimation: >= 0 })
            {
                _destAnimator.SetInteger("Act", _animationParameters.playAnimation);
            }
        }
        
        // Log the components on the clone, for debugging
        /*
        foreach (var comp in _destRoot.GetComponentsInChildren<Component>())
        {
            if (comp is Transform)
            {
                continue;
            }
            Debug.Log($"Clone component: {comp.name} of type {comp.GetType()}");
        }
        */

        // Use the hand points of the destination instead of the source, if possible
        var playerCharacter = _sourceRoot.GetComponentInParent<PlayerCharacter>();
        if (playerCharacter)
        {
            if (_destBones.TryGetValue(HumanBodyBones.LeftHand, out var leftHand))
            {
                var newHandPoint = leftHand.gameObject.AddComponent<HandPoint>();
                newHandPoint.isRight = false;
            }

            if (_destBones.TryGetValue(HumanBodyBones.RightHand, out var rightHand))
            {
                var newHandPoint = rightHand.gameObject.AddComponent<HandPoint>();
                newHandPoint.isRight = true;
            }
        }

        // Disable renderers of the default skin, do this last in case something else goes wrong
        foreach (var renderer in _sourceRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            renderer.enabled = false;
        }
        foreach (var renderer in _sourceRoot.GetComponentsInChildren<MeshRenderer>())
        {
            renderer.enabled = false;
        }
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
        if (_measureArmAngle >= 0f)
        {
            GetDestBoneTransform(HumanBodyBones.LeftUpperArm).Rotate(Vector3.forward, (_measureArmAngle-_zoeArmAngle), Space.World);
            GetDestBoneTransform(HumanBodyBones.RightUpperArm).Rotate(Vector3.forward, -(_measureArmAngle-_zoeArmAngle), Space.World);
        }

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
                    //Debug.Log($"Prefab has no bone: {bone}");
                    continue;
                }
            }
            var sourceBone = GetSourceBoneTransform(bone, _sourceRoot);
            if (!sourceBone)
            {
                Debug.Log($"Source has no bone: {bone}");
                continue;
            }
            var destBone = GetDestBoneTransform(bone);
            if (!destBone)
            {
                // This case is OK, some bones are optional in Unity, it may make the pose a little bit off though
                //Debug.Log($"Destination has no bone: {bone}");
                continue;
            }
            
            _destBones.Add(bone, destBone);

            switch (bone)
            {
                case HumanBodyBones.LeftEye:
                    _destEyeLeft = destBone;
                    _destEyeStartRotLeft = destBone.localRotation;
                    break;
                case HumanBodyBones.RightEye:
                    _destEyeRight = destBone;
                    _destEyeStartRotRight = destBone.localRotation;
                    break;
            }

            if (sourceBone != _sourceHips)
            {
                sourceBone.localRotation = prefabBone.localRotation;
            }

            var correctiveBone = new GameObject($"CorrectiveBone: {bone}").transform;
            correctiveBone.position = sourceBone.position;
            correctiveBone.rotation = destBone.rotation;
            correctiveBone.parent = sourceBone;
            _correctiveBones.Add((correctiveBone, destBone));

            if (bone == HumanBodyBones.Hips)
            {
                _correctiveHips = correctiveBone;
            }
        }

        foreach (var bone in _destRoot.GetComponentsInChildren<Transform>())
        {
            if (bone != _destRoot && bone != _destHips)
            {
                _initialTransforms[bone] = (bone.localPosition, bone.localRotation);
            }
        }

        // Ensure the bones are in the correct hierarchy order so they don't influence each other wrongly
        _correctiveBones = _correctiveBones.OrderBy(x => GetHierarchyDepth(x.dest)).ToList();

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
        var destAnchorNormalized = new GameObject($"Normalized: {destAnchor}").transform;
        destAnchorNormalized.parent = destAnchor;
        destAnchorNormalized.localPosition = Vector3.zero;
        destAnchorNormalized.rotation = Quaternion.identity;
        return new IKInstance()
        {
            sourceBone = sourceBone,
            destBone = destBone,
            ikTarget = ikTarget,
            ikHint = ikHint,
            destAnchor = destAnchor,
            sourceAnchor = sourceAnchor,
            destNormalized = destNormalized,
            destAnchorNormalized = destAnchorNormalized,
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
        public Transform destAnchorNormalized;
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
        if (!_sourceAnimator)
        {
            Debug.Log("HasteClone destroyed due to missing animator");
            Destroy(gameObject);
            return;
        }

        _destRoot.localScale = Vector3.one;
        _destRoot.rotation = _sourceHips.rotation;
        _destRoot.position = _sourceHips.position;

        // If we have an enabled animator, do some idle animation blending
        var idleAnimationWeight = 0f;
        if (_destAnimator && _destAnimator.enabled && !(_animationParameters is { disableIdle: true }))
        {
            // Get the actual idle animation weight, it is a sum of multiple clips possibly
            idleAnimationWeight = _sourceAnimator.GetCurrentAnimatorClipInfo(0)
                .Where(x => x.clip.name.Contains("Idle"))
                .Sum(x => x.weight);

            // If we're idle, smoothly blend into the idle animation over time
            if (idleAnimationWeight > 0.5f)
            {
                _smoothedIdleWeight += Time.deltaTime / SmoothIdleTime;
            }
            else
            {
                _smoothedIdleWeight -= Time.deltaTime / SmoothIdleTime;
            }
            _smoothedIdleWeight = Mathf.Clamp01(_smoothedIdleWeight);
            idleAnimationWeight = Mathf.SmoothStep(0f, 1f, _smoothedIdleWeight);

            // If we're all the way into the idle animation we don't need to do any more interpolation etc
            if (idleAnimationWeight >= 1f)
            {
                SetHipPosition();
                _destHips.rotation = _correctiveHips.rotation;
                if (_animationParameters != null)
                {
                    _destHips.localPosition += _animationParameters.offsetPosition;
                    _destHips.localRotation *= _animationParameters.offsetRotation;
                }
                _destRoot.localScale = Vector3.one * (modelIKParameters.scale * _instanceScale);
                return;
            }
        
            // If we're idle, store the current (animated) transformations to blend into later
            if (_destAnimator && _destAnimator.enabled && idleAnimationWeight > 0)
            {
                foreach (var bone in _initialTransforms.Keys)
                {
                    _animatedTransforms[bone] = (bone.localPosition, bone.localRotation);
                }
            }
        }

        ForceBasePose();
        RotateSourceArms();
        SetBoneRotations();
        SetHipPosition();
        RotateDestHands();
        SetIKHandles();
        SetHeadCrownPosition();

        // Bend the clone's spine
        var spineBones = new List<Transform>();
        if (_destBones.TryGetValue(HumanBodyBones.Spine, out var spine))
        {
            spineBones.Add(spine);
        }
        if (_destBones.TryGetValue(HumanBodyBones.Chest, out var chest))
        {
            spineBones.Add(chest);
        }
        if (_destBones.TryGetValue(HumanBodyBones.UpperChest, out var upperChest))
        {
            spineBones.Add(upperChest);
        }
        foreach (var t in spineBones)
        {
            t.Rotate(_sourceHips.right, modelIKParameters.spineAngleOffset / spineBones.Count, Space.World);
        }
        
        // Tilt the clone's head up or down
        if(_destBones.TryGetValue(HumanBodyBones.Head, out var head))
        {
            head.Rotate(_sourceHips.right, modelIKParameters.headAngleOffset, Space.World);
        }
        
        _destHips.rotation = _correctiveHips.rotation;
        _destRoot.localScale = Vector3.one * (modelIKParameters.scale * _instanceScale);

        // If the player is idle, pose them back into their idle animation
        if (_destAnimator && _destAnimator.enabled && idleAnimationWeight > 0)
        {
            foreach (var kvp in _animatedTransforms)
            {
                kvp.Key.localPosition = Vector3.Lerp(kvp.Key.localPosition, kvp.Value.localPosition, idleAnimationWeight);
                kvp.Key.localRotation = Quaternion.Slerp(kvp.Key.localRotation, kvp.Value.localRotation, idleAnimationWeight);
            }
            if (_animationParameters != null)
            {
                _destHips.localPosition += _animationParameters.offsetPosition * idleAnimationWeight;
                _destHips.localRotation *= Quaternion.Slerp(Quaternion.identity, _animationParameters.offsetRotation, idleAnimationWeight);
            }
        }
    }

    private void ForceBasePose()
    {
        foreach (var kvp in _initialTransforms)
        {
            if (!kvp.Key)
            {
                Debug.Log("HasteClone destroyed due to missing dest bone");
                Destroy(gameObject);
                return;
            }
            if (_animatedBones.Contains(kvp.Key))
            {
                continue;
            }
            kvp.Key.localPosition = kvp.Value.localPosition;
            kvp.Key.localRotation = kvp.Value.localRotation;
        }
    }

    private void SetHipPosition()
    {
        var up = _sourceHips.up;
        if (_measureHipHeight >= 0)
        {
            // Match hip position of the two models, then offset the clone upward
            var offsetAmount = (_measureHipHeight * modelIKParameters.scale) + modelIKParameters.verticalOffset;
            _destHips.position = _sourceHips.position - (up * _zoeHipHeight) + (up * offsetAmount);
        }
        else
        {
            var offsetAmount = modelIKParameters.verticalOffset;
            _destHips.position = _sourceHips.position + (up * offsetAmount);
        }
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
            // Allow eyes to interpolate their rotations, since Zoe's eyes move a lot
            if (entry.dest == _destEyeLeft)
            {
                entry.dest.localRotation = _destEyeStartRotLeft;
                entry.dest.rotation = Quaternion.Slerp(entry.dest.rotation, entry.source.rotation, modelIKParameters.eyeMovement);
                continue;
            }
            if (entry.dest == _destEyeRight)
            {
                entry.dest.localRotation = _destEyeStartRotRight;
                entry.dest.rotation = Quaternion.Slerp(entry.dest.rotation, entry.source.rotation, modelIKParameters.eyeMovement);
                continue;
            }
            entry.dest.rotation = entry.source.rotation;
        }
    }

    void RotateSourceArms()
    {
        if (_ikHandLeft != null)
        {
            RotateArm(_ikHandLeft,-1f);
        }

        if (_ikHandRight != null)
        {
            RotateArm(_ikHandRight, 1f);
        }
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
        _ikHandLeft?.destBone.Rotate(_ikHandLeft.destNormalized.forward, modelIKParameters.handAngleOffset, Space.World);
        _ikHandRight?.destBone.Rotate(_ikHandRight.destNormalized.forward, -modelIKParameters.handAngleOffset, Space.World);
    }

    void SetHeadCrownPosition()
    {
        if (_destBones.TryGetValue(HumanBodyBones.Head, out var head) && head && _sourceHead)
        {
            _sourceHead.position = head.position;
        }
    }

    void SetIKHandles()
    {
        // If we are missing measurements it means we're missing some IK bones too
        if (_measureHipHeight < 0f || _measureStanceWidth < 0f)
        {
            return;
        }
        // Prepare scaling values for the foot IK
        var footScale = new Vector3( _measureStanceWidth / _zoeStanceWidth, _measureHipHeight / _zoeHipHeight, (1f / modelIKParameters.scale) * modelIKParameters.strideLength);
        var manualFootScale = new Vector3(modelIKParameters.stanceWidth, modelIKParameters.stanceHeight, 1);
        var offsetPosition = new Vector3(0f, ZoeFootIKHeightOffset, modelIKParameters.footFrontBackOffset);
        if (_ikFootLeft != null)
        {
            // Set IK handle position
            MoveIkHandle(_ikFootLeft, footScale, manualFootScale, offsetPosition);
            // Rotate foot IK targets for knees
            _ikFootLeft.ikTarget.Rotate(_ikFootLeft.destNormalized.up, modelIKParameters.kneesOut, Space.World);
            // Set IK hint forward for knees
            MoveIKHint(_ikFootLeft, new Vector3(-modelIKParameters.kneesOut * _measureHipHeight / 2f, -_measureHipHeight / 2f, _measureHipHeight * 3f));
            // Rotate foot IK targets again for "pigeon toe"
            _ikFootLeft.ikTarget.Rotate(_ikFootLeft.destNormalized.up, modelIKParameters.footAngle, Space.World);
            _ikFootLeft.simpleLimbIK.ResolveIK();
        }
        if (_ikFootRight != null)
        {
            MoveIkHandle(_ikFootRight, footScale, manualFootScale, offsetPosition);
            _ikFootRight.ikTarget.Rotate(_ikFootRight.destNormalized.up, -modelIKParameters.kneesOut, Space.World);
            MoveIKHint(_ikFootRight, new Vector3(modelIKParameters.kneesOut * _measureHipHeight / 2f, -_measureHipHeight / 2f, _measureHipHeight * 3f));
            _ikFootRight.ikTarget.Rotate(_ikFootRight.destNormalized.up, -modelIKParameters.footAngle, Space.World);
            _ikFootRight.simpleLimbIK.ResolveIK();
        }

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
            instance.ikHint.position = instance.destAnchorNormalized.TransformPoint(offset);
        }
    }
    
    private void MeasureAvatar()
    {
        // Imported avatars should be in an A-Pose or T-Pose when instantiated
        var leftFoot = GetDestBoneTransform(HumanBodyBones.LeftFoot);
        var rightFoot = GetDestBoneTransform(HumanBodyBones.RightFoot);
        var hips = GetDestBoneTransform(HumanBodyBones.Hips);
        var leftHand = GetDestBoneTransform(HumanBodyBones.LeftHand);
        var leftUpperArm = GetDestBoneTransform(HumanBodyBones.LeftUpperArm);
        if (leftFoot && rightFoot)
        {
            // Distance between feet
            _measureStanceWidth = Vector3.Distance(leftFoot.position, rightFoot.position);
            var footCenter = (leftFoot.position + rightFoot.position) / 2f;
            // Distance from floor to hips
            _measureHipHeight = Vector3.Distance(hips.position, footCenter);
        }
        if (leftUpperArm && leftHand)
        {
            // Arm angle, for correcting A-Pose or T-Pose differences in imported models
            var dirToHips = (leftUpperArm.position - hips.position).normalized;
            var dirToHand = (leftUpperArm.position - leftHand.position).normalized;
            _measureArmAngle = Vector3.Angle(dirToHips, dirToHand);
        }
        Debug.Log($"Automatic {gameObject.name} measurements: Stance:{_measureStanceWidth}, Height:{_measureHipHeight}, ArmAngle:{_measureArmAngle}");
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
    
    // Get the bone of the imported model
    private Transform GetDestBoneTransform(HumanBodyBones bone, bool strict = true)
    {
        if (_destAnimator && _destAnimator.avatar && _destAnimator.isHuman)
        {
            return _destAnimator.GetBoneTransform(bone);
        }
        if (_boneNameTable == null)
        {
            return null;
        }
        if (_boneNameTable.TryGetValue(bone, out var boneName) && !string.IsNullOrEmpty(boneName))
        {
            return FindRecursive(boneName, _destRoot, strict);
        }
        return null;
    }
    
    // Recursively find the bone on the Zoe model, either from the prefab or the active player
    private static Transform GetSourceBoneTransform(HumanBodyBones bone, Transform root, bool strict = false)
    {
        var sourceName = GetSourceBoneName(bone);
        if (string.IsNullOrEmpty(sourceName))
        {
            return null;
        }
        return FindRecursive(sourceName, root, strict);
    }

    public static Transform FindRecursive(string search, Transform t, bool strict = false)
    {
        if(t.name == search)
        {
            return t;
        }
        if (t.name.Contains(search) && !strict)
        {
            if (t.name != search)
            {
                Debug.Log($"FindRecursive: [{search}] found as [{t.name}]");
            }
            return t;
        }
        var initialFind = t.Find(search);
        if (initialFind)
        {
            return initialFind;
        }
        for (int i = 0; i < t.childCount; i++)
        {
            var find = FindRecursive(search, t.GetChild(i), strict);
            if (find)
            {
                return find;
            }
        }
        return null;
    }

    static int GetHierarchyDepth(Transform t)
    {
        int depth = 0;
        var currentTransform = t;
        while (currentTransform.parent)
        {
            depth++;
            currentTransform = currentTransform.parent;
        }
        return depth;
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
            case HumanBodyBones.LeftEye: return "Eye_l";
            case HumanBodyBones.RightEye: return "Eye_r";
            case HumanBodyBones.Jaw: return "";
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
            default:
                return "";
        }
        #endregion
    }
    
    private void OnDestroy()
    {
        // Quickly null out the LocalClone so the editor can be updated to show the default model
        if (AethaModelSwap.LocalClone == this)
        {
            AethaModelSwap.LocalClone = null;
            ModelParamsEditor.ResetFields();
        }
    }

    // Helper function for just logging a transform hierarchy
    // Zoe's skeleton is very weird so this can help figure out which bones are connected where
    public static void LogTransformHierarchy(Transform t, string gap = "")
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
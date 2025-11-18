using System.Collections.Generic;
using UnityEngine;

namespace AethaModelSwapMod;

public class AnimationParameters
{
    public Vector3 offsetPosition = Vector3.zero;
    public Quaternion offsetRotation = Quaternion.identity;
    public int playAnimation = -1;
    public bool disableIdle = false;
    public HashSet<string> animatedBoneNames;
}
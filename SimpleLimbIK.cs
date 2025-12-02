#nullable enable

// Modified from:
// https://github.com/ditzel/SimpleIK/tree/master

using UnityEngine;

namespace AethaModelSwapMod;
 
public class SimpleLimbIK
{
    readonly Transform _target;
    readonly Transform? _hint;

    const int Iterations = 10; // Iterations per call
    const float Delta = 0.001f; // Distance to stop moving
    const float SnapBackStrength = 1f; // Strength of going back to the start position.
    
    readonly float[] _bonesLength; //Target to Origin
    readonly float _completeLength;
    readonly Transform[] _bones;
    readonly Vector3[] _positions;
    readonly Vector3[] _startDirectionSucc;
    readonly Quaternion[] _startRotationBone;
    readonly Quaternion _startRotationTarget;
    readonly Transform _root;

    public SimpleLimbIK(Transform target, Transform hint, params Transform[] bones)
    {
        _target = target;
        _hint = hint;
        
        _bones = new Transform[bones.Length];
        _positions = new Vector3[bones.Length];
        _bonesLength = new float[bones.Length-1];
        _startDirectionSucc = new Vector3[bones.Length];
        _startRotationBone = new Quaternion[bones.Length];
        
        _root = bones[0].parent;

        _startRotationTarget = GetRotationRootSpace(target);
        
        _completeLength = 0;
        for (var i = _bones.Length - 1; i >= 0; i--)
        {
            var current = bones[i];
            _bones[i] = current;
            _startRotationBone[i] = GetRotationRootSpace(current);

            if (i == _bones.Length - 1)
            {
                //leaf
                _startDirectionSucc[i] = GetPositionRootSpace(target) - GetPositionRootSpace(current);
            }
            else
            {
                //mid bone
                _startDirectionSucc[i] = GetPositionRootSpace(_bones[i + 1]) - GetPositionRootSpace(current);
                _bonesLength[i] = _startDirectionSucc[i].magnitude;
                _completeLength += _bonesLength[i];
            }
        }
    }

    public void ResolveIK()
    {
        //get position
        for (int i = 0; i < _bones.Length; i++)
        {
            _positions[i] = GetPositionRootSpace(_bones[i]);
        }

        var targetPosition = GetPositionRootSpace(_target);
        var targetRotation = GetRotationRootSpace(_target);

        //1st is possible to reach?
        if ((targetPosition - GetPositionRootSpace(_bones[0])).sqrMagnitude >= _completeLength * _completeLength)
        {
            //just stretch it
            var direction = (targetPosition - _positions[0]).normalized;
            //set everything after root
            for (int i = 1; i < _positions.Length; i++)
            {
                _positions[i] = _positions[i - 1] + direction * _bonesLength[i - 1];
            }
        }
        else
        {
            for (int i = 0; i < _positions.Length - 1; i++)
            {
                _positions[i + 1] = Vector3.Lerp(_positions[i + 1], _positions[i] + _startDirectionSucc[i], SnapBackStrength);
            }

            for (int iteration = 0; iteration < Iterations; iteration++)
            {
                //https://www.youtube.com/watch?v=UNoX65PRehA
                //back
                for (int i = _positions.Length - 1; i > 0; i--)
                {
                    if (i == _positions.Length - 1)
                    {
                        _positions[i] = targetPosition; //set it to target
                    }
                    else
                    {
                        _positions[i] = _positions[i + 1] + (_positions[i] - _positions[i + 1]).normalized * _bonesLength[i]; //set in line on distance
                    }
                }

                //forward
                for (int i = 1; i < _positions.Length; i++)
                {
                    _positions[i] = _positions[i - 1] + (_positions[i] - _positions[i - 1]).normalized * _bonesLength[i - 1];
                }

                //close enough?
                if ((_positions[^1] - targetPosition).sqrMagnitude < Delta * Delta)
                {
                    break;
                }
            }
        }

        //move towards pole
        if (_hint)
        {
            var polePosition = GetPositionRootSpace(_hint!);
            for (int i = 1; i < _positions.Length - 1; i++)
            {
                var plane = new Plane(_positions[i + 1] - _positions[i - 1], _positions[i - 1]);
                var projectedPole = plane.ClosestPointOnPlane(polePosition);
                var projectedBone = plane.ClosestPointOnPlane(_positions[i]);
                var angle = Vector3.SignedAngle(projectedBone - _positions[i - 1], projectedPole - _positions[i - 1], plane.normal);
                _positions[i] = Quaternion.AngleAxis(angle, plane.normal) * (_positions[i] - _positions[i - 1]) + _positions[i - 1];
            }
        }

        //set position & rotation
        for (int i = 0; i < _positions.Length; i++)
        {
            if (i == _positions.Length - 1)
            {
                SetRotationRootSpace(_bones[i], Quaternion.Inverse(targetRotation) * _startRotationTarget * Quaternion.Inverse(_startRotationBone[i]));
            }
            else
            {
                
                SetRotationRootSpace(_bones[i], Quaternion.FromToRotation(_startDirectionSucc[i], _positions[i + 1] - _positions[i]) * Quaternion.Inverse(_startRotationBone[i]));
            }
            SetPositionRootSpace(_bones[i], _positions[i]);
        }
    }

    private Vector3 GetPositionRootSpace(Transform current)
    {
        return Quaternion.Inverse(_root.rotation) * (current.position - _root.position);
    }

    private void SetPositionRootSpace(Transform current, Vector3 position)
    {
        current.position = _root.rotation * position + _root.position;
    }

    private Quaternion GetRotationRootSpace(Transform current)
    {
        //inverse(after) * before => rot: before -> after
        return Quaternion.Inverse(current.rotation) * _root.rotation;
    }

    private void SetRotationRootSpace(Transform current, Quaternion rotation)
    {
        current.rotation = _root.rotation * rotation;
    }

}
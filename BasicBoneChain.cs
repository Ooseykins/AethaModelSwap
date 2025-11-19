using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AethaModelSwapMod;

[DefaultExecutionOrder(5)]
public class BasicBoneChain : MonoBehaviour
{
    private class ChainLink
    {
        public readonly Transform transform;
        private Transform _parent;
        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private Vector3 _previousPos;
        private Vector3 _velocity;

        public ChainLink(Transform transform)
        {
            this.transform = transform;
            _parent = transform.parent;
            _basePosition = transform.localPosition;
            _baseRotation = transform.localRotation;
            _previousPos = transform.position;
            _velocity = Vector3.zero;
        }

        public void UpdatePosition(float deltaTime, Vector3 gravity, float damping, float maxAngle, float maxSpeed, List<Plane> constraintPlanes)
        {
            _velocity += gravity * deltaTime;
            var startPos = _previousPos + _velocity * deltaTime;
            var maxDist = Vector3.Distance(_parent.position, startPos) - _basePosition.magnitude;
            transform.position = Vector3.MoveTowards(startPos, _parent.position, maxDist);

            _velocity = (_velocity + ((transform.position - _previousPos) / deltaTime)) / 2f;
            
            foreach (var plane in constraintPlanes)
            {
                if (!plane.GetSide(transform.position))
                {
                    var prevPos = transform.position;
                    transform.localPosition = _basePosition;
                    transform.position = plane.ClosestPointOnPlane((transform.position + prevPos)/2f);
                    //transform.position = Vector3.Lerp(transform.position, plane.ClosestPointOnPlane(transform.position), 0.5f);
                    var dist = Vector3.Distance(transform.position, _parent.position) - _basePosition.magnitude;
                    if (dist > 0)
                    {
                        transform.position = Vector3.MoveTowards(transform.position, _parent.position, dist);
                    }
                }
            }

            var angle = Vector3.Angle(_basePosition.normalized, transform.localPosition.normalized);
            if (maxAngle >= 0)
            {
                angle = Mathf.Clamp(angle, -maxAngle, maxAngle);
            }

            var axis = Vector3.Cross((_basePosition - transform.localPosition).normalized, _basePosition.normalized);
            var newDir = Quaternion.AngleAxis(angle, axis) * _basePosition.normalized;
            transform.localRotation = _baseRotation * Quaternion.AngleAxis(angle, axis);
            transform.localPosition = newDir * transform.localPosition.magnitude;
            
            _velocity *= Mathf.Pow(damping, deltaTime);
            if (maxSpeed >= 0 && _velocity.magnitude > maxSpeed)
            {
                _velocity = _velocity.normalized * maxSpeed;
            }
            _previousPos = transform.position;
        }
    }

    public float maxAngle = -1;
    public float maxSpeed = -1;
    public float damping = 0.0001f;
    public Vector3 gravity;
    public float planeForce = 0f;
    public List<Transform> chainElements = new();
    private List<ChainLink> _links = new ();
    
    public List<Transform> colliderTransforms = new();
    public List<Vector3> colliderNormals = new();
    public List<Vector3> colliderOffsets = new();

    public void AddConstraintPlane(Transform root, Vector3 normal, Vector3 offset)
    {
        colliderTransforms.Add(root);
        colliderNormals.Add(normal.normalized);
        colliderOffsets.Add(offset);
    }

    public void AddSingleLink(Transform link)
    {
        chainElements.Add(link);
    }
    
    public void AddChainFromRoot(Transform root)
    {
        AddChainFromRootRecursive(root);
        Init();
    }

    void AddChainFromRootRecursive(Transform root)
    {
        chainElements.Add(root);
        for (int i = 0; i < root.childCount; i++)
        {
            AddChainFromRootRecursive(root.GetChild(i));
        }
    }
    
    public void AddChainFromEnd(Transform end, int length)
    {
        Transform current = end;
        for (int i = 0; i < length; i++)
        {
            if (current == null)
            {
                break;
            }
            chainElements.Add(current);
            current = current.parent;
        }
        Init();
    }
    
    private void Awake()
    {
        Init();
    }

    public void Init()
    {
        if (!chainElements.Any())
        {
            return;
        }
        _links.Clear();
        foreach (var t in chainElements)
        {
            _links.Add(new ChainLink(t));
        }
        _links = _links.OrderBy(x => GetHierarchyDepth(x.transform)).ToList();
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
    
    private void LateUpdate()
    {
        Vector3 totalPlaneForce = Vector3.zero;
        List<Plane> colliderPlanes = new();
        for (int i = 0; i < colliderTransforms.Count; i++)
        {
            Vector3 direction = colliderTransforms[i].TransformDirection(colliderNormals[i].normalized).normalized;
            Vector3 position = colliderTransforms[i].TransformPoint(colliderOffsets[i]);
            colliderPlanes.Add(new Plane(direction, position));

            totalPlaneForce += colliderTransforms[i].TransformDirection(colliderNormals[i].normalized).normalized * planeForce;
        }
        foreach (var t in _links)
        {
            if (Time.deltaTime > 0f)
            {
                t.UpdatePosition(Time.deltaTime, gravity + totalPlaneForce, damping, maxAngle, maxSpeed, colliderPlanes);
            }
        }
    }
}

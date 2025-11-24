using UnityEngine;

namespace AethaModelSwapMod;

[DefaultExecutionOrder(3)]
public class CompanionFollower : MonoBehaviour
{
    public Vector3 followOffset;
    public float followSpeed = 1f;

    public float driftVertical = 0.5f;
    public float driftVerticalSpeed = 0.1f;
    public float driftHorizontal = 0.2f;
    public float driftHorizontalSpeed = 1f;

    public Quaternion rotation = Quaternion.identity;
    
    private Transform _followTarget;
    private Vector3 _initialPosition;
    private Vector3 _prevPosition;

    private void Awake()
    {
        _initialPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        transform.localPosition = _initialPosition;
        _prevPosition = transform.position;
    }

    private void LateUpdate()
    {
        if (Time.deltaTime <= 0)
        {
            return;
        }
        if (!_followTarget)
        {
            var pc = GetComponentInParent<PlayerCharacter>();
            if (!pc)
            {
                Destroy(this);
                return;
            }
            _followTarget = pc.refs.playerVisualRotation.visual;
            if (!_followTarget)
            {
                Destroy(this);
                return;
            }
        }

        var horizontal = Mathf.Cos(Time.time * driftHorizontalSpeed) * driftHorizontal;
        var vertical = Mathf.Cos(Time.time * driftVerticalSpeed) * driftVertical;
        var drift = new Vector3(horizontal, vertical, 0f);
        
        var p1 = _prevPosition;
        var p2 = _followTarget.TransformPoint(followOffset + drift);
        var dist = Vector3.Distance(p1, p2);
        
        if (Vector3.Distance(p1, p2) > 500f)
        {
            transform.position = p2;
            _prevPosition = transform.position;
        }
        else
        {
            _prevPosition = Vector3.MoveTowards(p1, p2, dist * Time.deltaTime * followSpeed);
            transform.position = _prevPosition;
        }

        transform.rotation = _followTarget.rotation * rotation;
    }
}
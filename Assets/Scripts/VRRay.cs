using UnityEngine;

/// <summary>
/// Simple always-on ray visual for the VR selection system.
/// Attach to the Right Controller. Draws a LineRenderer forward from the controller.
/// VRSelectionManager reads RayOrigin from this transform.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class VRRay : MonoBehaviour
{
    [SerializeField] float    maxLength   = 20f;
    [SerializeField] Color    rayColor    = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] float    rayWidth    = 0.003f;
    [SerializeField] LayerMask hitMask    = ~0;

    LineRenderer _line;

    void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _line.positionCount    = 2;
        _line.useWorldSpace    = true;
        _line.startWidth       = rayWidth;
        _line.endWidth         = rayWidth;
        _line.material         = new Material(Shader.Find("Sprites/Default"));
        _line.startColor       = rayColor;
        _line.endColor         = new Color(rayColor.r, rayColor.g, rayColor.b, 0f);
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows   = false;
    }

    void Update()
    {
        Vector3 origin = transform.position;
        Vector3 dir    = transform.forward;

        Vector3 endpoint = Physics.Raycast(origin, dir, out RaycastHit hit, maxLength, hitMask)
            ? hit.point
            : origin + dir * maxLength;

        _line.SetPosition(0, origin);
        _line.SetPosition(1, endpoint);
    }
}

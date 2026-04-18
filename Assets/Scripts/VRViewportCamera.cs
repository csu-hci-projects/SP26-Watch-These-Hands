using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Blender-style turntable orbit for VR.
/// Rotates and translates a World Root GameObject so the scene orbits around
/// the user. XR Origin never moves — no headset tracking conflicts.
///
/// Right thumbstick = orbit  |  Left stick X = pan  |  Left stick Y = zoom
/// </summary>
public class VRViewportCamera : MonoBehaviour
{
    public static VRViewportCamera Instance { get; private set; }

    [Header("References")]
    [Tooltip("Parent of all scene objects (Cube, Grid, etc).")]
    [SerializeField] Transform worldRoot;
    [Tooltip("Main Camera inside XR Origin — Camera Offset → Main Camera.")]
    [SerializeField] Transform vrCamera;

    [Header("Orbit")]
    [SerializeField] float orbitSpeed    = 90f;
    [SerializeField] float minElevation  = -89f;
    [SerializeField] float maxElevation  =  89f;

    [Header("Pan")]
    [SerializeField] float panSpeed = 2f;

    [Header("Zoom")]
    [SerializeField] float zoomSpeed   = 3f;
    [SerializeField] float minDistance = 0.2f;
    [SerializeField] float maxDistance = 500f;

    [Header("Input Actions")]
    [Tooltip("Right thumbstick — XRI Right/Thumbstick")]
    [SerializeField] InputActionReference orbitAction;
    [Tooltip("Left thumbstick  — XRI Left/Thumbstick")]
    [SerializeField] InputActionReference panZoomAction;

    [Header("Starting Pivot")]
    [Tooltip("Local position inside World Root to orbit around. Match your cube's local position.")]
    [SerializeField] Vector3 pivotLocal = new Vector3(0f, 0.5f, 0f);
    [Tooltip("How far in front of the user the pivot starts.")]
    [SerializeField] float startDistance = 7f;

    // Elevation angle tracked so we can clamp it
    float _elevation = 35f;

    void Awake() => Instance = this;

    void Start()
    {
        if (worldRoot == null || vrCamera == null) return;

        // Place worldRoot so the pivot appears directly in front of the user at startDistance.
        Vector3 eye     = vrCamera.position;
        Vector3 forward = vrCamera.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 targetPivotWorld = eye + forward * startDistance;
        Vector3 currentPivotWorld = worldRoot.TransformPoint(pivotLocal);
        worldRoot.position += targetPivotWorld - currentPivotWorld;
    }

    void OnEnable()
    {
        orbitAction?.action.Enable();
        panZoomAction?.action.Enable();
    }

    void OnDisable()
    {
        orbitAction?.action.Disable();
        panZoomAction?.action.Disable();
    }

    void Update()
    {
        if (worldRoot == null || vrCamera == null) return;

        Vector2 orbit   = orbitAction?.action.ReadValue<Vector2>()  ?? Vector2.zero;
        Vector2 panZoom = panZoomAction?.action.ReadValue<Vector2>() ?? Vector2.zero;
        float   dt      = Time.deltaTime;

        Vector3 pivotWorld = worldRoot.TransformPoint(pivotLocal);

        // Camera-relative axes (flat = projected onto horizontal plane)
        Vector3 camForward = vrCamera.forward;
        Vector3 camRight   = vrCamera.right;
        Vector3 flatForward = new Vector3(camForward.x, 0f, camForward.z).normalized;
        Vector3 flatRight   = new Vector3(camRight.x,   0f, camRight.z).normalized;

        // ── Orbit ─────────────────────────────────────────────────────────────
        if (orbit.sqrMagnitude > 0.01f)
        {
            // Azimuth: rotate around world up, direction relative to camera facing
            float azDelta = -orbit.x * orbitSpeed * dt;
            worldRoot.RotateAround(pivotWorld, Vector3.up, azDelta);
            pivotWorld = worldRoot.TransformPoint(pivotLocal);

            // Elevation: rotate around camera's flat-right axis
            float elDelta = orbit.y * orbitSpeed * dt;
            float newEl   = Mathf.Clamp(_elevation + elDelta, minElevation, maxElevation);
            elDelta    = newEl - _elevation;
            _elevation = newEl;

            if (Mathf.Abs(elDelta) > 0.001f && flatRight.sqrMagnitude > 0.001f)
                worldRoot.RotateAround(pivotWorld, flatRight, elDelta);

            pivotWorld = worldRoot.TransformPoint(pivotLocal);
        }

        // ── Pan: camera-relative horizontal ───────────────────────────────────
        if (Mathf.Abs(panZoom.x) > 0.01f)
        {
            float dist = Vector3.Distance(vrCamera.position, pivotWorld);
            worldRoot.position -= flatRight * (panZoom.x * panSpeed * dist * 0.01f * dt * 60f);
        }

        // ── Zoom: along pivot→camera axis ─────────────────────────────────────
        if (Mathf.Abs(panZoom.y) > 0.01f)
        {
            Vector3 pivotToCam = vrCamera.position - pivotWorld;
            float   dist       = pivotToCam.magnitude;
            float   newDist    = Mathf.Clamp(dist * Mathf.Exp(-panZoom.y * zoomSpeed * dt),
                                             minDistance, maxDistance);
            worldRoot.position += pivotToCam.normalized * (dist - newDist);
        }
    }

    /// <summary>Called by VRSelectionManager when an object is selected.</summary>
    public void SetPivot(Vector3 worldPos)
    {
        if (worldRoot == null) return;
        pivotLocal = worldRoot.InverseTransformPoint(worldPos);
    }
}

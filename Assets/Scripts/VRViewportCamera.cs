using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Orbit-style viewport controls for VR modeling.
/// The workspace stays fixed in world space while the XR Origin moves around a pivot.
/// </summary>
public class VRViewportCamera : MonoBehaviour
{
    public static VRViewportCamera Instance { get; private set; }

    [Header("References")]
    [SerializeField] Transform xrOrigin;
    [SerializeField] Transform vrCamera;

    [Header("Orbit")]
    [SerializeField] float orbitSpeed = 35f;
    [SerializeField] float minElevation = -80f;
    [SerializeField] float maxElevation = 80f;
    [SerializeField] float orbitDeadzone = 0.2f;
    [SerializeField] float orbitAxisAssist = 0.65f;

    [Header("Traverse")]
    [SerializeField] float traverseSpeed = 45f;
    [SerializeField] float traverseDeadzone = 0.2f;

    [Header("Zoom")]
    [SerializeField] float zoomSpeed = 2.2f;
    [SerializeField] float zoomDeadzone = 0.2f;
    [SerializeField] float minDistance = 1.25f;
    [SerializeField] float maxDistance = 1000f;

    [Header("Input Actions")]
    [SerializeField] InputActionReference orbitAction;
    [SerializeField] InputActionReference panZoomAction;

    [Header("Framing")]
    [SerializeField] Vector3 pivotWorld = new Vector3(0f, 0.5f, 0f);
    [SerializeField] float startDistance = 2.25f;
    [SerializeField] float startElevation = 10f;

    float azimuth;
    float elevation;
    float distance;
    bool initialized;
    bool applyingOriginPose;
    Vector3 lastAuthorizedOriginPosition;
    Quaternion lastAuthorizedOriginRotation;

    void Awake()
    {
        Instance = this;
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

    void OnValidate()
    {
        orbitDeadzone = Mathf.Clamp01(orbitDeadzone);
        traverseDeadzone = Mathf.Clamp01(traverseDeadzone);
        zoomDeadzone = Mathf.Clamp01(zoomDeadzone);
        minDistance = Mathf.Max(0.25f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);
        startDistance = Mathf.Clamp(startDistance, minDistance, maxDistance);
        startElevation = Mathf.Clamp(startElevation, minElevation, maxElevation);
    }

    void LateUpdate()
    {
        if (xrOrigin == null || vrCamera == null)
            return;

        if (!initialized)
        {
            if (vrCamera.position.sqrMagnitude > 0.0001f)
            {
                Recenter();
                initialized = true;
            }

            return;
        }

        EnforceAuthorizedOriginPose();

        bool disableViewport =
            (VRSelectionManager.Instance != null && VRSelectionManager.Instance.IsSelectionGrabbed) ||
            (VRTransformTool.Instance != null && VRTransformTool.Instance.IsManipulatingGizmo);

        Vector2 orbitInput = disableViewport
            ? Vector2.zero
            : ApplyDeadzone(orbitAction?.action.ReadValue<Vector2>() ?? Vector2.zero, orbitDeadzone);
        orbitInput = ApplyAxisAssist(orbitInput, orbitAxisAssist);
        Vector2 traverseZoomInput = disableViewport
            ? Vector2.zero
            : panZoomAction?.action.ReadValue<Vector2>() ?? Vector2.zero;

        float traverseInput = ApplyDeadzone(traverseZoomInput.x, traverseDeadzone);
        float zoomInput = ApplyDeadzone(traverseZoomInput.y, zoomDeadzone);
        float dt = Time.deltaTime;

        elevation = Mathf.Clamp(elevation + orbitInput.y * orbitSpeed * dt, minElevation, maxElevation);

        if (Mathf.Abs(traverseInput) > 0.0001f)
            azimuth -= traverseInput * traverseSpeed * dt;

        if (Mathf.Abs(zoomInput) > 0.0001f)
            distance = Mathf.Clamp(distance * Mathf.Exp(-zoomInput * zoomSpeed * dt), minDistance, maxDistance);

        ApplyOrbitPose();
    }

    public void SetPivot(Vector3 worldPos)
    {
        pivotWorld = worldPos;

        if (initialized)
            SyncOrbitFromCurrentPose();
    }

    public void Recenter()
    {
        if (xrOrigin == null || vrCamera == null)
            return;

        var flatForward = Flatten(vrCamera.forward, Vector3.forward);
        var startDirection = Quaternion.Euler(-startElevation, 0f, 0f) * -flatForward;

        azimuth = Mathf.Atan2(startDirection.x, startDirection.z) * Mathf.Rad2Deg;
        elevation = Mathf.Clamp(startElevation, minElevation, maxElevation);
        distance = startDistance;

        ApplyOrbitPose();
    }

    void SyncOrbitFromCurrentPose()
    {
        Vector3 offset = vrCamera.position - pivotWorld;
        distance = Mathf.Clamp(offset.magnitude, minDistance, maxDistance);

        if (distance <= 0.0001f)
            return;

        Vector3 direction = offset / distance;
        elevation = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg, minElevation, maxElevation);
        azimuth = Mathf.Atan2(-direction.x, -direction.z) * Mathf.Rad2Deg;
    }

    void ApplyOrbitPose()
    {
        var originTransform = xrOrigin;
        var desiredDirection = Quaternion.Euler(elevation, azimuth, 0f) * Vector3.back;
        var desiredCameraPosition = pivotWorld + desiredDirection.normalized * distance;

        var flatToPivot = Flatten(pivotWorld - desiredCameraPosition, originTransform.forward);
        float targetYaw = originTransform.eulerAngles.y;
        if (flatToPivot.sqrMagnitude > 0.0001f)
            targetYaw = Quaternion.LookRotation(flatToPivot, Vector3.up).eulerAngles.y;

        var targetRotation = Quaternion.Euler(0f, targetYaw, 0f);
        var cameraLocalOffset = Quaternion.Inverse(originTransform.rotation) * (vrCamera.position - originTransform.position);

        applyingOriginPose = true;
        originTransform.rotation = targetRotation;
        originTransform.position = desiredCameraPosition - targetRotation * cameraLocalOffset;
        lastAuthorizedOriginPosition = originTransform.position;
        lastAuthorizedOriginRotation = originTransform.rotation;
        applyingOriginPose = false;
    }

    void EnforceAuthorizedOriginPose()
    {
        if (applyingOriginPose || xrOrigin == null)
            return;

        if ((xrOrigin.position - lastAuthorizedOriginPosition).sqrMagnitude > 0.000001f ||
            Quaternion.Angle(xrOrigin.rotation, lastAuthorizedOriginRotation) > 0.01f)
        {
            xrOrigin.SetPositionAndRotation(lastAuthorizedOriginPosition, lastAuthorizedOriginRotation);
        }
    }

    static Vector3 Flatten(Vector3 vector, Vector3 fallback)
    {
        vector.y = 0f;
        if (vector.sqrMagnitude < 0.0001f)
            return fallback.normalized;

        return vector.normalized;
    }

    static Vector2 ApplyDeadzone(Vector2 value, float deadzone)
    {
        float magnitude = value.magnitude;
        if (magnitude <= deadzone)
            return Vector2.zero;

        float scaledMagnitude = (magnitude - deadzone) / Mathf.Max(0.0001f, 1f - deadzone);
        return value.normalized * scaledMagnitude;
    }

    static float ApplyDeadzone(float value, float deadzone)
    {
        float magnitude = Mathf.Abs(value);
        if (magnitude <= deadzone)
            return 0f;

        float scaledMagnitude = (magnitude - deadzone) / Mathf.Max(0.0001f, 1f - deadzone);
        return Mathf.Sign(value) * scaledMagnitude;
    }

    static Vector2 ApplyAxisAssist(Vector2 value, float assist)
    {
        assist = Mathf.Clamp01(assist);
        float absX = Mathf.Abs(value.x);
        float absY = Mathf.Abs(value.y);
        if (absX < 0.0001f || absY < 0.0001f || assist <= 0f)
            return value;

        if (absX >= absY)
        {
            float suppression = Mathf.Clamp01((absX - absY) / absX) * assist;
            value.y *= 1f - suppression;
        }
        else
        {
            float suppression = Mathf.Clamp01((absY - absX) / absY) * assist;
            value.x *= 1f - suppression;
        }

        return value;
    }
}

using UnityEngine;

/// <summary>
/// Blender-style turntable orbit camera.
/// Middle Mouse = orbit | Shift+Middle Mouse = pan | Scroll = zoom
/// Numpad 1/3/7 = Front/Right/Top | Numpad 5 = toggle ortho
/// </summary>
[RequireComponent(typeof(Camera))]
public class ViewportCamera : MonoBehaviour
{
    public static ViewportCamera Instance { get; private set; }

    [Header("Orbit")]
    [SerializeField] float orbitSpeed    = 0.35f;
    [SerializeField] float minElevation  = -89f;
    [SerializeField] float maxElevation  =  89f;

    [Header("Pan")]
    [SerializeField] float panSpeed = 0.0015f;

    [Header("Zoom")]
    [SerializeField] float zoomSpeed    = 0.12f;
    [SerializeField] float minDistance  = 0.2f;
    [SerializeField] float maxDistance  = 500f;

    // Spherical-coordinate state
    float _azimuth   = 45f;   // degrees, horizontal
    float _elevation = 30f;   // degrees, vertical
    float _distance  = 8f;

    Vector3 _pivot = Vector3.zero;
    Vector2 _prevMouse;
    Camera  _cam;

    void Awake()
    {
        Instance = this;
        _cam = GetComponent<Camera>();
        ApplyTransform();
    }

    void Update()
    {
        HandleScroll();
        HandleMiddleMouse();
        HandleNumpad();
    }

    // ── Scroll zoom ──────────────────────────────────────────────────────────

    void HandleScroll()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < 0.0001f) return;

        _distance *= Mathf.Exp(-scroll * zoomSpeed * 10f);
        _distance  = Mathf.Clamp(_distance, minDistance, maxDistance);
        ApplyTransform();
    }

    // ── Middle mouse orbit / pan ─────────────────────────────────────────────

    void HandleMiddleMouse()
    {
        if (Input.GetMouseButtonDown(2))
            _prevMouse = Input.mousePosition;

        if (!Input.GetMouseButton(2)) return;

        Vector2 current = Input.mousePosition;
        Vector2 delta   = current - _prevMouse;
        _prevMouse      = current;

        if (delta.sqrMagnitude < 0.0001f) return;

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (shift)
        {
            // Pan: move pivot in camera's local XY plane
            float scale = _distance * panSpeed;
            _pivot -= transform.right * (delta.x * scale);
            _pivot -= transform.up    * (delta.y * scale);
        }
        else
        {
            // Turntable orbit (Blender default)
            _azimuth   += delta.x * orbitSpeed;
            _elevation += delta.y * orbitSpeed;   // drag up = look from above
            _elevation  = Mathf.Clamp(_elevation, minElevation, maxElevation);
        }

        ApplyTransform();
    }

    // ── Number key preset views ──────────────────────────────────────────────

    void HandleNumpad()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetView(0f,  0f);  // Front
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetView(90f, 0f);  // Right
        if (Input.GetKeyDown(KeyCode.Alpha7)) SetView(0f, 89f);  // Top
        if (Input.GetKeyDown(KeyCode.Alpha4)) { _azimuth -= 15f; ApplyTransform(); }
        if (Input.GetKeyDown(KeyCode.Alpha6)) { _azimuth += 15f; ApplyTransform(); }
        if (Input.GetKeyDown(KeyCode.Alpha8)) { _elevation = Mathf.Clamp(_elevation + 15f, minElevation, maxElevation); ApplyTransform(); }
        if (Input.GetKeyDown(KeyCode.Alpha2)) { _elevation = Mathf.Clamp(_elevation - 15f, minElevation, maxElevation); ApplyTransform(); }

        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            _cam.orthographic = !_cam.orthographic;
            if (_cam.orthographic)
                _cam.orthographicSize = _distance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            ApplyTransform();
        }
    }

    void SetView(float azimuth, float elevation)
    {
        _azimuth   = azimuth;
        _elevation = elevation;
        ApplyTransform();
    }

    // ── Core ─────────────────────────────────────────────────────────────────

    void ApplyTransform()
    {
        float az = _azimuth   * Mathf.Deg2Rad;
        float el = _elevation * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            _distance * Mathf.Cos(el) * Mathf.Sin(az),
            _distance * Mathf.Sin(el),
            _distance * Mathf.Cos(el) * Mathf.Cos(az)
        );

        transform.position = _pivot + offset;
        transform.LookAt(_pivot, Vector3.up);
    }

    public void SetPivot(Vector3 pivot)
    {
        _pivot = pivot;
        ApplyTransform();
    }
}

using UnityEngine;
using TMPro;

/// <summary>
/// Blender-style object transforms on a selected object.
///
///   G  →  Grab (translate in screen plane)
///   R  →  Rotate
///   S  →  Scale
///
/// After starting a mode press X / Y / Z to constrain to that world axis.
/// Press the same axis key again to go back to Free.
///
/// Confirm : Left-click or Enter
/// Cancel  : Right-click or Escape
/// </summary>
public class TransformTool : MonoBehaviour
{
    public static TransformTool Instance { get; private set; }

    [Tooltip("Optional — drag a TextMeshProUGUI element here for live status.")]
    public TextMeshProUGUI statusText;

    // ── State ────────────────────────────────────────────────────────────────

    enum Mode { None, Grab, Rotate, Scale }
    enum Axis { Free, X, Y, Z }

    Mode _mode = Mode.None;
    Axis _axis = Axis.Free;

    Transform _target;
    Vector3    _origPos;
    Quaternion _origRot;
    Vector3    _origScale;

    Vector2 _startMouse;
    Camera  _cam;

    // ── Public API ───────────────────────────────────────────────────────────

    public bool IsTransforming => _mode != Mode.None;

    public void SetTarget(Transform t)
    {
        if (_mode != Mode.None) CancelTransform();
        _target = t;
    }

    // ── Unity ────────────────────────────────────────────────────────────────

    void Awake()
    {
        Instance = this;
        _cam = Camera.main;
    }

    void Update()
    {
        if (_cam == null) _cam = Camera.main;
        if (_target == null) return;

        if (_mode == Mode.None)
            HandleHotkeys();
        else
        {
            HandleAxisKeys();
            ApplyTransform();
            HandleConfirmOrCancel();
        }

        RefreshStatus();
    }

    // ── Input handlers ───────────────────────────────────────────────────────

    void HandleHotkeys()
    {
        if (Input.GetKeyDown(KeyCode.G)) BeginMode(Mode.Grab);
        else if (Input.GetKeyDown(KeyCode.R)) BeginMode(Mode.Rotate);
        else if (Input.GetKeyDown(KeyCode.S)) BeginMode(Mode.Scale);
    }

    void BeginMode(Mode mode)
    {
        _mode       = mode;
        _axis       = Axis.Free;
        _startMouse = Input.mousePosition;
        _origPos    = _target.position;
        _origRot    = _target.rotation;
        _origScale  = _target.localScale;
    }

    void HandleAxisKeys()
    {
        if (Input.GetKeyDown(KeyCode.X)) _axis = (_axis == Axis.X) ? Axis.Free : Axis.X;
        if (Input.GetKeyDown(KeyCode.Y)) _axis = (_axis == Axis.Y) ? Axis.Free : Axis.Y;
        if (Input.GetKeyDown(KeyCode.Z)) _axis = (_axis == Axis.Z) ? Axis.Free : Axis.Z;
    }

    void HandleConfirmOrCancel()
    {
        bool confirm = Input.GetMouseButtonDown(0)
                    || Input.GetKeyDown(KeyCode.Return)
                    || Input.GetKeyDown(KeyCode.KeypadEnter);

        bool cancel  = Input.GetMouseButtonDown(1)
                    || Input.GetKeyDown(KeyCode.Escape);

        if (confirm) _mode = Mode.None;
        else if (cancel) CancelTransform();
    }

    void CancelTransform()
    {
        _target.position   = _origPos;
        _target.rotation   = _origRot;
        _target.localScale = _origScale;
        _mode = Mode.None;
    }

    // ── Transform logic ──────────────────────────────────────────────────────

    void ApplyTransform()
    {
        Vector2 current = Input.mousePosition;

        switch (_mode)
        {
            case Mode.Grab:   ApplyGrab(current);   break;
            case Mode.Rotate: ApplyRotate(current);  break;
            case Mode.Scale:  ApplyScale(current);   break;
        }
    }

    void ApplyGrab(Vector2 current)
    {
        // Project both mouse positions to a world-space plane at the object's depth.
        float depth = _cam.WorldToScreenPoint(_origPos).z;

        Vector3 startWorld   = _cam.ScreenToWorldPoint(new Vector3(_startMouse.x, _startMouse.y, depth));
        Vector3 currentWorld = _cam.ScreenToWorldPoint(new Vector3(current.x,     current.y,     depth));
        Vector3 delta        = currentWorld - startWorld;

        delta = ConstrainVector(delta);

        _target.position = _origPos + delta;
    }

    void ApplyRotate(Vector2 current)
    {
        float angle = (current.x - _startMouse.x) * 0.5f;  // 0.5° per pixel

        Vector3 axis = GetWorldAxis();
        if (axis == Vector3.zero) axis = _cam.transform.forward; // free = view axis

        _target.rotation = _origRot;
        _target.RotateAround(_target.position, axis, angle);
    }

    void ApplyScale(Vector2 current)
    {
        // Scale factor = current mouse distance from object screen-centre
        //              / start mouse distance from object screen-centre.
        Vector2 objScreen = _cam.WorldToScreenPoint(_origPos);
        float startDist   = Vector2.Distance(_startMouse, objScreen);
        float currentDist = Vector2.Distance(current,     objScreen);

        float factor = startDist > 0.5f ? currentDist / startDist : 1f;
        factor = Mathf.Max(0.001f, factor);

        if (_axis == Axis.Free)
        {
            _target.localScale = _origScale * factor;
        }
        else
        {
            Vector3 s = _origScale;
            if (_axis == Axis.X) s.x *= factor;
            if (_axis == Axis.Y) s.y *= factor;
            if (_axis == Axis.Z) s.z *= factor;
            _target.localScale = s;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    Vector3 GetWorldAxis()
    {
        return _axis switch
        {
            Axis.X => Vector3.right,
            Axis.Y => Vector3.up,
            Axis.Z => Vector3.forward,
            _      => Vector3.zero,
        };
    }

    Vector3 ConstrainVector(Vector3 v)
    {
        return _axis switch
        {
            Axis.X => Vector3.Project(v, Vector3.right),
            Axis.Y => Vector3.Project(v, Vector3.up),
            Axis.Z => Vector3.Project(v, Vector3.forward),
            _      => v,
        };
    }

    void RefreshStatus()
    {
        if (statusText == null) return;

        if (_mode == Mode.None)
        {
            statusText.text = _target != null
                ? "G  Grab    R  Rotate    S  Scale"
                : "";
            return;
        }

        string axisLabel = _axis == Axis.Free ? "" : $"  |  {_axis} axis";
        statusText.text = $"{_mode}{axisLabel}    [LMB / Enter = confirm]  [RMB / Esc = cancel]";
    }
}

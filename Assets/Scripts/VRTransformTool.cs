using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Blender-style object transforms for VR controllers.
/// Drop-in VR equivalent of TransformTool.cs.
///
///   A button (right) = Grab   — move controller to move object
///   B button (right) = Rotate — rotate controller to rotate object
///   X button (left)  = Scale  — move controller toward/away from object to scale
///
///   Right trigger = Confirm
///   Left  grip    = Cancel
/// </summary>
public class VRTransformTool : MonoBehaviour
{
    public static VRTransformTool Instance { get; private set; }

    [Header("References")]
    [Tooltip("Right Controller transform — used to drive grab / rotate / scale.")]
    [SerializeField] Transform controllerTransform;

    [Header("Optional UI")]
    public TextMeshProUGUI statusText;

    [Header("Input Actions")]
    [Tooltip("A button (right) — XRI RightHand/PrimaryButton")]
    [SerializeField] InputActionReference grabAction;
    [Tooltip("B button (right) — XRI RightHand/SecondaryButton")]
    [SerializeField] InputActionReference rotateAction;
    [Tooltip("X button (left)  — XRI LeftHand/PrimaryButton")]
    [SerializeField] InputActionReference scaleAction;
    [Tooltip("Right trigger pressed — XRI RightHand/Select")]
    [SerializeField] InputActionReference confirmAction;
    [Tooltip("Left grip pressed — XRI LeftHand/Activate")]
    [SerializeField] InputActionReference cancelAction;

    // ── State ─────────────────────────────────────────────────────────────────

    enum Mode { None, Grab, Rotate, Scale }

    Mode       _mode = Mode.None;
    Transform  _target;
    Vector3    _origPos;
    Quaternion _origRot;
    Vector3    _origScale;

    Vector3    _controllerStartPos;
    Quaternion _controllerStartRot;
    float      _startDistToObject;

    public bool IsTransforming => _mode != Mode.None;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Awake() => Instance = this;

    void OnEnable()
    {
        grabAction?.action.Enable();
        rotateAction?.action.Enable();
        scaleAction?.action.Enable();
        confirmAction?.action.Enable();
        cancelAction?.action.Enable();
    }

    void OnDisable()
    {
        grabAction?.action.Disable();
        rotateAction?.action.Disable();
        scaleAction?.action.Disable();
        confirmAction?.action.Disable();
        cancelAction?.action.Disable();
    }

    public void SetTarget(Transform t)
    {
        if (_mode != Mode.None) CancelTransform();
        _target = t;
    }

    void Update()
    {
        if (_target == null) { RefreshStatus(); return; }

        if (_mode == Mode.None)
            HandleModeButtons();
        else
        {
            ApplyCurrentTransform();
            HandleConfirmOrCancel();
        }

        RefreshStatus();
    }

    // ── Mode entry ────────────────────────────────────────────────────────────

    void HandleModeButtons()
    {
        if      (grabAction   != null && grabAction.action.WasPressedThisFrame())   BeginMode(Mode.Grab);
        else if (rotateAction != null && rotateAction.action.WasPressedThisFrame()) BeginMode(Mode.Rotate);
        else if (scaleAction  != null && scaleAction.action.WasPressedThisFrame())  BeginMode(Mode.Scale);
    }

    void BeginMode(Mode mode)
    {
        _mode      = mode;
        _origPos   = _target.position;
        _origRot   = _target.rotation;
        _origScale = _target.localScale;

        if (controllerTransform != null)
        {
            _controllerStartPos = controllerTransform.position;
            _controllerStartRot = controllerTransform.rotation;
            _startDistToObject  = Vector3.Distance(controllerTransform.position, _target.position);
        }
    }

    // ── Transform application ─────────────────────────────────────────────────

    void ApplyCurrentTransform()
    {
        if (controllerTransform == null) return;

        switch (_mode)
        {
            case Mode.Grab:   ApplyGrab();   break;
            case Mode.Rotate: ApplyRotate(); break;
            case Mode.Scale:  ApplyScale();  break;
        }
    }

    void ApplyGrab()
    {
        // Object follows controller delta 1:1
        Vector3 delta = controllerTransform.position - _controllerStartPos;
        _target.position = _origPos + delta;
    }

    void ApplyRotate()
    {
        // Object inherits the rotation delta of the controller
        Quaternion delta = controllerTransform.rotation * Quaternion.Inverse(_controllerStartRot);
        _target.rotation = delta * _origRot;
    }

    void ApplyScale()
    {
        // Scale proportional to how much the controller moved toward/away from the object's origin
        float currentDist = Vector3.Distance(controllerTransform.position, _origPos);
        float factor = _startDistToObject > 0.001f ? currentDist / _startDistToObject : 1f;
        factor = Mathf.Max(0.001f, factor);
        _target.localScale = _origScale * factor;
    }

    // ── Confirm / Cancel ──────────────────────────────────────────────────────

    void HandleConfirmOrCancel()
    {
        if (confirmAction != null && confirmAction.action.WasPressedThisFrame())
            _mode = Mode.None;
        else if (cancelAction != null && cancelAction.action.WasPressedThisFrame())
            CancelTransform();
    }

    void CancelTransform()
    {
        _target.position   = _origPos;
        _target.rotation   = _origRot;
        _target.localScale = _origScale;
        _mode = Mode.None;
    }

    // ── Status text ───────────────────────────────────────────────────────────

    void RefreshStatus()
    {
        if (statusText == null) return;

        statusText.text = _mode == Mode.None
            ? (_target != null ? "A  Grab    B  Rotate    X  Scale" : "")
            : $"{_mode}   [Trigger = confirm]   [Grip = cancel]";
    }
}

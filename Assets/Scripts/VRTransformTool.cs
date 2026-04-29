using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Explicit rotate/scale mode layer for the VR study controls.
/// The selected object is grabbed normally, but rotate/scale are driven by the right stick.
/// </summary>
public class VRTransformTool : MonoBehaviour
{
    public static VRTransformTool Instance { get; private set; }

    enum Mode
    {
        None,
        Rotate,
        Scale
    }

    [Header("Optional UI")]
    [SerializeField] TMP_Text rotateLabel;
    [FormerlySerializedAs("moveLabel")]
    [SerializeField] TMP_Text scaleLabel;
    [SerializeField] Color inactiveColor = Color.white;
    [SerializeField] Color activeColor = new Color(1f, 0.9f, 0.1f, 1f);
    [SerializeField] TextMeshProUGUI statusText;

    [Header("Input Actions")]
    [SerializeField] InputActionReference rotateAction;
    [FormerlySerializedAs("moveAction")]
    [SerializeField] InputActionReference scaleAction;
    [FormerlySerializedAs("gizmoAction")]
    [FormerlySerializedAs("selectAction")]
    [SerializeField] InputActionReference joystickAction;
    [SerializeField] InputActionReference cancelAction;

    [Header("Mode Tuning")]
    [SerializeField] float stickDeadzone = 0.2f;
    [SerializeField] float rotateYawSpeed = 120f;
    [SerializeField] float rotatePitchSpeed = 90f;
    [SerializeField] float scaleSpeed = 1.2f;
    [SerializeField] float minimumUniformScale = 0.05f;
    [SerializeField] float maximumUniformScale = 20f;

    Mode mode = Mode.None;
    VRModelingInteractable activeInteractable;
    Transform activeTarget;
    bool manipulating;
    Vector3 lockedPosition;
    Quaternion lockedRotation;

    public bool IsTransforming => mode != Mode.None && manipulating;

    // Kept for VRViewportCamera compatibility with the temporary gizmo experiment.
    public bool IsManipulatingGizmo => IsTransforming;

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        rotateAction?.action.Enable();
        scaleAction?.action.Enable();
        joystickAction?.action.Enable();
        cancelAction?.action.Enable();
        RefreshUi();
    }

    void OnDisable()
    {
        if (activeInteractable != null)
            activeInteractable.ApplyMode(VRModelingInteractable.ToolMode.Free, VRModelingInteractable.Axis.Free);

        rotateAction?.action.Disable();
        scaleAction?.action.Disable();
        joystickAction?.action.Disable();
        cancelAction?.action.Disable();
    }

    void Update()
    {
        HandleModeToggle();
        SyncSelection();
        UpdateManipulation();
        RefreshUi();
    }

    void HandleModeToggle()
    {
        if (rotateAction != null && rotateAction.action.WasPressedThisFrame())
            SetMode(mode == Mode.Rotate ? Mode.None : Mode.Rotate);
        else if (scaleAction != null && scaleAction.action.WasPressedThisFrame())
            SetMode(mode == Mode.Scale ? Mode.None : Mode.Scale);
        else if (cancelAction != null && cancelAction.action.WasPressedThisFrame())
            SetMode(Mode.None);
    }

    void SetMode(Mode newMode)
    {
        if (mode == newMode)
            return;

        mode = newMode;
        manipulating = false;
        ApplyModeToActiveInteractable();
        RefreshUi();
    }

    void SyncSelection()
    {
        var selected = VRSelectionManager.Instance != null ? VRSelectionManager.Instance.Selected : null;
        var nextInteractable = selected != null ? selected.GetComponent<VRModelingInteractable>() : null;

        if (nextInteractable == activeInteractable)
            return;

        if (activeInteractable != null)
            activeInteractable.ApplyMode(VRModelingInteractable.ToolMode.Free, VRModelingInteractable.Axis.Free);

        activeInteractable = nextInteractable;
        activeTarget = activeInteractable != null ? activeInteractable.transform : null;
        manipulating = false;
        ApplyModeToActiveInteractable();
    }

    void ApplyModeToActiveInteractable()
    {
        if (activeInteractable == null)
            return;

        var nativeMode = mode switch
        {
            Mode.Rotate => VRModelingInteractable.ToolMode.Rotate,
            Mode.Scale => VRModelingInteractable.ToolMode.Scale,
            _ => VRModelingInteractable.ToolMode.Free,
        };

        activeInteractable.ApplyMode(nativeMode, VRModelingInteractable.Axis.Free);
    }

    void UpdateManipulation()
    {
        if (activeInteractable == null || activeTarget == null || mode == Mode.None)
        {
            manipulating = false;
            return;
        }

        bool isGrabbed = activeInteractable.GrabInteractable != null &&
            activeInteractable.GrabInteractable.interactorsSelecting.Count > 0;

        if (!isGrabbed)
        {
            manipulating = false;
            return;
        }

        if (!manipulating)
            BeginManipulation();

        var stick = ApplyDeadzone(joystickAction?.action.ReadValue<Vector2>() ?? Vector2.zero, stickDeadzone);
        if (stick == Vector2.zero)
        {
            PreserveLockedPose();
            return;
        }

        switch (mode)
        {
            case Mode.Rotate:
                ApplyRotate(stick);
                break;

            case Mode.Scale:
                ApplyScale(stick);
                break;
        }
    }

    void BeginManipulation()
    {
        manipulating = true;
        lockedPosition = activeTarget.position;
        lockedRotation = activeTarget.rotation;
    }

    void PreserveLockedPose()
    {
        activeTarget.position = lockedPosition;
        if (mode == Mode.Scale)
            activeTarget.rotation = lockedRotation;
    }

    void ApplyRotate(Vector2 stick)
    {
        activeTarget.position = lockedPosition;

        float dt = Time.deltaTime;
        if (Mathf.Abs(stick.x) > 0.0001f)
            activeTarget.Rotate(Vector3.up, -stick.x * rotateYawSpeed * dt, Space.World);

        if (Mathf.Abs(stick.y) > 0.0001f)
        {
            var cameraTransform = Camera.main != null ? Camera.main.transform : null;
            var pitchAxis = cameraTransform != null ? cameraTransform.right : Vector3.right;
            activeTarget.Rotate(pitchAxis, stick.y * rotatePitchSpeed * dt, Space.World);
        }
    }

    void ApplyScale(Vector2 stick)
    {
        activeTarget.position = lockedPosition;
        activeTarget.rotation = lockedRotation;

        float delta = stick.y * scaleSpeed * Time.deltaTime;
        if (Mathf.Abs(delta) <= 0.0001f)
            return;

        float scaleFactor = Mathf.Exp(delta);
        var scale = activeTarget.localScale * scaleFactor;
        float largest = Mathf.Max(scale.x, scale.y, scale.z);
        if (largest > maximumUniformScale)
            scale *= maximumUniformScale / Mathf.Max(0.0001f, largest);

        float smallest = Mathf.Min(scale.x, scale.y, scale.z);
        if (smallest < minimumUniformScale)
            scale *= minimumUniformScale / Mathf.Max(0.0001f, smallest);

        activeTarget.localScale = scale;
    }

    void RefreshUi()
    {
        if (rotateLabel != null)
            rotateLabel.color = mode == Mode.Rotate ? activeColor : inactiveColor;

        if (scaleLabel != null)
            scaleLabel.color = mode == Mode.Scale ? activeColor : inactiveColor;

        if (statusText == null)
            return;

        statusText.text = mode switch
        {
            Mode.Rotate => "Rotate mode: grab selected object, then use right stick",
            Mode.Scale => "Scale mode: grab selected object, then use right stick up/down",
            _ => ""
        };
    }

    static Vector2 ApplyDeadzone(Vector2 value, float deadzone)
    {
        float magnitude = value.magnitude;
        if (magnitude <= deadzone)
            return Vector2.zero;

        float scaledMagnitude = (magnitude - deadzone) / Mathf.Max(0.0001f, 1f - deadzone);
        return value.normalized * scaledMagnitude;
    }
}

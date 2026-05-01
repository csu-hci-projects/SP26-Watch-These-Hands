using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Explicit translate/rotate/scale mode layer for the VR study controls.
/// All three modes are driven by XRI-selectable gizmos.
/// </summary>
public class VRTransformTool : MonoBehaviour
{
    public static VRTransformTool Instance { get; private set; }

    enum Mode
    {
        None,
        Translate,
        Rotate,
        Scale
    }

    [Header("Optional UI")]
    [SerializeField] TMP_Text translateLabel;
    [SerializeField] TMP_Text rotateLabel;
    [FormerlySerializedAs("moveLabel")]
    [SerializeField] TMP_Text scaleLabel;
    [SerializeField] Color inactiveColor = Color.white;
    [SerializeField] Color activeColor = new Color(1f, 0.9f, 0.1f, 1f);
    [SerializeField] TextMeshProUGUI statusText;

    [Header("Input Actions")]
    [SerializeField] InputActionReference translateAction;
    [SerializeField] InputActionReference rotateAction;
    [FormerlySerializedAs("moveAction")]
    [SerializeField] InputActionReference scaleAction;
    [SerializeField] InputActionReference cancelAction;

    Mode mode = Mode.None;
    VRModelingInteractable activeInteractable;
    Transform activeTarget;
    VRTranslationGizmo translationGizmo;
    VRRotationGizmo rotationGizmo;
    VRScaleGizmo scaleGizmo;
    bool manipulating;

    public bool IsTransforming => mode != Mode.None && manipulating;
    public bool IsManipulatingGizmo =>
        (translationGizmo != null && translationGizmo.IsDragging) ||
        (rotationGizmo != null && rotationGizmo.IsDragging) ||
        (scaleGizmo != null && scaleGizmo.IsDragging);
    public bool IsTranslationGizmoVisible => mode == Mode.Translate && activeTarget != null;
    public bool IsRotationGizmoVisible => mode == Mode.Rotate && activeTarget != null;
    public bool IsScaleGizmoVisible => mode == Mode.Scale && activeTarget != null;

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        translateAction?.action.Enable();
        rotateAction?.action.Enable();
        scaleAction?.action.Enable();
        cancelAction?.action.Enable();
        RefreshUi();
    }

    void OnDisable()
    {
        if (activeInteractable != null)
            activeInteractable.ApplyMode(VRModelingInteractable.ToolMode.Free, VRModelingInteractable.Axis.Free);

        translateAction?.action.Disable();
        rotateAction?.action.Disable();
        scaleAction?.action.Disable();
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
        if (translateAction != null && translateAction.action.WasPressedThisFrame())
            SetMode(mode == Mode.Translate ? Mode.None : Mode.Translate);
        else if (rotateAction != null && rotateAction.action.WasPressedThisFrame())
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
        RefreshTranslationGizmo();
        RefreshRotationGizmo();
        RefreshScaleGizmo();
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
        RefreshTranslationGizmo();
        RefreshRotationGizmo();
        RefreshScaleGizmo();
    }

    void ApplyModeToActiveInteractable()
    {
        if (activeInteractable == null)
            return;

        var nativeMode = mode switch
        {
            Mode.Translate => VRModelingInteractable.ToolMode.Translate,
            Mode.Rotate => VRModelingInteractable.ToolMode.Rotate,
            Mode.Scale => VRModelingInteractable.ToolMode.Scale,
            _ => VRModelingInteractable.ToolMode.Free,
        };

        activeInteractable.ApplyMode(nativeMode, VRModelingInteractable.Axis.Free);
    }

    void UpdateManipulation()
    {
        if (mode == Mode.Translate)
        {
            manipulating = translationGizmo != null && translationGizmo.IsDragging;
            return;
        }

        if (mode == Mode.Rotate)
        {
            manipulating = rotationGizmo != null && rotationGizmo.IsDragging;
            return;
        }

        if (mode == Mode.Scale)
        {
            manipulating = scaleGizmo != null && scaleGizmo.IsDragging;
            return;
        }

        manipulating = false;
    }

    void RefreshTranslationGizmo()
    {
        if (translationGizmo == null)
        {
            var gizmoObject = new GameObject("VR Translation Gizmo");
            translationGizmo = gizmoObject.AddComponent<VRTranslationGizmo>();
        }

        translationGizmo.SetTarget(mode == Mode.Translate ? activeTarget : null);
    }

    void RefreshRotationGizmo()
    {
        if (rotationGizmo == null)
        {
            var gizmoObject = new GameObject("VR Rotation Gizmo");
            rotationGizmo = gizmoObject.AddComponent<VRRotationGizmo>();
        }

        rotationGizmo.SetTarget(mode == Mode.Rotate ? activeTarget : null);
    }

    void RefreshScaleGizmo()
    {
        if (scaleGizmo == null)
        {
            var gizmoObject = new GameObject("VR Scale Gizmo");
            scaleGizmo = gizmoObject.AddComponent<VRScaleGizmo>();
        }

        scaleGizmo.SetTarget(mode == Mode.Scale ? activeTarget : null);
    }

    void RefreshUi()
    {
        if (translateLabel != null)
            translateLabel.color = mode == Mode.Translate ? activeColor : inactiveColor;

        if (rotateLabel != null)
            rotateLabel.color = mode == Mode.Rotate ? activeColor : inactiveColor;

        if (scaleLabel != null)
            scaleLabel.color = mode == Mode.Scale ? activeColor : inactiveColor;

        if (statusText == null)
            return;

        statusText.text = mode switch
        {
            Mode.Translate => "Translate mode: grab a colored arrow handle",
            Mode.Rotate => "Rotate mode: grab a colored ring",
            Mode.Scale => "Scale mode: grab a colored arrow handle",
            _ => ""
        };
    }
}

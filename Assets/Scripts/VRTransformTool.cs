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

    public enum ToolMode
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

    ToolMode mode = ToolMode.None;
    VRModelingInteractable activeInteractable;
    Transform activeTarget;
    VRTranslationGizmo translationGizmo;
    VRRotationGizmo rotationGizmo;
    VRScaleGizmo scaleGizmo;
    bool manipulating;

    public ToolMode ActiveMode => mode;
    public bool IsTransforming => mode != ToolMode.None && manipulating;
    public bool IsManipulatingGizmo =>
        (translationGizmo != null && translationGizmo.IsDragging) ||
        (rotationGizmo != null && rotationGizmo.IsDragging) ||
        (scaleGizmo != null && scaleGizmo.IsDragging);
    public bool IsTranslationGizmoVisible => mode == ToolMode.Translate && activeTarget != null;
    public bool IsRotationGizmoVisible => mode == ToolMode.Rotate && activeTarget != null;
    public bool IsScaleGizmoVisible => mode == ToolMode.Scale && activeTarget != null;
    public bool IsTranslateModeActive => mode == ToolMode.Translate;
    public bool IsRotateModeActive => mode == ToolMode.Rotate;
    public bool IsScaleModeActive => mode == ToolMode.Scale;

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

    public void ToggleTranslateMode()
    {
        SetMode(mode == ToolMode.Translate ? ToolMode.None : ToolMode.Translate);
    }

    public void ToggleRotateMode()
    {
        SetMode(mode == ToolMode.Rotate ? ToolMode.None : ToolMode.Rotate);
    }

    public void ToggleScaleMode()
    {
        SetMode(mode == ToolMode.Scale ? ToolMode.None : ToolMode.Scale);
    }

    public void ClearMode()
    {
        SetMode(ToolMode.None);
    }

    public void SetTranslateMode()
    {
        SetMode(ToolMode.Translate);
    }

    public void SetRotateMode()
    {
        SetMode(ToolMode.Rotate);
    }

    public void SetScaleMode()
    {
        SetMode(ToolMode.Scale);
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
            ToggleTranslateMode();
        else if (rotateAction != null && rotateAction.action.WasPressedThisFrame())
            ToggleRotateMode();
        else if (scaleAction != null && scaleAction.action.WasPressedThisFrame())
            ToggleScaleMode();
        else if (cancelAction != null && cancelAction.action.WasPressedThisFrame())
            ClearMode();
    }

    void SetMode(ToolMode newMode)
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
            ToolMode.Translate => VRModelingInteractable.ToolMode.Translate,
            ToolMode.Rotate => VRModelingInteractable.ToolMode.Rotate,
            ToolMode.Scale => VRModelingInteractable.ToolMode.Scale,
            _ => VRModelingInteractable.ToolMode.Free,
        };

        activeInteractable.ApplyMode(nativeMode, VRModelingInteractable.Axis.Free);
    }

    void UpdateManipulation()
    {
        if (mode == ToolMode.Translate)
        {
            manipulating = translationGizmo != null && translationGizmo.IsDragging;
            return;
        }

        if (mode == ToolMode.Rotate)
        {
            manipulating = rotationGizmo != null && rotationGizmo.IsDragging;
            return;
        }

        if (mode == ToolMode.Scale)
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

        translationGizmo.SetTarget(mode == ToolMode.Translate ? activeTarget : null);
    }

    void RefreshRotationGizmo()
    {
        if (rotationGizmo == null)
        {
            var gizmoObject = new GameObject("VR Rotation Gizmo");
            rotationGizmo = gizmoObject.AddComponent<VRRotationGizmo>();
        }

        rotationGizmo.SetTarget(mode == ToolMode.Rotate ? activeTarget : null);
    }

    void RefreshScaleGizmo()
    {
        if (scaleGizmo == null)
        {
            var gizmoObject = new GameObject("VR Scale Gizmo");
            scaleGizmo = gizmoObject.AddComponent<VRScaleGizmo>();
        }

        scaleGizmo.SetTarget(mode == ToolMode.Scale ? activeTarget : null);
    }

    void RefreshUi()
    {
        if (translateLabel != null)
            translateLabel.color = mode == ToolMode.Translate ? activeColor : inactiveColor;

        if (rotateLabel != null)
            rotateLabel.color = mode == ToolMode.Rotate ? activeColor : inactiveColor;

        if (scaleLabel != null)
            scaleLabel.color = mode == ToolMode.Scale ? activeColor : inactiveColor;

        if (statusText == null)
            return;

        statusText.text = mode switch
        {
            ToolMode.Translate => "Translate mode: grab a colored arrow handle",
            ToolMode.Rotate => "Rotate mode: grab a colored ring",
            ToolMode.Scale => "Scale mode: grab a colored arrow handle",
            _ => ""
        };
    }
}

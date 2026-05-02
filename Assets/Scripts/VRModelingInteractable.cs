using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

/// <summary>
/// Configures a selectable mesh as a low-latency XRI modeling object.
/// Native XRI handles selection and translation; explicit tool modes can take over rotate/scale.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SelectableObject))]
public class VRModelingInteractable : MonoBehaviour
{
    public enum ToolMode
    {
        Free,
        Translate,
        Rotate,
        Scale
    }

    public enum Axis
    {
        Free,
        X,
        Y,
        Z
    }

    XRGrabInteractable grabInteractable;
    XRGeneralGrabTransformer grabTransformer;
    VRPrimaryHandSelectFilter primaryHandFilter;
    Rigidbody body;
    Collider[] rayBlockingColliders;
    bool[] originalColliderEnabledStates;
    ToolMode currentMode = ToolMode.Free;
    Axis currentAxis = Axis.Free;
    bool grabInteractableEnabledByTool = true;
    bool rayBlockingSuppressed;

    public XRGrabInteractable GrabInteractable => grabInteractable;

    void Awake()
    {
        Configure();
    }

    void OnEnable()
    {
        if (grabInteractable == null)
            Configure();

        grabInteractable.selectEntered.AddListener(OnGrabSelectionChanged);
        grabInteractable.selectExited.AddListener(OnGrabSelectionChanged);
        RefreshGrabBehavior();
    }

    void OnDisable()
    {
        if (grabInteractable == null)
            return;

        SetRayBlockingSuppressed(false);
        grabInteractable.selectEntered.RemoveListener(OnGrabSelectionChanged);
        grabInteractable.selectExited.RemoveListener(OnGrabSelectionChanged);
    }

    public void Configure()
    {
        body = GetComponent<Rigidbody>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();

        body.useGravity = false;
        body.isKinematic = true;

        grabInteractable = GetComponent<XRGrabInteractable>();
        if (grabInteractable == null)
            grabInteractable = gameObject.AddComponent<XRGrabInteractable>();

        grabTransformer = GetComponent<XRGeneralGrabTransformer>();
        if (grabTransformer == null)
            grabTransformer = gameObject.AddComponent<XRGeneralGrabTransformer>();

        primaryHandFilter = GetComponent<VRPrimaryHandSelectFilter>();
        if (primaryHandFilter == null)
            primaryHandFilter = gameObject.AddComponent<VRPrimaryHandSelectFilter>();

        CacheRayBlockingColliders();

        grabInteractable.selectMode = InteractableSelectMode.Multiple;
        grabInteractable.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grabInteractable.throwOnDetach = false;
        grabInteractable.useDynamicAttach = true;
        grabInteractable.matchAttachPosition = true;
        grabInteractable.matchAttachRotation = false;
        grabInteractable.reinitializeDynamicAttachEverySingleGrab = true;
        grabInteractable.trackPosition = true;
        grabInteractable.trackRotation = false;
        grabInteractable.trackScale = true;

        grabTransformer.allowOneHandedScaling = false;
        grabTransformer.allowTwoHandedScaling = true;
        grabTransformer.allowTwoHandedRotation = XRGeneralGrabTransformer.TwoHandedRotationMode.TwoHandedAverage;
        grabTransformer.thresholdMoveRatioForScale = 0f;
        grabTransformer.minimumScaleRatio = 0.05f;
        grabTransformer.maximumScaleRatio = 20f;
        grabTransformer.scaleMultiplier = 1f;
        grabTransformer.constrainedAxisDisplacementMode = XRGeneralGrabTransformer.ConstrainedAxisDisplacementMode.WorldAxisRelative;

        EnsureSelectFilter(primaryHandFilter);

        ApplyMode(currentMode, currentAxis);
    }

    public void ApplyMode(ToolMode mode, Axis axis)
    {
        if (grabInteractable == null || grabTransformer == null)
            Configure();

        currentMode = mode;
        currentAxis = axis;

        switch (mode)
        {
            case ToolMode.Translate:
                SetGrabInteractableEnabled(false);
                SetRayBlockingSuppressed(true);
                grabInteractable.trackPosition = false;
                grabInteractable.trackRotation = false;
                grabInteractable.trackScale = false;
                grabTransformer.permittedDisplacementAxes = 0;
                break;

            case ToolMode.Rotate:
                SetGrabInteractableEnabled(false);
                SetRayBlockingSuppressed(true);
                grabInteractable.trackPosition = true;
                grabInteractable.trackRotation = false;
                grabInteractable.trackScale = false;
                grabTransformer.permittedDisplacementAxes = 0;
                break;

            case ToolMode.Scale:
                SetGrabInteractableEnabled(false);
                SetRayBlockingSuppressed(true);
                grabInteractable.trackPosition = false;
                grabInteractable.trackRotation = false;
                grabInteractable.trackScale = false;
                grabTransformer.permittedDisplacementAxes = 0;
                break;

            default:
                SetRayBlockingSuppressed(false);
                SetGrabInteractableEnabled(true);
                grabInteractable.trackPosition = true;
                grabInteractable.trackRotation = false;
                grabInteractable.trackScale = true;
                grabTransformer.permittedDisplacementAxes = XRGeneralGrabTransformer.ManipulationAxes.All;
                break;
        }

        RefreshGrabBehavior();
    }

    void CacheRayBlockingColliders()
    {
        rayBlockingColliders = GetComponentsInChildren<Collider>(true);
        originalColliderEnabledStates = new bool[rayBlockingColliders.Length];

        for (int i = 0; i < rayBlockingColliders.Length; i++)
            originalColliderEnabledStates[i] = rayBlockingColliders[i] != null && rayBlockingColliders[i].enabled;
    }

    void SetRayBlockingSuppressed(bool suppressed)
    {
        if (rayBlockingSuppressed == suppressed)
            return;

        if (rayBlockingColliders == null || originalColliderEnabledStates == null)
            CacheRayBlockingColliders();

        rayBlockingSuppressed = suppressed;

        for (int i = 0; i < rayBlockingColliders.Length; i++)
        {
            var collider = rayBlockingColliders[i];
            if (collider == null)
                continue;

            collider.enabled = suppressed ? false : originalColliderEnabledStates[i];
        }
    }

    void SetGrabInteractableEnabled(bool enabled)
    {
        if (grabInteractable == null || grabInteractableEnabledByTool == enabled)
            return;

        grabInteractableEnabledByTool = enabled;
        grabInteractable.enabled = enabled;
    }

    static XRGeneralGrabTransformer.ManipulationAxes ToManipulationAxes(Axis axis)
    {
        return axis switch
        {
            Axis.X => XRGeneralGrabTransformer.ManipulationAxes.X,
            Axis.Y => XRGeneralGrabTransformer.ManipulationAxes.Y,
            Axis.Z => XRGeneralGrabTransformer.ManipulationAxes.Z,
            _ => XRGeneralGrabTransformer.ManipulationAxes.All,
        };
    }

    void EnsureSelectFilter(IXRSelectFilter filter)
    {
        if (filter == null)
            return;

        var filters = grabInteractable.selectFilters;
        for (int i = 0; i < filters.count; i++)
        {
            if (ReferenceEquals(filters.GetAt(i), filter))
                return;
        }

        filters.Add(filter);
    }

    void OnGrabSelectionChanged(BaseInteractionEventArgs args)
    {
        RefreshGrabBehavior();
    }

    void RefreshGrabBehavior()
    {
        if (grabInteractable == null)
            return;

        grabInteractable.matchAttachRotation = false;
        grabInteractable.trackRotation = false;
    }
}

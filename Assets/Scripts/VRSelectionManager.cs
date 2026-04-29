using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Selection bridge between the project marker component and XR Interaction Toolkit.
/// XRI handles hover/select/grab; this class keeps the current modeling target and outline in sync.
/// </summary>
public class VRSelectionManager : MonoBehaviour
{
    public static VRSelectionManager Instance { get; private set; }

    [Header("Right Hand Selection")]
    [SerializeField] Transform rightControllerRoot;
    [SerializeField] InputActionReference selectAction;

    readonly List<VRModelingInteractable> registeredInteractables = new();

    SelectableObject selected;
    ObjectOutline selectedOutline;
    XRRayInteractor rightRayInteractor;

    public SelectableObject Selected => selected;
    public XRRayInteractor RightRayInteractor => rightRayInteractor;
    public System.Action<SelectableObject> SelectionChanged;
    public bool IsSelectionGrabbed =>
        selected != null &&
        selected.TryGetComponent<VRModelingInteractable>(out var modelingInteractable) &&
        modelingInteractable.GrabInteractable != null &&
        modelingInteractable.GrabInteractable.interactorsSelecting.Count > 0;

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        RegisterSceneInteractables();
        ResolveRightRayInteractor();

        if (selectAction != null)
        {
            selectAction.action.Enable();
            selectAction.action.performed += OnSelectActionPerformed;
        }
    }

    void OnDisable()
    {
        if (selectAction != null)
        {
            selectAction.action.performed -= OnSelectActionPerformed;
            selectAction.action.Disable();
        }

        foreach (var interactable in registeredInteractables)
        {
            if (interactable == null || interactable.GrabInteractable == null)
                continue;

            interactable.GrabInteractable.selectEntered.RemoveListener(OnXriSelectEntered);
        }

        registeredInteractables.Clear();
    }

    public void RegisterSceneInteractables()
    {
        var selectableObjects = FindObjectsByType<SelectableObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var selectableObject in selectableObjects)
            Register(selectableObject);
    }

    public void Register(SelectableObject selectableObject)
    {
        if (selectableObject == null)
            return;

        var modelingInteractable = selectableObject.GetComponent<VRModelingInteractable>();
        if (modelingInteractable == null)
            modelingInteractable = selectableObject.gameObject.AddComponent<VRModelingInteractable>();
        else
            modelingInteractable.Configure();

        if (registeredInteractables.Contains(modelingInteractable))
            return;

        registeredInteractables.Add(modelingInteractable);
        modelingInteractable.GrabInteractable.selectEntered.AddListener(OnXriSelectEntered);
    }

    void ResolveRightRayInteractor()
    {
        if (rightRayInteractor != null)
            return;

        if (rightControllerRoot != null)
            rightRayInteractor = rightControllerRoot.GetComponentInChildren<XRRayInteractor>(true);

        if (rightRayInteractor == null)
            rightRayInteractor = FindFirstObjectByType<XRRayInteractor>(FindObjectsInactive.Exclude);

        ConfigureRayInteractors();
    }

    void ConfigureRayInteractors()
    {
        var rayInteractors = FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var rayInteractor in rayInteractors)
        {
            if (rayInteractor == null)
                continue;

            rayInteractor.manipulateAttachTransform = false;
            rayInteractor.useForceGrab = false;
        }
    }

    void OnXriSelectEntered(SelectEnterEventArgs args)
    {
        var selectableObject = args.interactableObject.transform.GetComponentInParent<SelectableObject>();
        Select(selectableObject);
    }

    void OnSelectActionPerformed(InputAction.CallbackContext context)
    {
        if (selected == null || IsSelectionGrabbed)
            return;

        ResolveRightRayInteractor();

        if (rightRayInteractor == null)
            return;

        if (!rightRayInteractor.TryGetCurrent3DRaycastHit(out var hit))
        {
            Deselect();
            return;
        }

        var hitSelectable = hit.collider != null ? hit.collider.GetComponentInParent<SelectableObject>() : null;
        if (hitSelectable == null)
            Deselect();
    }

    public void Select(SelectableObject target)
    {
        if (target == null || selected == target)
            return;

        Deselect();
        Register(target);

        selected = target;
        selectedOutline = target.GetComponent<ObjectOutline>();
        if (selectedOutline == null)
            selectedOutline = target.gameObject.AddComponent<ObjectOutline>();

        selectedOutline.SetSelected(true);
        SelectionChanged?.Invoke(selected);
    }

    public void Deselect()
    {
        if (selectedOutline != null)
            selectedOutline.SetSelected(false);

        selectedOutline = null;
        selected = null;
        SelectionChanged?.Invoke(null);
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Ray-based object selection from the right controller.
/// Right trigger (pressed) = select object under ray / deselect on miss.
/// Drives VRTransformTool target and VRViewportCamera pivot — drop-in VR
/// equivalent of SelectionManager.cs.
/// </summary>
public class VRSelectionManager : MonoBehaviour
{
    public static VRSelectionManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("Right Controller transform — assign the Right Controller child of XR Origin.")]
    [SerializeField] Transform rayOrigin;

    [Header("Input Actions")]
    [Tooltip("Right trigger pressed — XRI RightHand/Select")]
    [SerializeField] InputActionReference selectAction;

    [Header("Settings")]
    [SerializeField] float     maxRayDistance = 100f;
    [SerializeField] LayerMask raycastMask    = ~0;

    SelectableObject _selected;
    ObjectOutline    _outline;

    void Awake() => Instance = this;

    void OnEnable()
    {
        if (selectAction == null) return;
        selectAction.action.Enable();
        selectAction.action.performed += OnSelectPerformed;
    }

    void OnDisable()
    {
        if (selectAction == null) return;
        selectAction.action.performed -= OnSelectPerformed;
        selectAction.action.Disable();
    }

    void OnSelectPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        if (VRTransformTool.Instance != null && VRTransformTool.Instance.IsTransforming) return;
        TrySelect();
    }

    void Update() { }

    void TrySelect()
    {
        if (rayOrigin == null) return;

        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, raycastMask))
        {
            var selectable = hit.collider.GetComponent<SelectableObject>();
            if (selectable != null)
            {
                Select(selectable);
                return;
            }
        }

        Deselect();
    }

    void Select(SelectableObject target)
    {
        if (_selected == target) return;

        Deselect();
        _selected = target;

        _outline = target.GetComponent<ObjectOutline>();
        if (_outline == null)
            _outline = target.gameObject.AddComponent<ObjectOutline>();
        _outline.SetSelected(true);

        VRTransformTool.Instance?.SetTarget(target.transform);
        VRViewportCamera.Instance?.SetPivot(target.transform.position);
    }

    void Deselect()
    {
        if (_selected == null) return;

        _outline?.SetSelected(false);
        _outline = null;

        VRTransformTool.Instance?.SetTarget(null);
        _selected = null;
    }
}

using UnityEngine;

/// <summary>
/// Left-click to select any GameObject that has a SelectableObject component.
/// Click empty space to deselect.
/// Drives ObjectOutline and ViewportCamera pivot.
/// </summary>
[RequireComponent(typeof(Camera))]
public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    Camera _cam;
    SelectableObject _selected;
    ObjectOutline    _outline;

    void Awake()
    {
        Instance = this;
        _cam     = GetComponent<Camera>();
    }

    void Update()
    {
        // Don't grab while a transform operation is in progress.
        if (TransformTool.Instance != null && TransformTool.Instance.IsTransforming) return;

        if (Input.GetMouseButtonDown(0))
            TrySelect();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    void TrySelect()
    {
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
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

        if (TransformTool.Instance != null)
            TransformTool.Instance.SetTarget(target.transform);

        if (ViewportCamera.Instance != null)
            ViewportCamera.Instance.SetPivot(target.transform.position);
    }

    void Deselect()
    {
        if (_selected == null) return;

        if (_outline != null)
        {
            _outline.SetSelected(false);
            _outline = null;
        }

        if (TransformTool.Instance != null)
            TransformTool.Instance.SetTarget(null);

        _selected = null;
    }
}

using UnityEngine;

/// <summary>
/// Manages the Blender-style orange outline on a selected object.
/// Creates a child GameObject that renders the mesh with SelectionOutline.shader
/// (normal-extrusion, Cull Front) so only the silhouette border is visible.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class ObjectOutline : MonoBehaviour
{
    [SerializeField] Color  outlineColor = new Color(1f, 0.5f, 0f, 1f);
    [SerializeField] float  outlineWidth = 0.025f;

    GameObject _outlineGO;

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetSelected(bool selected)
    {
        if (selected) CreateOutline();
        else          DestroyOutline();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    void CreateOutline()
    {
        if (_outlineGO != null) return;

        Shader outlineShader = Shader.Find("Custom/SelectionOutline");
        if (outlineShader == null)
        {
            Debug.LogWarning("ObjectOutline: 'Custom/SelectionOutline' shader not found.");
            return;
        }

        var mat = new Material(outlineShader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        mat.SetColor("_OutlineColor", outlineColor);
        mat.SetFloat("_OutlineWidth", outlineWidth);

        _outlineGO = new GameObject("__Outline__") { hideFlags = HideFlags.HideAndDontSave };
        _outlineGO.transform.SetParent(transform, false);
        _outlineGO.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        _outlineGO.transform.localScale = Vector3.one;

        var mf       = _outlineGO.AddComponent<MeshFilter>();
        mf.sharedMesh = GetComponent<MeshFilter>().sharedMesh;

        var mr        = _outlineGO.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows     = false;
    }

    void DestroyOutline()
    {
        if (_outlineGO == null) return;

        // Destroy the temp material too.
        var mr = _outlineGO.GetComponent<MeshRenderer>();
        if (mr != null) DestroyImmediate(mr.sharedMaterial);

        DestroyImmediate(_outlineGO);
        _outlineGO = null;
    }

    void OnDestroy() => DestroyOutline();
}

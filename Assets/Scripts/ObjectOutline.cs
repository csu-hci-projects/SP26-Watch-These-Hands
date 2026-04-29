using UnityEngine;

/// <summary>
/// Reusable inverted-hull outline for selected objects.
/// The outline child stays alive and only toggles visibility, avoiding material churn.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class ObjectOutline : MonoBehaviour
{
    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    [SerializeField] Color outlineColor = new Color(1f, 0.55f, 0.05f, 1f);
    [SerializeField] float outlineWidth = 0.03f;

    GameObject outlineObject;
    MeshRenderer outlineRenderer;
    MaterialPropertyBlock propertyBlock;

    public void SetSelected(bool selected)
    {
        EnsureOutline();

        if (outlineObject != null)
            outlineObject.SetActive(selected);
    }

    void EnsureOutline()
    {
        if (outlineObject != null)
        {
            ApplyProperties();
            return;
        }

        var sourceFilter = GetComponent<MeshFilter>();
        if (sourceFilter == null || sourceFilter.sharedMesh == null)
            return;

        var outlineShader = Shader.Find("Custom/SelectionOutline");
        if (outlineShader == null)
        {
            Debug.LogWarning("ObjectOutline: Custom/SelectionOutline shader not found.", this);
            return;
        }

        outlineObject = new GameObject("__SelectionOutline__");
        outlineObject.transform.SetParent(transform, false);
        outlineObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        outlineObject.transform.localScale = Vector3.one;
        outlineObject.SetActive(false);

        var outlineFilter = outlineObject.AddComponent<MeshFilter>();
        outlineFilter.sharedMesh = sourceFilter.sharedMesh;

        outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
        outlineRenderer.sharedMaterial = new Material(outlineShader)
        {
            name = $"{name} Selection Outline"
        };
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
        outlineRenderer.allowOcclusionWhenDynamic = false;

        ApplyProperties();
    }

    void ApplyProperties()
    {
        if (outlineRenderer == null)
            return;

        propertyBlock ??= new MaterialPropertyBlock();
        outlineRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(OutlineColorId, outlineColor);
        propertyBlock.SetFloat(OutlineWidthId, outlineWidth);
        outlineRenderer.SetPropertyBlock(propertyBlock);
    }

    void OnValidate()
    {
        outlineWidth = Mathf.Max(0f, outlineWidth);
        ApplyProperties();
    }

    void OnDestroy()
    {
        if (outlineRenderer != null)
            Destroy(outlineRenderer.sharedMaterial);
    }
}

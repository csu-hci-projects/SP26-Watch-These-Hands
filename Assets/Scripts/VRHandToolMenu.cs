using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hand-modality bridge for the left hand tool menu.
/// UI Buttons call these methods directly; controller input can keep using BlenderVRActions.
/// </summary>
public class VRHandToolMenu : MonoBehaviour
{
    [SerializeField] VRTransformTool transformTool;

    [Header("Optional Button Text")]
    [SerializeField] TMP_Text translateLabel;
    [SerializeField] TMP_Text rotateLabel;
    [SerializeField] TMP_Text scaleLabel;

    [Header("Optional Button Graphics")]
    [SerializeField] Graphic translateGraphic;
    [SerializeField] Graphic rotateGraphic;
    [SerializeField] Graphic scaleGraphic;

    [Header("Colors")]
    [SerializeField] Color inactiveTextColor = Color.white;
    [SerializeField] Color activeTextColor = new Color(1f, 0.9f, 0.1f, 1f);
    [SerializeField] Color inactiveGraphicColor = new Color(1f, 1f, 1f, 0.18f);
    [SerializeField] Color activeGraphicColor = new Color(1f, 0.9f, 0.1f, 0.35f);

    void Awake()
    {
        if (transformTool == null)
            transformTool = VRTransformTool.Instance;
    }

    void OnEnable()
    {
        Refresh();
    }

    void LateUpdate()
    {
        Refresh();
    }

    public void ToggleTranslate()
    {
        ResolveTransformTool();
        transformTool?.ToggleTranslateMode();
        Refresh();
    }

    public void ToggleRotate()
    {
        ResolveTransformTool();
        transformTool?.ToggleRotateMode();
        Refresh();
    }

    public void ToggleScale()
    {
        ResolveTransformTool();
        transformTool?.ToggleScaleMode();
        Refresh();
    }

    public void ClearMode()
    {
        ResolveTransformTool();
        transformTool?.ClearMode();
        Refresh();
    }

    void ResolveTransformTool()
    {
        if (transformTool == null)
            transformTool = VRTransformTool.Instance;
    }

    void Refresh()
    {
        ResolveTransformTool();

        var mode = transformTool != null ? transformTool.ActiveMode : VRTransformTool.ToolMode.None;
        SetState(translateLabel, translateGraphic, mode == VRTransformTool.ToolMode.Translate);
        SetState(rotateLabel, rotateGraphic, mode == VRTransformTool.ToolMode.Rotate);
        SetState(scaleLabel, scaleGraphic, mode == VRTransformTool.ToolMode.Scale);
    }

    void SetState(TMP_Text label, Graphic graphic, bool active)
    {
        if (label != null)
            label.color = active ? activeTextColor : inactiveTextColor;

        if (graphic != null)
            graphic.color = active ? activeGraphicColor : inactiveGraphicColor;
    }
}

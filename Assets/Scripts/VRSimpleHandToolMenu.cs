using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Small custom left-hand tool menu for hand mode.
/// Creates its own world-space UI and follows a hand anchor without using XRI HandMenu.
/// </summary>
public class VRSimpleHandToolMenu : MonoBehaviour
{
    public enum FollowMode
    {
        HandAnchor,
        View,
        RigLocal
    }

    [Header("Follow")]
    [SerializeField] FollowMode followMode = FollowMode.HandAnchor;
    [SerializeField] Transform followAnchor;
    [SerializeField] Camera viewCamera;
    [SerializeField] Vector3 handLocalOffset = new Vector3(-0.09f, -0.035f, 0.08f);
    [SerializeField] Vector3 viewLocalOffset = new Vector3(-0.28f, -0.18f, 0.55f);
    [SerializeField] Vector3 rigLocalOffset = new Vector3(-0.28f, 1.2f, 0.55f);
    [SerializeField] bool faceCamera = true;
    [SerializeField] float followSmoothing = 18f;
    [SerializeField] Vector2 menuSize = new Vector2(180f, 250f);
    [SerializeField] float canvasScale = 0.001f;
    [SerializeField] bool buildMenuAtRuntime;
    [SerializeField] bool hideUnlessTrackedHand = true;

    [Header("Tool")]
    [SerializeField] VRTransformTool transformTool;

    [Header("Colors")]
    [SerializeField] Color panelColor = new Color(0.02f, 0.025f, 0.03f, 0.88f);
    [SerializeField] Color inactiveButtonColor = new Color(0.18f, 0.2f, 0.23f, 0.92f);
    [SerializeField] Color activeButtonColor = new Color(0.95f, 0.82f, 0.12f, 0.95f);
    [SerializeField] Color textColor = Color.white;
    [SerializeField] Color activeTextColor = Color.black;

    Canvas canvas;
    RectTransform panel;
    Button translateButton;
    Button rotateButton;
    Button scaleButton;
    Button clearButton;
    TMP_Text translateLabel;
    TMP_Text rotateLabel;
    TMP_Text scaleLabel;
    TMP_Text clearLabel;
    bool built;

    void Awake()
    {
        ResolveReferences();
        if (buildMenuAtRuntime)
            BuildMenu();
        SnapToAnchor();
        RefreshState();
    }

    void OnEnable()
    {
        if (buildMenuAtRuntime)
            BuildMenu();
        SnapToAnchor();
    }

    void LateUpdate()
    {
        ResolveReferences();
        ApplyVisibility();
        FollowAnchor();
        RefreshState();
    }

    public void SetFollowAnchor(Transform anchor)
    {
        followAnchor = anchor;
        SnapToAnchor();
    }

    public void ToggleTranslate()
    {
        ResolveReferences();
        transformTool?.ToggleTranslateMode();
        RefreshState();
    }

    public void ToggleRotate()
    {
        ResolveReferences();
        transformTool?.ToggleRotateMode();
        RefreshState();
    }

    public void ToggleScale()
    {
        ResolveReferences();
        transformTool?.ToggleScaleMode();
        RefreshState();
    }

    public void ClearMode()
    {
        ResolveReferences();
        transformTool?.ClearMode();
        RefreshState();
    }

    void ResolveReferences()
    {
        if (viewCamera == null)
            viewCamera = Camera.main;

        if (transformTool == null)
            transformTool = VRTransformTool.Instance;
    }

    void BuildMenu()
    {
        if (built)
            return;

        built = true;

        var canvasObject = new GameObject("Simple Hand Tool Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = viewCamera;

        var canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = GetCanvasSize();
        canvasRect.localScale = Vector3.one * canvasScale;

        var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panelObject.transform.SetParent(canvasObject.transform, false);
        panel = panelObject.GetComponent<RectTransform>();
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;

        var panelImage = panelObject.GetComponent<Image>();
        panelImage.color = panelColor;

        var layout = panelObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        translateButton = CreateButton("Translate", out translateLabel);
        rotateButton = CreateButton("Rotate", out rotateLabel);
        scaleButton = CreateButton("Scale", out scaleLabel);
        clearButton = CreateButton("Clear", out clearLabel);

        translateButton.onClick.AddListener(ToggleTranslate);
        rotateButton.onClick.AddListener(ToggleRotate);
        scaleButton.onClick.AddListener(ToggleScale);
        clearButton.onClick.AddListener(ClearMode);
    }

    void ApplyVisibility()
    {
        var shouldShow = !hideUnlessTrackedHand ||
            XRInputModalityManager.currentInputMode.Value == XRInputModalityManager.InputMode.TrackedHand;

        if (canvas != null)
            canvas.gameObject.SetActive(shouldShow);
        else
            SetChildrenActive(shouldShow);
    }

    Button CreateButton(string label, out TMP_Text labelText)
    {
        var buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(panel, false);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(150f, 45f);

        var layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 45f;

        var image = buttonObject.GetComponent<Image>();
        image.color = inactiveButtonColor;

        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);

        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        labelText = textObject.GetComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.color = textColor;
        labelText.fontSize = 18f;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.raycastTarget = false;

        return button;
    }

    Vector2 GetCanvasSize()
    {
        // Earlier versions of this script used meter-sized UI units. Treat those as meters
        // and convert them to normal canvas pixels so TextMeshPro and layout behave.
        if (menuSize.x < 10f && menuSize.y < 10f)
            return menuSize / Mathf.Max(canvasScale, 0.0001f);

        return menuSize;
    }

    void FollowAnchor()
    {
        if (followMode == FollowMode.HandAnchor && followAnchor == null)
            return;

        var targetPosition = GetTargetPosition();
        var t = 1f - Mathf.Exp(-followSmoothing * Time.unscaledDeltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, GetTargetRotation(), t);
    }

    void SnapToAnchor()
    {
        if (followMode == FollowMode.HandAnchor && followAnchor == null)
            return;

        transform.position = GetTargetPosition();
        transform.rotation = GetTargetRotation();
    }

    Vector3 GetTargetPosition()
    {
        if (followMode == FollowMode.View && viewCamera != null)
            return viewCamera.transform.TransformPoint(viewLocalOffset);

        if (followMode == FollowMode.RigLocal && transform.parent != null)
            return transform.parent.TransformPoint(rigLocalOffset);

        if (followAnchor != null)
            return followAnchor.TransformPoint(handLocalOffset);

        return transform.position;
    }

    Quaternion GetTargetRotation()
    {
        if (!faceCamera || viewCamera == null)
            return followAnchor != null ? followAnchor.rotation : transform.rotation;

        var toCamera = transform.position - viewCamera.transform.position;
        if (toCamera.sqrMagnitude < 0.0001f)
            return transform.rotation;

        return Quaternion.LookRotation(toCamera.normalized, viewCamera.transform.up);
    }

    void RefreshState()
    {
        ResolveReferences();
        var mode = transformTool != null ? transformTool.ActiveMode : VRTransformTool.ToolMode.None;

        SetButtonState(translateButton, translateLabel, mode == VRTransformTool.ToolMode.Translate);
        SetButtonState(rotateButton, rotateLabel, mode == VRTransformTool.ToolMode.Rotate);
        SetButtonState(scaleButton, scaleLabel, mode == VRTransformTool.ToolMode.Scale);
        SetButtonState(clearButton, clearLabel, mode == VRTransformTool.ToolMode.None);
    }

    void SetChildrenActive(bool active)
    {
        foreach (Transform child in transform)
        {
            if (child.gameObject.activeSelf != active)
                child.gameObject.SetActive(active);
        }
    }

    void SetButtonState(Button button, TMP_Text label, bool active)
    {
        if (button != null && button.targetGraphic is Image image)
            image.color = active ? activeButtonColor : inactiveButtonColor;

        if (label != null)
            label.color = active ? activeTextColor : textColor;
    }
}

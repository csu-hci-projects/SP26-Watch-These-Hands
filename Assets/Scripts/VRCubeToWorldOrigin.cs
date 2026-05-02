using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class VRCubeToWorldOrigin : MonoBehaviour
{
    public static VRCubeToWorldOrigin Instance { get; private set; }

    [Header("Target")]
    [SerializeField] Transform targetOverride;
    [SerializeField] string fallbackObjectName = "Cube";

    [Header("Input")]
    [SerializeField] InputActionReference moveToOriginAction;
    [SerializeField] float holdDuration = 0.75f;

    [Header("Optional UI")]
    [SerializeField] TMP_Text label;
    [SerializeField] Graphic graphic;
    [SerializeField] Color readyTextColor = Color.white;
    [SerializeField] Color pressedTextColor = Color.black;
    [SerializeField] Color readyGraphicColor = new Color(0.18f, 0.2f, 0.23f, 0.92f);
    [SerializeField] Color pressedGraphicColor = new Color(0.95f, 0.82f, 0.12f, 0.95f);
    [SerializeField] float pressedVisualDuration = 0.15f;

    float pressedVisualUntil;
    float holdStartedAt = -1f;
    bool holdTriggered;

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        moveToOriginAction?.action.Enable();
        RefreshUi();
    }

    void OnDisable()
    {
        moveToOriginAction?.action.Disable();
    }

    void Update()
    {
        UpdateHoldInput();

        RefreshUi();
    }

    public void MoveSelectedToWorldOrigin()
    {
        MoveTargetToWorldOrigin();
    }

    public void MoveSelectedToWorldOriginInstant()
    {
        MoveTargetToWorldOrigin();
    }

    void MoveTargetToWorldOrigin()
    {
        var target = ResolveTarget();
        if (target == null)
            return;

        target.position = Vector3.zero;
        pressedVisualUntil = Time.unscaledTime + pressedVisualDuration;
        holdStartedAt = -1f;
        holdTriggered = false;
        RefreshUi();
    }

    public void SetUi(TMP_Text newLabel, Graphic newGraphic)
    {
        label = newLabel;
        graphic = newGraphic;
        RefreshUi();
    }

    void RefreshUi()
    {
        bool pressed = Time.unscaledTime < pressedVisualUntil;
        float holdProgress = GetHoldProgress();

        if (label != null)
            label.color = Color.Lerp(readyTextColor, pressedTextColor, Mathf.Max(holdProgress, pressed ? 1f : 0f));

        if (graphic != null)
            graphic.color = Color.Lerp(readyGraphicColor, pressedGraphicColor, Mathf.Max(holdProgress, pressed ? 1f : 0f));
    }

    void UpdateHoldInput()
    {
        if (moveToOriginAction == null)
            return;

        var action = moveToOriginAction.action;

        if (action.WasPressedThisFrame())
        {
            holdStartedAt = Time.unscaledTime;
            holdTriggered = false;
        }

        if (action.IsPressed() && !holdTriggered && GetHoldProgress() >= 1f)
        {
            holdTriggered = true;
            MoveTargetToWorldOrigin();
        }

        if (action.WasReleasedThisFrame())
        {
            holdStartedAt = -1f;
            holdTriggered = false;
        }
    }

    float GetHoldProgress()
    {
        if (holdStartedAt < 0f || holdTriggered)
            return 0f;

        return Mathf.Clamp01((Time.unscaledTime - holdStartedAt) / Mathf.Max(0.01f, holdDuration));
    }

    Transform ResolveTarget()
    {
        if (VRSelectionManager.Instance != null && VRSelectionManager.Instance.Selected != null)
            return VRSelectionManager.Instance.Selected.transform;

        if (targetOverride != null)
            return targetOverride;

        if (!string.IsNullOrWhiteSpace(fallbackObjectName))
        {
            var namedObject = GameObject.Find(fallbackObjectName);
            if (namedObject != null)
                return namedObject.transform;
        }

        var selectable = FindFirstObjectByType<SelectableObject>(FindObjectsInactive.Exclude);
        return selectable != null ? selectable.transform : null;
    }
}

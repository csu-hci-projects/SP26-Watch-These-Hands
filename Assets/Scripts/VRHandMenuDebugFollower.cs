using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.UI.BodyUI;

/// <summary>
/// Debug helper for the XRI ButtonHandMenu prefab. It bypasses HandMenu visibility gates
/// and forces the wrist button/scroll view to follow a tracked hand anchor.
/// </summary>
public class VRHandMenuDebugFollower : MonoBehaviour
{
    [SerializeField] Transform handAnchor;
    [SerializeField] bool useLeftHandAnchor = true;
    [SerializeField] bool disableXriHandMenus = true;
    [SerializeField] bool showScrollView;
    [SerializeField] bool showDebugMarker = true;
    [SerializeField] bool useCameraRelativeOffset = true;
    [SerializeField] bool showDebugPanel = true;
    [SerializeField] Vector3 cameraRelativeOffset = new Vector3(-0.12f, 0.02f, 0.12f);
    [SerializeField] Vector3 localOffset = Vector3.zero;
    [SerializeField] Vector3 localEulerOffset = new Vector3(270f, 180f, 0f);
    [SerializeField] float visibleScale = 1f;

    Transform wristFollow;
    Transform scrollFollow;
    Transform debugMarker;
    Transform debugPanel;
    Camera mainCamera;
    bool loggedState;

    void Awake()
    {
        ResolveReferences();
    }

    void OnEnable()
    {
        ResolveReferences();
        ApplyDebugVisibility();
    }

    void LateUpdate()
    {
        ResolveReferences();

        if (handAnchor == null)
        {
            LogState();
            return;
        }

        ApplyDebugVisibility();
        Follow(wristFollow);

        if (showScrollView)
            Follow(scrollFollow);

        UpdateDebugMarker();
        UpdateDebugPanel();
        LogState();
    }

    void ResolveReferences()
    {
        if (handAnchor == null)
            handAnchor = FindDeepChild(transform, useLeftHandAnchor ? "Left Hand Tracked Anchor" : "Right Hand Tracked Anchor");

        if (wristFollow == null)
        {
            var wristButton = FindDeepChild(transform, "Hand Menu Wrist Button");
            wristFollow = wristButton != null ? FindDeepChild(wristButton, "Follow GameObject") : null;
        }

        if (scrollFollow == null)
        {
            var scrollView = FindDeepChild(transform, "Hand Menu ScrollView");
            scrollFollow = scrollView != null ? FindDeepChild(scrollView, "Follow GameObject") : null;
        }

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (showDebugMarker && debugMarker == null)
            CreateDebugMarker();

        if (showDebugPanel && debugPanel == null)
            CreateDebugPanel();
    }

    void ApplyDebugVisibility()
    {
        if (disableXriHandMenus)
        {
            foreach (var handMenu in GetComponentsInChildren<HandMenu>(true))
                handMenu.enabled = false;
        }

        SetObjectActive("Hand Menu Wrist Button", true);
        SetObjectActive("Hand Menu ScrollView", showScrollView);

        if (wristFollow != null)
        {
            wristFollow.gameObject.SetActive(true);
            wristFollow.localScale = Vector3.one * visibleScale;
        }

        if (scrollFollow != null)
        {
            scrollFollow.gameObject.SetActive(showScrollView);
            scrollFollow.localScale = Vector3.one * visibleScale;
        }
    }

    void Follow(Transform followTarget)
    {
        if (followTarget == null)
            return;

        followTarget.position = GetTargetPosition();
        followTarget.rotation = handAnchor.rotation * Quaternion.Euler(localEulerOffset);

        if (mainCamera == null)
            return;

        var toCamera = followTarget.position - mainCamera.transform.position;
        if (toCamera.sqrMagnitude > 0.0001f)
            followTarget.rotation = Quaternion.LookRotation(toCamera.normalized, mainCamera.transform.up);
    }

    void UpdateDebugMarker()
    {
        if (!showDebugMarker || debugMarker == null)
            return;

        debugMarker.gameObject.SetActive(handAnchor != null);
        debugMarker.position = GetTargetPosition();
    }

    void UpdateDebugPanel()
    {
        if (!showDebugPanel || debugPanel == null)
            return;

        debugPanel.gameObject.SetActive(handAnchor != null);
        debugPanel.position = GetTargetPosition();

        if (mainCamera == null)
            return;

        var toCamera = debugPanel.position - mainCamera.transform.position;
        if (toCamera.sqrMagnitude > 0.0001f)
            debugPanel.rotation = Quaternion.LookRotation(toCamera.normalized, mainCamera.transform.up);
    }

    Vector3 GetTargetPosition()
    {
        if (handAnchor == null)
            return transform.position;

        if (!useCameraRelativeOffset || mainCamera == null)
            return handAnchor.TransformPoint(localOffset);

        var cameraTransform = mainCamera.transform;
        return handAnchor.position
            + cameraTransform.right * cameraRelativeOffset.x
            + cameraTransform.up * cameraRelativeOffset.y
            + cameraTransform.forward * cameraRelativeOffset.z;
    }

    void CreateDebugMarker()
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "Hand Menu Debug Marker";
        marker.transform.SetParent(transform, false);
        marker.transform.localScale = Vector3.one * 0.035f;

        var collider = marker.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        var renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = Color.cyan;
        }

        debugMarker = marker.transform;
    }

    void CreateDebugPanel()
    {
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Hand Menu Debug Panel";
        panel.transform.SetParent(transform, false);
        panel.transform.localScale = new Vector3(0.16f, 0.08f, 0.006f);

        var collider = panel.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        var renderer = panel.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = new Color(0.02f, 0.9f, 1f, 1f);
        }

        debugPanel = panel.transform;
    }

    void LogState()
    {
        if (loggedState)
            return;

        loggedState = true;
        Debug.Log(
            $"VRHandMenuDebugFollower on {name}: " +
            $"handAnchor={(handAnchor != null ? handAnchor.name : "null")}, " +
            $"wristFollow={(wristFollow != null ? wristFollow.name : "null")}, " +
            $"scrollFollow={(scrollFollow != null ? scrollFollow.name : "null")}, " +
            $"camera={(mainCamera != null ? mainCamera.name : "null")}",
            this);
    }

    void SetObjectActive(string childName, bool active)
    {
        var child = FindDeepChild(transform, childName);
        if (child != null)
            child.gameObject.SetActive(active);
    }

    static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            var result = FindDeepChild(child, childName);
            if (result != null)
                return result;
        }

        return null;
    }
}

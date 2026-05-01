using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

/// <summary>
/// Keeps the ButtonHandMenu's visible UI hidden until XRI has entered tracked-hand mode.
/// This prevents the prefab's initial world-space UI from appearing at its scene root.
/// </summary>
public class VRHandMenuVisibilityGuard : MonoBehaviour
{
    [SerializeField] GameObject wristFollowObject;
    [SerializeField] GameObject scrollFollowObject;
    [SerializeField] bool requireTrackedHandMode = true;

    void Reset()
    {
        AutoAssign();
    }

    void Awake()
    {
        AutoAssign();
        Apply();
    }

    void LateUpdate()
    {
        Apply();
    }

    void AutoAssign()
    {
        if (wristFollowObject == null)
        {
            var wristButton = FindDeepChild(transform, "Hand Menu Wrist Button");
            var wristFollow = wristButton != null ? FindDeepChild(wristButton, "Follow GameObject") : null;
            wristFollowObject = wristFollow != null ? wristFollow.gameObject : null;
        }

        if (scrollFollowObject == null)
        {
            var scrollView = FindDeepChild(transform, "Hand Menu ScrollView");
            var scrollFollow = scrollView != null ? FindDeepChild(scrollView, "Follow GameObject") : null;
            scrollFollowObject = scrollFollow != null ? scrollFollow.gameObject : null;
        }
    }

    void Apply()
    {
        if (!requireTrackedHandMode)
            return;

        var handModeActive = XRInputModalityManager.currentInputMode.Value == XRInputModalityManager.InputMode.TrackedHand;
        if (handModeActive)
            return;

        if (wristFollowObject != null)
            wristFollowObject.SetActive(false);

        if (scrollFollowObject != null)
            scrollFollowObject.SetActive(false);
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

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.UI.BodyUI;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using System.Reflection;

/// <summary>
/// Temporary diagnostic for XRI ButtonHandMenu setup.
/// Logs input modality and whether the menu anchors/follow objects are moving.
/// </summary>
public class VRHandMenuStatusLogger : MonoBehaviour
{
    [SerializeField] Transform leftAnchor;
    [SerializeField] Transform rightAnchor;
    [SerializeField] Transform wristFollowObject;
    [SerializeField] Transform scrollFollowObject;
    [SerializeField] float logInterval = 1f;
    [SerializeField] bool logModalityManagers = true;

    float nextLogTime;

    static readonly FieldInfo LeftHandField = typeof(XRInputModalityManager).GetField("m_LeftHand", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo RightHandField = typeof(XRInputModalityManager).GetField("m_RightHand", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo LeftControllerField = typeof(XRInputModalityManager).GetField("m_LeftController", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo RightControllerField = typeof(XRInputModalityManager).GetField("m_RightController", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly PropertyInfo LeftInputModeProperty = typeof(XRInputModalityManager).GetProperty("leftInputMode", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly PropertyInfo RightInputModeProperty = typeof(XRInputModalityManager).GetProperty("rightInputMode", BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo HandMenuUiField = typeof(HandMenu).GetField("m_HandMenuUIGameObject", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo HandMenuHandednessField = typeof(HandMenu).GetField("m_MenuHandedness", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo HandMenuLeftPalmField = typeof(HandMenu).GetField("m_LeftPalmAnchor", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo HandMenuRightPalmField = typeof(HandMenu).GetField("m_RightPalmAnchor", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo HandMenuLeftShowingField = typeof(HandMenu).GetField("m_ShowMenuLeftHand", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo HandMenuRightShowingField = typeof(HandMenu).GetField("m_ShowMenuRightHand", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo HandMenuPresetField = typeof(HandMenu).GetField("m_HandTrackingFollowPreset", BindingFlags.Instance | BindingFlags.NonPublic);

    void Reset()
    {
        AutoAssign();
    }

    void Awake()
    {
        AutoAssign();
    }

    void Update()
    {
        if (Time.unscaledTime < nextLogTime)
            return;

        nextLogTime = Time.unscaledTime + logInterval;
        AutoAssign();

        Debug.Log(
            $"HandMenu status on {name}: mode={XRInputModalityManager.currentInputMode.Value}, " +
            $"leftAnchor={Describe(leftAnchor)}, rightAnchor={Describe(rightAnchor)}, " +
            $"wrist={Describe(wristFollowObject)}, scroll={Describe(scrollFollowObject)}",
            this);

        if (!logModalityManagers)
            return;

        var managers = FindObjectsByType<XRInputModalityManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (managers.Length == 0)
        {
            Debug.Log("XRInputModalityManager status: none found in loaded scene.", this);
            return;
        }

        foreach (var manager in managers)
        {
            Debug.Log(
                $"XRInputModalityManager on {GetHierarchyPath(manager.transform)}: " +
                $"active={manager.gameObject.activeInHierarchy}, enabled={manager.enabled}, " +
                $"leftMode={ReadProperty(LeftInputModeProperty, manager)}, rightMode={ReadProperty(RightInputModeProperty, manager)}, " +
                $"leftHand={Describe(ReadGameObject(LeftHandField, manager))}, rightHand={Describe(ReadGameObject(RightHandField, manager))}, " +
                $"leftController={Describe(ReadGameObject(LeftControllerField, manager))}, rightController={Describe(ReadGameObject(RightControllerField, manager))}",
                manager);
        }

        foreach (var handMenu in GetComponentsInChildren<HandMenu>(true))
        {
            var uiObject = HandMenuUiField?.GetValue(handMenu) as GameObject;
            var leftPalm = HandMenuLeftPalmField?.GetValue(handMenu) as Transform;
            var rightPalm = HandMenuRightPalmField?.GetValue(handMenu) as Transform;

            Debug.Log(
                $"HandMenu component on {GetHierarchyPath(handMenu.transform)}: " +
                $"enabled={handMenu.enabled}, componentObjectActive={handMenu.gameObject.activeInHierarchy}, " +
                $"uiObject={Describe(uiObject)}, handedness={ReadField(HandMenuHandednessField, handMenu)}, " +
                $"showLeft={ReadField(HandMenuLeftShowingField, handMenu)}, showRight={ReadField(HandMenuRightShowingField, handMenu)}, " +
                $"leftPalm={Describe(leftPalm)}, rightPalm={Describe(rightPalm)}, " +
                $"handPreset={DescribePreset(HandMenuPresetField?.GetValue(handMenu))}",
                handMenu);
        }
    }

    void AutoAssign()
    {
        if (leftAnchor == null)
            leftAnchor = FindDeepChild(transform, "Left Hand Tracked Anchor");

        if (rightAnchor == null)
            rightAnchor = FindDeepChild(transform, "Right Hand Tracked Anchor");

        if (wristFollowObject == null)
        {
            var wristButton = FindDeepChild(transform, "Hand Menu Wrist Button");
            wristFollowObject = wristButton != null ? FindDeepChild(wristButton, "Follow GameObject") : null;
        }

        if (scrollFollowObject == null)
        {
            var scrollView = FindDeepChild(transform, "Hand Menu ScrollView");
            scrollFollowObject = scrollView != null ? FindDeepChild(scrollView, "Follow GameObject") : null;
        }
    }

    static string Describe(Transform target)
    {
        if (target == null)
            return "null";

        var p = target.position;
        var active = target.gameObject.activeInHierarchy;
        return $"{target.name}@({p.x:F2},{p.y:F2},{p.z:F2}) active={active}";
    }

    static string Describe(GameObject target)
    {
        if (target == null)
            return "null";

        var p = target.transform.position;
        return $"{GetHierarchyPath(target.transform)}@({p.x:F2},{p.y:F2},{p.z:F2}) active={target.activeInHierarchy}";
    }

    static GameObject ReadGameObject(FieldInfo field, XRInputModalityManager manager)
    {
        return field?.GetValue(manager) as GameObject;
    }

    static object ReadProperty(PropertyInfo property, XRInputModalityManager manager)
    {
        return property != null ? property.GetValue(manager) : "unknown";
    }

    static object ReadField(FieldInfo field, object target)
    {
        return field != null ? field.GetValue(target) : "unknown";
    }

    static string DescribePreset(object datumProperty)
    {
        if (datumProperty == null)
            return "null";

        var valueProperty = datumProperty.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
        var value = valueProperty?.GetValue(datumProperty);
        if (value == null)
            return "null";

        var type = value.GetType();
        return
            $"requirePalmFacingUser={ReadMember(type, value, "requirePalmFacingUser")}, " +
            $"requirePalmFacingUp={ReadMember(type, value, "requirePalmFacingUp")}, " +
            $"palmAxis={ReadMember(type, value, "palmReferenceAxis")}, " +
            $"hideDelay={ReadMember(type, value, "hideDelaySeconds")}";
    }

    static object ReadMember(System.Type type, object target, string name)
    {
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
        if (field != null)
            return field.GetValue(target);

        var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        return property != null ? property.GetValue(target) : "unknown";
    }

    static string GetHierarchyPath(Transform target)
    {
        if (target == null)
            return "null";

        var path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = $"{target.name}/{path}";
        }

        return path;
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

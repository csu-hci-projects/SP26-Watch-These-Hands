using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Reflection;

/// <summary>
/// Runtime policy for hand mode:
/// left hand is menu/poke-only, and left near/far wakes only while the right hand is selecting.
/// </summary>
public class VRHandInteractorPolicy : MonoBehaviour
{
    enum MenuUiHands
    {
        RightPokeOnly,
        BothPokes
    }

    [Header("Left Hand")]
    [SerializeField] Behaviour[] leftPokeInteractors;
    [SerializeField] Behaviour[] leftConditionalInteractors;
    [SerializeField] GameObject[] leftConditionalObjects;
    [SerializeField] Behaviour[] leftAlwaysDisabledInteractors;
    [SerializeField] GameObject[] leftAlwaysDisabledObjects;

    [Header("Right Hand")]
    [SerializeField] XRBaseInteractor[] rightGrabInteractors;

    [Header("Menu UI Input")]
    [SerializeField] MenuUiHands menuUiHands = MenuUiHands.RightPokeOnly;
    [SerializeField] Behaviour[] leftPokeUiInteractors;
    [SerializeField] Behaviour[] rightPokeUiInteractors;
    [SerializeField] Behaviour[] nonPokeUiInteractors;
    [SerializeField] bool onlyPokeInteractorsCanUseUi = true;

    [Header("Behavior")]
    [SerializeField] bool enableLeftConditionalWhileRightSelecting = true;

    static readonly BindingFlags InstanceBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    void Reset()
    {
        AutoAssignFromScene();
    }

    void Awake()
    {
        AutoAssignFromScene();
        ApplyPolicy();
    }

    void LateUpdate()
    {
        ApplyPolicy();
    }

    void AutoAssignFromScene()
    {
        if (leftPokeInteractors == null || leftPokeInteractors.Length == 0)
            leftPokeInteractors = FindNamedBehaviours("Left Hand", "Poke Interactor");

        if (leftConditionalInteractors == null || leftConditionalInteractors.Length == 0)
            leftConditionalInteractors = FindNamedBehaviours("Left Hand", "Near-Far Interactor");

        if (rightGrabInteractors == null || rightGrabInteractors.Length == 0)
            rightGrabInteractors = FindRightGrabInteractors();

        if (leftPokeUiInteractors == null || leftPokeUiInteractors.Length == 0)
            leftPokeUiInteractors = FindUiInteractors("Left Hand", pokeOnly: true);

        if (rightPokeUiInteractors == null || rightPokeUiInteractors.Length == 0)
            rightPokeUiInteractors = FindUiInteractors("Right Hand", pokeOnly: true);

        if (nonPokeUiInteractors == null || nonPokeUiInteractors.Length == 0)
            nonPokeUiInteractors = FindUiInteractors(null, pokeOnly: false);
    }

    void ApplyPolicy()
    {
        SetEnabled(leftPokeInteractors, true);
        SetEnabled(leftAlwaysDisabledInteractors, false);
        SetActive(leftAlwaysDisabledObjects, false);

        var allowConditional = enableLeftConditionalWhileRightSelecting && IsRightSelecting();
        SetEnabled(leftConditionalInteractors, allowConditional);
        SetActive(leftConditionalObjects, allowConditional);

        if (onlyPokeInteractorsCanUseUi)
        {
            SetUiInteraction(leftPokeUiInteractors, menuUiHands == MenuUiHands.BothPokes);
            SetUiInteraction(rightPokeUiInteractors, true);
            SetUiInteraction(nonPokeUiInteractors, false);
        }
    }

    bool IsRightSelecting()
    {
        if (rightGrabInteractors == null)
            return false;

        foreach (var interactor in rightGrabInteractors)
        {
            if (interactor != null && interactor.hasSelection)
                return true;
        }

        return false;
    }

    static void SetEnabled(Behaviour[] behaviours, bool enabled)
    {
        if (behaviours == null)
            return;

        foreach (var behaviour in behaviours)
        {
            if (behaviour != null)
                behaviour.enabled = enabled;
        }
    }

    static void SetActive(GameObject[] objects, bool active)
    {
        if (objects == null)
            return;

        foreach (var target in objects)
        {
            if (target != null)
                target.SetActive(active);
        }
    }

    static Behaviour[] FindNamedBehaviours(string ancestorName, string objectName)
    {
        var results = new System.Collections.Generic.List<Behaviour>();
        foreach (var behaviour in FindObjectsByType<Behaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour == null || behaviour.gameObject.name != objectName)
                continue;

            if (HasAncestorNamed(behaviour.transform, ancestorName))
                results.Add(behaviour);
        }

        return results.ToArray();
    }

    static XRBaseInteractor[] FindRightGrabInteractors()
    {
        var results = new System.Collections.Generic.List<XRBaseInteractor>();
        foreach (var interactor in FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (interactor != null && HasAncestorNamed(interactor.transform, "Right Hand"))
                results.Add(interactor);
        }

        return results.ToArray();
    }

    static Behaviour[] FindUiInteractors(string requiredAncestorName, bool pokeOnly)
    {
        var results = new System.Collections.Generic.List<Behaviour>();
        foreach (var behaviour in FindObjectsByType<Behaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour == null || !HasEnableUiInteractionProperty(behaviour))
                continue;

            var isHandInteractor = HasAncestorNamed(behaviour.transform, "Left Hand") || HasAncestorNamed(behaviour.transform, "Right Hand");
            if (!isHandInteractor)
                continue;

            if (!string.IsNullOrEmpty(requiredAncestorName) && !HasAncestorNamed(behaviour.transform, requiredAncestorName))
                continue;

            var isPoke = behaviour.gameObject.name == "Poke Interactor";
            if (isPoke == pokeOnly)
                results.Add(behaviour);
        }

        return results.ToArray();
    }

    static bool HasEnableUiInteractionProperty(Behaviour behaviour)
    {
        return behaviour.GetType().GetProperty("enableUIInteraction", InstanceBindingFlags) != null;
    }

    static void SetUiInteraction(Behaviour[] behaviours, bool enabled)
    {
        if (behaviours == null)
            return;

        foreach (var behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            var property = behaviour.GetType().GetProperty("enableUIInteraction", InstanceBindingFlags);
            if (property != null && property.CanWrite)
                property.SetValue(behaviour, enabled);
        }
    }

    static bool HasAncestorNamed(Transform target, string ancestorName)
    {
        while (target != null)
        {
            if (target.name == ancestorName)
                return true;

            target = target.parent;
        }

        return false;
    }
}

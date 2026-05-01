using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Keeps the left hand as the menu hand unless the right hand is already selecting something.
/// Attach this as a select filter to left hand grab/manipulation interactors.
/// </summary>
public class VRLeftHandGrabGate : MonoBehaviour, IXRSelectFilter
{
    [SerializeField] XRBaseInteractor[] leftHandInteractors;
    [SerializeField] XRBaseInteractor[] rightHandInteractors;
    [SerializeField] Transform[] alwaysAllowSelectionUnder;

    public bool canProcess => isActiveAndEnabled;

    void OnEnable()
    {
        Register();
    }

    void OnDisable()
    {
        Unregister();
    }

    void Register()
    {
        if (leftHandInteractors == null)
            return;

        foreach (var interactor in leftHandInteractors)
        {
            if (interactor == null)
                continue;

            interactor.selectFilters.Remove(this);
            interactor.selectFilters.Add(this);
        }
    }

    void Unregister()
    {
        if (leftHandInteractors == null)
            return;

        foreach (var interactor in leftHandInteractors)
        {
            if (interactor == null)
                continue;

            interactor.selectFilters.Remove(this);
        }
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        if (!IsLeftHandInteractor(interactor))
            return true;

        if (IsAlwaysAllowed(interactable))
            return true;

        return IsRightHandSelecting();
    }

    bool IsLeftHandInteractor(IXRSelectInteractor interactor)
    {
        if (leftHandInteractors == null)
            return false;

        foreach (var leftInteractor in leftHandInteractors)
        {
            if (ReferenceEquals(leftInteractor, interactor))
                return true;
        }

        return false;
    }

    bool IsRightHandSelecting()
    {
        if (rightHandInteractors == null)
            return false;

        foreach (var rightInteractor in rightHandInteractors)
        {
            if (rightInteractor != null && rightInteractor.hasSelection)
                return true;
        }

        return false;
    }

    bool IsAlwaysAllowed(IXRSelectInteractable interactable)
    {
        if (interactable == null || alwaysAllowSelectionUnder == null)
            return false;

        var selectedTransform = interactable.transform;
        if (selectedTransform == null)
            return false;

        foreach (var allowedRoot in alwaysAllowSelectionUnder)
        {
            if (allowedRoot != null && selectedTransform.IsChildOf(allowedRoot))
                return true;
        }

        return false;
    }
}

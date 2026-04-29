using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Makes the right hand the primary object manipulator.
/// The left hand may only join after the right hand has already selected the object.
/// </summary>
[DisallowMultipleComponent]
public class VRPrimaryHandSelectFilter : MonoBehaviour, IXRSelectFilter
{
    [SerializeField] InteractorHandedness primaryHand = InteractorHandedness.Right;
    [SerializeField] bool allowSecondaryHandWhenPrimarySelected = true;

    XRGrabInteractable grabInteractable;

    public bool canProcess => isActiveAndEnabled;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        if (interactor == null || interactable == null)
            return false;

        if (interactor.handedness == primaryHand)
            return true;

        if (!allowSecondaryHandWhenPrimarySelected)
            return false;

        if (grabInteractable == null)
            grabInteractable = interactable as XRGrabInteractable;

        if (grabInteractable == null)
            return false;

        for (int i = 0; i < grabInteractable.interactorsSelecting.Count; i++)
        {
            var selectingInteractor = grabInteractable.interactorsSelecting[i];
            if (selectingInteractor != null && selectingInteractor.handedness == primaryHand)
                return true;
        }

        return false;
    }
}

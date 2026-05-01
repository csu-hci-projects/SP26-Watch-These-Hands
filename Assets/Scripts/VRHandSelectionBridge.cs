using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Optional hand-scene bridge that makes hand interactors update the existing sticky selection.
/// Object and gizmo manipulation still happens through XRI.
/// </summary>
public class VRHandSelectionBridge : MonoBehaviour
{
    [SerializeField] XRBaseInteractor[] handInteractors;

    void OnEnable()
    {
        RegisterInteractors();
    }

    void OnDisable()
    {
        UnregisterInteractors();
    }

    void RegisterInteractors()
    {
        if (handInteractors == null)
            return;

        foreach (var interactor in handInteractors)
        {
            if (interactor == null)
                continue;

            interactor.selectEntered.RemoveListener(OnSelectEntered);
            interactor.selectEntered.AddListener(OnSelectEntered);
        }
    }

    void UnregisterInteractors()
    {
        if (handInteractors == null)
            return;

        foreach (var interactor in handInteractors)
        {
            if (interactor == null)
                continue;

            interactor.selectEntered.RemoveListener(OnSelectEntered);
        }
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        var selectedObject = args.interactableObject?.transform?.GetComponentInParent<SelectableObject>();
        if (selectedObject == null)
            return;

        VRSelectionManager.Instance?.Select(selectedObject);
    }
}

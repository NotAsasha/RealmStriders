using UnityEngine;

public interface IInteractable
{
    bool IsTaken() { return false; }
    void Interact(GameObject interactor);
    void StopInteraction(GameObject interactor);
}

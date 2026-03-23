using UnityEngine;

namespace Player.Equipment
{
    public interface IInteractable
    {
        bool IsSingleUse() => false;
        bool IsTaken() => false;
        void Interact(GameObject interactor);
        void StopInteraction(GameObject interactor);
    }
}

using UnityEngine;

namespace Player.Equipment
{
    public interface IInteractable
    {
        
        bool IsSingleUse() => false;
        Transform GetCameraPoint() => null;
        bool IsTaken() => false;
        void Interact(GameObject interactor);
        void StopInteraction();
    }
}

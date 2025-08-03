using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

public class Button : NetworkBehaviour, IInteractable
{
    [SerializeField] private UnityEvent onInteract; 
    public bool IsSingleUse() => true;
    public void Interact(GameObject _player)
    {
        onInteract.Invoke();
    }

    public void StopInteraction(GameObject _player)
    {
        
    }
}

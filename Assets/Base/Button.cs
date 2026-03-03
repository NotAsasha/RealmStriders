using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;
using System.Collections;

public class Button : NetworkBehaviour, IInteractable
{
    [SerializeField] private float cooldown = 0f;
    [SerializeField] private UnityEvent onInteract;

    public NetworkVariable<bool> isReady = new(true, 0);

    public bool IsSingleUse() => true;
    public void Interact(GameObject _player)
    {
        if (!isReady.Value) return;

        if (cooldown != 0f) StartCooldownServerRpc();
        onInteract.Invoke();
    }

    public void StopInteraction(GameObject _player)
    {
        
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void StartCooldownServerRpc()
    {
        StartCoroutine(Cooldown());
    }
    private IEnumerator Cooldown()
    {
        isReady.Value = false;
        yield return new WaitForSeconds(cooldown);
        isReady.Value = true;
    }
}

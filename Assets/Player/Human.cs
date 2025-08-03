using UnityEngine;
using Unity.Netcode;
using Player;
using System.Collections;
public class Human : Entity
{
    private Animator animator;
    private CharacterController characterController;
    private void Awake()
    { 
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        ToggleRagdoll(false);
    }

    override protected void KillEntity()
    {
        ToggleRagdoll(true);
        if (IsOwner)
        {
            GameManager.instance.OnPlayerDeathServerRpc();
        }
    }
    override protected void ReviveEntity()
    {
        Debug.Log($"---Human: Reviving myself");
        ToggleRagdoll(false);
    }

    private void ToggleRagdoll(bool isActive)
    {
        if (IsOwner)
        {
            Movement.instance.enabled = !isActive;
            if (isActive)
                Movement.instance.SwitchToInteractionControls();
            else
                Movement.instance.SwitchToGameplayControls();
        }
        animator.enabled = !isActive;
        characterController.enabled = !isActive;
        Debug.Log($"---Human: Toggled ragdoll: {isActive}");
    }
}
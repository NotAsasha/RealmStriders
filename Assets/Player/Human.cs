using UnityEngine;
using Player.Movement;

namespace Player
{

    public class Human : Entity
    {
        private Animator animator;
        public CharacterController characterController;
        protected override void Awake()
        {
            base.Awake();

            animator = GetComponent<Animator>();
            characterController = GetComponent<CharacterController>();
            ToggleRagdoll(false);

            ToSpawnPoint();
        }

        public void ToSpawnPoint()
        {
            characterController.enabled = false;
            transform.position = GameManager.Instance.spawnPoint;
            characterController.enabled = true;

        }

        override protected void KillEntity()
        {
            ToggleRagdoll(true);
            if (IsOwner)
            {
                GameManager.Instance.OnPlayerDeathServerRpc();
            }
        }
        override protected void ReviveEntity()
        {
            Debug.Log($"---Human: Reviving myself");
            ToSpawnPoint();
            ToggleRagdoll(false);
        }

        override protected void OnFreezeStateChange(bool oldV, bool isFreezed)
        {
            characterController.enabled = !isFreezed && !isDead.Value;
        }

        private void ToggleRagdoll(bool isActive)
        {
            if (IsOwner)
            {
                PlayerMovement.Instance.enabled = !isActive;
                if (isActive)
                    PlayerMovement.Instance.SwitchToInteractionControls();
                else
                    PlayerMovement.Instance.SwitchToGameplayControls();
            }
            animator.enabled = !isActive;
            characterController.enabled = !isActive;
            Debug.Log($"---Human: Toggled ragdoll: {isActive}");
        }

        private void Update()
        {
            if (characterController.enabled)
            {
                animator.speed = characterController.velocity.magnitude / 4;
            }
        }
    }

}
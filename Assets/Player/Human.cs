using UnityEngine;
using UnityEngine.SceneManagement;
using Player.Movement;
using Unity.Netcode;

namespace Player
{

    public class Human : Entity
    {
        private Animator animator;
        public CharacterController characterController;
        public AudioSource source;
        private PlayerMovement playerMovement;

        protected override void Awake()
        {
            base.Awake();

            animator = GetComponent<Animator>();
            characterController = GetComponent<CharacterController>();
            playerMovement = GetComponent<PlayerMovement>();
            ToggleRagdoll(false);

            ToSpawnPoint();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsOwner && scene.name == "Lobby")
            {
                ToSpawnPoint();
            }
        }

        public void Teleport(Vector3 position)
        {
            if (characterController != null) characterController.enabled = false;
            transform.position = position;
            if (characterController != null) characterController.enabled = true;

            if (playerMovement != null)
            {
                playerMovement.ResetMovementState();
            }
        }

        public void ToSpawnPoint()
        {
            Vector3 targetPos = GameManager.Instance != null ? GameManager.Instance.spawnPoint : transform.position;
            Teleport(targetPos);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void TakeDamageRpc(float damage)
        {
            AddHealth(-damage);
        }

        override protected void KillEntity()
        {
            StatusVisuals?.OnDeath();
            ToggleRagdoll(true);
            PlayDeathSound();
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
            StatusVisuals?.SetDead(false);
        }

        override protected void OnFreezeStateChange(bool oldV, bool isFreezed)
        {
            if (animator != null) animator.speed = isFreezed ? 0 : 1;
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

        private void PlayDeathSound()
        {
            source.Play();
        }
    }
}
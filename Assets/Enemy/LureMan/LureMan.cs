using Player;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Enemy.LureMan
{
    public class LureMan : Enemy
    {
        [Header("Lure Settings")]
        [SerializeField] private float triggerDistance = 7f;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioClip jumpScreamClip;

        [Header("Procedural Spin")]
        [Tooltip("Трансформ балерини / Armature для процедурного обертання")]
        [SerializeField] private Transform ballerinaTransform;
        [SerializeField] private float spinSpeed = 90f;

        [Header("Jump Setup")]
        [SerializeField] private Animator ballerinaAnimator;
        [SerializeField] private Transform musicBoxTransform;
        [SerializeField] private float jumpAnimationDuration = 2.5f;
        [SerializeField] private string jumpTriggerName = "Jump";

        private bool isAwakened;

        protected override void Start()
        {
            base.Start();

            if (ballerinaAnimator != null)
            {
                animator = ballerinaAnimator;
                animator.enabled = false;
            }


            if (musicSource != null)
            {
                musicSource.loop = true;
                musicSource.Play();
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                effects[EffectType.Asleep].Value = true;
                effects[EffectType.Invincible].Value = true;
            }
        }

        protected override void Update()
        {
            base.Update();

            // Процедурне обертання балерини навколо осі Y, поки скринька заведена
            if (!isAwakened && ballerinaTransform != null)
            {
                ballerinaTransform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
            }
        }

        protected override void Think()
        {
            if (!isAwakened)
            {
                CheckForNearbyPlayers();
                return;
            }

            // first priority, run
            if (enemyState == EnemyState.IsRunning)
            {
                if (agent.remainingDistance > agent.stoppingDistance) return;
                enemyState = EnemyState.IsMoving;
            }

            // second priority, search for player
            if (ChasePlayer(overAggresive)) return;
        }

        private void CheckForNearbyPlayers()
        {
            var detectedPlayer = vision.EntityInHearing();
            if (detectedPlayer != null)
            {
                float distance = Vector3.Distance(transform.position, detectedPlayer.transform.position);
                if (distance <= triggerDistance)
                {
                    AwakenServer();
                }
            }
        }

        private void AwakenServer()
        {
            if (isAwakened) return;
            isAwakened = true;

            WakeUpClientRpc();
            StartCoroutine(WakeUpSequence());
        }

        [ClientRpc]
        private void WakeUpClientRpc()
        {
            isAwakened = true;
            animator.enabled = true;
            if (musicSource != null)
            {
                musicSource.Stop();
                if (jumpScreamClip != null)
                {
                    musicSource.PlayOneShot(jumpScreamClip);
                }
            }

            if (musicBoxTransform != null)
            {
                musicBoxTransform.SetParent(null);
            }

            if (animator != null)
            {
                animator.SetTrigger(jumpTriggerName);
            }
        }

        private IEnumerator WakeUpSequence()
        {
            yield return new WaitForSeconds(jumpAnimationDuration);

            if (IsServer)
            {
                effects[EffectType.Asleep].Value = false;
                effects[EffectType.Invincible].Value = false;

                enemyState = EnemyState.IsMoving;
                ChasePlayer(overAggresive);
            }
        }
    }
}
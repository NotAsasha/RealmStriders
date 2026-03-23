using System.Collections;
using Player;
using Player.Equipment;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;

namespace Enemy
{
    [RequireComponent(typeof(NavMeshAgent), typeof(EntityDetector))]
    public class Enemy : Entity, ICollidable
    {
        public float damage = 1f;
        public bool overAggresive = false;
        public float moveRange = 10f;
        public float defaultSpeed = 5f;

        public float stepSoundCooldown = 0.3f;
        public AudioSource audioSource;
        public AudioResource[] stepSounds;

        protected EnemyState enemyState = EnemyState.IsMoving;

        protected Animator animator;
        protected EntityDetector vision;
        protected NavMeshAgent agent;
        protected Rigidbody playerRigidbody;
        protected Collider collider1;

        #region Initialization

        protected virtual void Start()
        {
            if (TryGetComponent(out agent))
            {
                agent.speed = defaultSpeed;
            }
            else
            {
                Debug.LogWarning("No NavMeshAgent, might need to add.");
            }
            if (vision == null) TryGetComponent<EntityDetector>(out vision);

            if (playerRigidbody == null) TryGetComponent<Rigidbody>(out playerRigidbody);
            if (collider1 == null) TryGetComponent<Collider>(out collider1);
            if (animator == null) TryGetComponent<Animator>(out animator);

            ToggleRagdoll(true);
        }

        #endregion

        #region Entity

        protected override void KillEntity()
        {
            if (!IsOwner) return;
            ToggleRagdoll(false);
        }

        protected override void ReviveEntity()
        {
            Debug.Log($"---{name}: Reviving myself");
            ToggleRagdoll(true);
        }

        protected override void OnFreezeStateChange(bool oldV, bool isFreezed)
        {
            animator.speed = isFreezed ? 0 : 1;
            agent.speed = isFreezed ? 0 : defaultSpeed;
        }

        private void ToggleRagdoll(bool isActive)
        {
            Debug.Log($"ToggleRagdoll, is entity alive - {isActive}");
            animator.enabled = isActive;
            vision.enabled = isActive;
            agent.enabled = isActive;
            playerRigidbody.isKinematic = isActive;
            collider1.isTrigger = isActive;
        }

        #endregion

        #region ICollidable

        public void OnColliderEnter(GameObject collider)
        {
            if (!IsServer || isDead.Value || IsEffectActive(EffectType.Freeze) /*|| !GameManager.instance.hasStartedMission.Value*/) return;
            var player = collider.GetComponent<Entity>();
            if (player == null || player.isDead.Value) return;
            if (!overAggresive && collider.GetComponent<Enemy>() != null) return;

            BiteClientRpc(collider);
            player.AddHealth(-damage);
        }

        [ClientRpc]
        protected virtual void BiteClientRpc(NetworkObjectReference obj)
        {
            obj.TryGet(out NetworkObject player);
            Debug.Log($"---Enemy: Eaten {player.name}");
        }

        #endregion

        private float nextUpdate;
        void Update()
        {
            if (!IsServer || isDead.Value || IsEffectActive(EffectType.Freeze)) return;

            vision.DrawViewState(); //draw vision boundaries
            if (Time.time >= nextUpdate)
            {
                nextUpdate = Time.time + 0.3f + Random.Range(0f, 0.1f);
                Think();
            }
            if (isSoundReady && agent.velocity.magnitude > 0.1) PlayStepsSound(stepSoundCooldown);
        }

        protected virtual void Think()
        {

            //first priority, run
            if (enemyState == EnemyState.IsRunning)
            {
                if (agent.remainingDistance > agent.stoppingDistance) return;
                enemyState = EnemyState.IsMoving;
            }

            //second priority, search for player
            if (ChasePlayer(overAggresive)) return;

            //third priority, go to the target location
            if (agent.remainingDistance > agent.stoppingDistance & agent.pathStatus == NavMeshPathStatus.PathComplete) return; //not done with path

            //forth priority, go somewhere
            Move();
        }

        public void Run()
        {
            Move();
            enemyState = EnemyState.IsRunning;
        }

        protected virtual bool ChasePlayer(bool countEnemies = false)
        {
            var player = vision.EntityInSight(countEnemies);
            if (player != null)
            {
                Vector3 target = player.transform.position;
                if ((agent.destination - target).sqrMagnitude > 0.5f * 0.5f)
                {
                    agent.SetDestination(target);
                }
                enemyState = EnemyState.IsChasingPlayer;
            }
            return (player != null);
        }

        public void Lure(Vector3 coords)
        {
            if (!IsServer || enemyState < EnemyState.IsChasingSound) return;
            if (agent.SetDestination(coords))
            {
                enemyState = EnemyState.IsChasingSound;
            }
        }

        protected virtual void Move()
        {
            Vector3 point;
            if (RandomMove.RandomPoint(transform.position, moveRange, out point)) //choose where to go
            {
                Debug.DrawRay(point, Vector3.up, UnityEngine.Color.blue, 1.0f); //so you can see with gizmos
                agent.SetDestination(point);
                enemyState = EnemyState.IsMoving;
            }
        }

        bool isSoundReady = true;
        private void PlayStepsSound(float cooldown)
        {
            isSoundReady = false;
            if (stepSounds.Length <= 0)
            {
                Debug.LogWarning($"---Enemy: {name} has no movement sounds.");
                return;
            }
            int soundsCount = stepSounds.Length;
            audioSource.resource = stepSounds[Random.Range(0, soundsCount)];
            audioSource.Play();
            StartCoroutine(SoundTimer(cooldown));
        }

        private IEnumerator SoundTimer(float cooldown)
        {
            yield return new WaitForSeconds(cooldown);
            isSoundReady = true;
            audioSource.Stop();
        }
    }
    public enum EnemyState
    {
        IsRunning = 0,
        IsChasingPlayer = 1,
        IsChasingSound = 2,
        IsMoving = 3,
    }
}
using System.Collections;
using Base.Alarm;
using Player;
using Player.Equipment;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;
using System;

namespace Enemy
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class Enemy : Entity, ICollidable
    {
        [Header("Movement")]
        public float damage = 1f;
        public bool overAggresive = false;
        public float moveRange = 10f;
        public float defaultSpeed = 5f;
        public float chaseSpeed = 8f;
        public float thinkCooldown = 0.3f;

        [Header("Flee")]
        [SerializeField, Min(0.5f)] private float fleeDistance = 12f;
        [SerializeField, Range(1, 8)] private int fleeDirectionSamples = 5;
        [SerializeField, Min(0.5f)] private float navMeshSampleDistance = 4f;
        private NavMeshPath fleePath;

        [Header("Step Sounds")]
        public float stepSoundCooldown = 0.3f;
        public float chaseStepSoundCooldown = 0.18f;
        public float stepSoundRadius = 15f;
        public AudioSource audioSource;
        public AudioClip[] stepSounds;

        public NetworkVariable<EnemyState> netEnemyState = new(
            EnemyState.IsMoving,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private EnemyState _enemyState;
        public EnemyState enemyState
        {
            get => IsSpawned ? netEnemyState.Value : _enemyState;
            protected set
            {
                if (_enemyState == value) return;

                _enemyState = value;
                if (IsServer && IsSpawned)
                {
                    netEnemyState.Value = value;
                }
                else if (!IsSpawned)
                {
                    onStateChanged?.Invoke(_enemyState);
                }
            }
        }
        public event Action<EnemyState> onStateChanged;
        public event Action onBite;
        

        protected Animator animator;
        protected EntityDetector vision;
        protected NavMeshAgent agent;
        protected Rigidbody playerRigidbody;
        protected Collider mainCollider;

        #region Initialization

        protected override void Awake()
        {
            base.Awake();

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
            if (mainCollider == null) TryGetComponent<Collider>(out mainCollider);
            if (animator == null) TryGetComponent<Animator>(out animator);
            if (audioSource == null) TryGetComponent<AudioSource>(out audioSource);
        }

        protected virtual void Start()
        {
            if (chaseSpeed <= defaultSpeed)
            {
                chaseSpeed = defaultSpeed * 1.5f;
            }

            if (agent != null)
            {
                agent.speed = defaultSpeed;
            }

            ToggleRagdoll(true);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            netEnemyState.OnValueChanged += OnNetEnemyStateChanged;
            _enemyState = netEnemyState.Value;

            // Only the server runs NavMesh movement; clients receive the authoritative transform.
            if (agent != null)
            {
                agent.enabled = IsServer && !isDead.Value;
                if (IsServer) agent.speed = defaultSpeed;
            }
        }

        public override void OnNetworkDespawn()
        {
            netEnemyState.OnValueChanged -= OnNetEnemyStateChanged;
            base.OnNetworkDespawn();
        }

        private void OnNetEnemyStateChanged(EnemyState previousValue, EnemyState newValue)
        {
            _enemyState = newValue;
            onStateChanged?.Invoke(newValue);
        }

        #endregion

        #region Entity

        protected override void KillEntity()
        {
            if (!IsOwner) return;
            ToggleRagdoll(false);

            // Let the alarm system know — it will cancel the alarm if no intruders remain in the base
            if (IsServer)
            {
                BaseAlarmManager.Instance?.NotifyEnemyDied(this);
            }
        }

        protected override void ReviveEntity()
        {
            Debug.Log($"---{name}: Reviving myself");
            ToggleRagdoll(true);
        }

        protected override void OnFreezeStateChange(bool oldV, bool isFreezed)
        {
            if (animator != null) animator.speed = isFreezed ? 0 : 1;
            if (agent != null) agent.speed = isFreezed ? 0 : defaultSpeed;
        }

        protected override void BeginCaptureLockServer()
        {
            if (agent != null)
            {
                if (agent.isOnNavMesh) agent.ResetPath();
                agent.enabled = false;
            }

            if (vision != null) vision.enabled = false;
            if (playerRigidbody != null) playerRigidbody.isKinematic = true;
            if (mainCollider != null) mainCollider.enabled = false;
        }

        protected override void OnCaptureFinalizedServer()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.activeEnemies.Remove(this);
            }
        }

        private void ToggleRagdoll(bool isActive)
        {
            Debug.Log($"ToggleRagdoll, is entity alive - {isActive}");
            if (animator != null) animator.enabled = isActive;
            if (vision != null) vision.enabled = isActive;
            if (agent != null) agent.enabled = isActive && IsServer;
            if (playerRigidbody != null) playerRigidbody.isKinematic = isActive;
            if (mainCollider != null) mainCollider.isTrigger = isActive;
        }

        #endregion

        #region ICollidable

        public void OnColliderEnter(GameObject collider)
        {
            if (!IsServer || isDead.Value || IsBeingCaptured || IsEffectActive(EffectType.Freeze) || IsEffectActive(EffectType.Asleep) /*|| !GameManager.instance.hasStartedMission.Value*/) return;
            var player = collider.GetComponentInParent<Entity>();
            if (player == null || player.isDead.Value) return;
            if (!overAggresive && collider.GetComponentInParent<Enemy>() != null) return;

            BiteClientRpc(collider);
            player.AddHealth(-damage);
        }

        [ClientRpc]
        protected virtual void BiteClientRpc(NetworkObjectReference obj)
        {
            obj.TryGet(out NetworkObject player);
            Debug.Log($"---Enemy: Eaten {player.name}");
            onBite?.Invoke();
        }

        #endregion

        private float nextUpdate;
        private Vector3 lastPosition;

        protected virtual void Update()
        {
            if (isDead.Value || IsBeingCaptured || IsEffectActive(EffectType.Freeze)) return;

            if (IsServer)
            {
                if (Time.time >= nextUpdate)
                {
                    nextUpdate = Time.time + thinkCooldown + UnityEngine.Random.Range(0f, 0.1f);
                    if (!ContinueRunning()) Think();
                }
            }

            //for sound
            float currentSpeed = Time.deltaTime > 0f ? (transform.position - lastPosition).magnitude / Time.deltaTime : 0f;
            lastPosition = transform.position;

            if (currentSpeed > 0.1f)
            {
                float t = Mathf.Clamp01((currentSpeed - defaultSpeed) / Mathf.Max(chaseSpeed - defaultSpeed, 0.1f));
                float cooldown = Mathf.Lerp(stepSoundCooldown, chaseStepSoundCooldown, t);
                PlayStepsSound(cooldown, t);
            }
        }

        protected virtual void Think()
        {
            if (agent == null) return;

            //second priority, search for player
            if (ChasePlayer(overAggresive)) return;

            //third priority, go to the target location
            if (agent.isOnNavMesh && agent.remainingDistance > agent.stoppingDistance && agent.pathStatus == NavMeshPathStatus.PathComplete) return; //not done with path

            //forth priority, go somewhere
            Move();
        }

        public void Run()
        {
            if (!IsServer) return;
            var threat = vision != null ? vision.EntityInSight() ?? vision.EntityInHearing() : null;
            Run(threat != null ? threat.transform.position : transform.position + transform.forward);
        }

        public void Run(Vector3 sourcePosition)
        {
            if (!IsServer || isDead.Value || IsBeingCaptured || IsEffectActive(EffectType.Asleep) ||
                agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

            if (!TryFindFleePoint(sourcePosition, out Vector3 fleePoint) || !agent.SetDestination(fleePoint)) return;

            enemyState = EnemyState.IsRunning;
            if (!IsEffectActive(EffectType.Freeze))
            {
                agent.speed = chaseSpeed;
            }
        }

        private bool ContinueRunning()
        {
            if (enemyState != EnemyState.IsRunning) return false;
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                if (agent.pathPending || (agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete &&
                    agent.remainingDistance > agent.stoppingDistance))
                {
                    agent.speed = chaseSpeed;
                    return true;
                }
                agent.speed = defaultSpeed;
            }
            enemyState = EnemyState.IsMoving;
            return false;
        }

        protected bool TryFindFleePoint(Vector3 sourcePosition, out Vector3 fleePoint)
        {
            fleePath ??= new NavMeshPath();
            Vector3 origin = transform.position;
            Vector3 away = origin - sourcePosition;
            away.y = 0f;
            float sourceDistanceSquared = away.sqrMagnitude;
            if (away.sqrMagnitude <= 0.001f) away = -transform.forward;
            away.y = 0f;
            if (away.sqrMagnitude <= 0.001f) away = Vector3.back;
            away.Normalize();

            fleePoint = origin;
            float bestDistance = sourceDistanceSquared;
            int sampleCount = Mathf.Max(1, fleeDirectionSamples);
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };

            // Shorter candidates let enemies escape in tight rooms without choosing a random direction.
            for (int distanceStep = 0; distanceStep < 3; distanceStep++)
            {
                float distance = Mathf.Max(0.5f, fleeDistance) / (1 << distanceStep);
                for (int i = 0; i < sampleCount; i++)
                {
                    float angle = sampleCount == 1 ? 0f : -60f + 120f * i / (sampleCount - 1);
                    Vector3 direction = Quaternion.Euler(0f, angle, 0f) * away;
                    Vector3 candidate = origin + direction * distance;
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, filter)) continue;

                    Vector3 displacement = hit.position - origin;
                    displacement.y = 0f;
                    Vector3 fromSource = hit.position - sourcePosition;
                    fromSource.y = 0f;
                    float distanceFromSource = fromSource.sqrMagnitude;
                    if (Vector3.Dot(displacement, away) <= 0f || distanceFromSource <= bestDistance ||
                        displacement.sqrMagnitude <= agent.stoppingDistance * agent.stoppingDistance) continue;
                    if (!agent.CalculatePath(hit.position, fleePath) || fleePath.status != NavMeshPathStatus.PathComplete) continue;

                    bestDistance = distanceFromSource;
                    fleePoint = hit.position;
                }
            }
            return bestDistance > sourceDistanceSquared;
        }

        protected virtual bool ChasePlayer(bool countEnemies = false)
        {
            var seenPlayer = vision != null ? vision.EntityInSight(countEnemies) : null;
            var heardPlayer = (seenPlayer == null && vision != null) ? vision.EntityInHearing() : null;
            var player = seenPlayer ?? heardPlayer;
            if (player != null)
            {
                Vector3 target = player.transform.position;
                if (agent != null && agent.isOnNavMesh && (agent.destination - target).sqrMagnitude > 0.5f * 0.5f)
                {
                    agent.SetDestination(target);
                }
                enemyState = EnemyState.IsChasingPlayer;
                if (agent != null && !IsEffectActive(EffectType.Freeze))
                {
                    agent.speed = (seenPlayer != null) ? chaseSpeed : defaultSpeed;
                }
            }
            else
            {
                if (agent != null && !IsEffectActive(EffectType.Freeze) && enemyState != EnemyState.IsRunning)
                {
                    agent.speed = defaultSpeed;
                }
            }
            return (player != null);
        }

        public virtual void Lure(Vector3 coords)
        {
            if (!IsServer || IsEffectActive(EffectType.Asleep) || enemyState < EnemyState.IsChasingSound ||
                agent == null || !agent.isOnNavMesh) return;
            if (agent.SetDestination(coords))
            {
                enemyState = EnemyState.IsChasingSound;
                if (!IsEffectActive(EffectType.Freeze))
                {
                    agent.speed = defaultSpeed;
                }
            }
        }

        protected virtual void Move()
        {
            if (agent == null) return;
            if (!IsEffectActive(EffectType.Freeze))
            {
                agent.speed = defaultSpeed;
            }
            Vector3 point;
            if (RandomMove.RandomPoint(transform.position, moveRange, out point)) //choose where to go
            {
                Debug.DrawRay(point, Vector3.up, UnityEngine.Color.blue, 1.0f); //so you can see with gizmos
                if (agent.isOnNavMesh)
                {
                    agent.SetDestination(point);
                }
                enemyState = EnemyState.IsMoving;
            }
        }

        private float nextStepTime;

        protected virtual void PlayStepsSound(float cooldown, float speedFactor = 0f)
        {
            if (audioSource == null) return;
            if (Time.time < nextStepTime) return;
            if (stepSounds == null || stepSounds.Length <= 0)
            {
                Debug.LogWarning($"---Enemy: {name} has no movement sounds.");
                return;
            }

            nextStepTime = Time.time + cooldown;
            if (stepSoundRadius > 0f)
            {
                audioSource.maxDistance = stepSoundRadius;
            }

            float basePitch = Mathf.Lerp(1.0f, 1.25f, speedFactor);
            audioSource.pitch = basePitch + UnityEngine.Random.Range(-0.05f, 0.05f);
            audioSource.PlayOneShot(stepSounds[UnityEngine.Random.Range(0, stepSounds.Length)]);
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

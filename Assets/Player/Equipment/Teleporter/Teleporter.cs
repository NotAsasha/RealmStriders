using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Player;

namespace Player.Equipment.Teleporter
{
    public sealed class Teleporter : Item, IChargeable
    {
        [Header("Teleport")]
        [SerializeField, Min(1)] private int maxAttempts = 32;
        [SerializeField, Min(0f)] private float minimumDistance = 12f;
        [SerializeField, Min(0f)] private float baseExclusionRadius = 24f;
        [SerializeField, Min(0f)] private float destinationClearance = 0.65f;
        [SerializeField] private LayerMask destinationObstacles = ~0;

        [Header("Presentation")]
        [SerializeField] private Animator buttonAnimator;
        [SerializeField] private ParticleSystem useVfx;
        [SerializeField] private ParticleSystem destinationVfx;
        [SerializeField] private AudioSource teleportAudio;
        [SerializeField] private AudioClip teleportSound;


        [Header("IChargeable")]

        [SerializeField] private Renderer indicator;
        public readonly NetworkVariable<int> chargePercent = new(100,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public int CurrentCharge => chargePercent.Value;
        public int MaxCharge => 100;

        private const int UseCost = 50;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock indicatorProperties;
        private float nextUseTime;
        private readonly Collider[] destinationHits = new Collider[16];

        #region IChargiable

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            chargePercent.OnValueChanged += UpdateIndicator;
            UpdateIndicator(0, CurrentCharge);
        }

        public override void OnNetworkDespawn()
        {
            chargePercent.OnValueChanged -= UpdateIndicator;
            base.OnNetworkDespawn();
        }

        void IChargeable.ModifyCharge(int amount)
        {
            if (!IsServer) return;
            chargePercent.Value = Mathf.Clamp(CurrentCharge + amount, 0, MaxCharge);
        }


        private void UpdateIndicator(int previous, int current)
        {
            if (indicator == null) return;
            indicatorProperties ??= new MaterialPropertyBlock();
            Color color = current >= MaxCharge ? Color.green : current >= UseCost ? Color.yellow : Color.red;
            indicator.GetPropertyBlock(indicatorProperties);
            indicatorProperties.SetColor(BaseColorId, color);
            indicatorProperties.SetColor(ColorId, color);
            indicatorProperties.SetColor(EmissionColorId, color);
            indicator.SetPropertyBlock(indicatorProperties);
        }

        #endregion



        protected override void ExecuteItemAction(GameObject player)
        {
            if (!IsClient || !IsSpawned || CurrentCharge < UseCost || player == null) return;

            TeleportServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void TeleportServerRpc(RpcParams rpcParams = default)
        {
            if (!IsServer || GameManager.Instance == null ||
                !GameManager.Instance.hasStartedMission.Value ||
                !IsHeldByClient(rpcParams.Receive.SenderClientId) ||
                Time.time < nextUseTime
                || CurrentCharge < UseCost)
            {
                return;
            }

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
                    rpcParams.Receive.SenderClientId, out NetworkClient client) ||
                client.PlayerObject == null ||
                !client.PlayerObject.TryGetComponent(out Human human) ||
                human.isDead.Value)
            {
                return;
            }

            if (!TryGetDestination(human.transform.position, out Vector3 destination))
            {
                return;
            }

            ((IChargeable)this).ModifyCharge(-UseCost);
            nextUseTime = Time.time + 0.75f;
            Vector3 origin = human.transform.position;
            human.Teleport(destination, human.transform.rotation);
            TeleportClientRpc(origin, destination);
        }

        [ClientRpc]
        private void TeleportClientRpc(Vector3 origin, Vector3 destination)
        {
            if (buttonAnimator != null)
            {
                buttonAnimator.Play("Press", 0, 0f);
            }

            PlayDetachedVfx(useVfx, origin);
            if (destinationVfx != null)
            {
                destinationVfx.transform.position = destination;
                PlayDetachedVfx(destinationVfx, destination);
            }

            if (teleportSound != null)
            {
                Game.Audio.ItemAcoustics.PlayAt(teleportSound, origin, teleportAudio);
            }
            else if (teleportAudio != null)
            {
                teleportAudio.Play();
            }
        }


        private static void PlayDetachedVfx(ParticleSystem particle, Vector3 position)
        {
            if (particle == null) return;

            particle.transform.SetParent(null, true);
            particle.transform.position = position;
            particle.Play(true);
            Object.Destroy(particle.gameObject, 1.5f);
        }

        private bool TryGetDestination(Vector3 origin, out Vector3 destination)
        {
            destination = default;
            Enemy.EnemySpawner spawner = GameManager.Instance.GetComponent<Enemy.EnemySpawner>();
            if (spawner == null)
            {
                Debug.LogWarning("---Teleporter: Cannot use item because EnemySpawner is missing.");
                return false;
            }

            float minimumDistanceSqr = minimumDistance * minimumDistance;
            float exclusionRadius = Mathf.Max(baseExclusionRadius, GameManager.Instance.baseRadius);
            Vector3 basePosition = GameManager.Instance.spawnPoint;
            float exclusionRadiusSqr = exclusionRadius * exclusionRadius;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (!spawner.RandomMapPoint(out Vector3 candidate))
                {
                    continue;
                }

                if ((candidate - origin).sqrMagnitude < minimumDistanceSqr ||
                    (candidate - basePosition).sqrMagnitude < exclusionRadiusSqr)
                {
                    continue;
                }

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas) ||
                    !IsDestinationClear(hit.position))
                {
                    continue;
                }

                destination = hit.position;
                return true;
            }

            Debug.LogWarning("---Teleporter: Could not find a safe random NavMesh destination.");
            return false;
        }

        private bool IsDestinationClear(Vector3 position)
        {
            const float capsuleRadius = 0.35f;
            const float capsuleHeight = 1.75f;
            Vector3 bottom = position + Vector3.up * capsuleRadius;
            Vector3 top = position + Vector3.up * (capsuleHeight - capsuleRadius);
            int hitCount = Physics.OverlapCapsuleNonAlloc(
                bottom,
                top,
                capsuleRadius + destinationClearance,
                destinationHits,
                destinationObstacles,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = destinationHits[i];
                destinationHits[i] = null;
                if (collider != null && !collider.gameObject.isStatic)
                {
                    return false;
                }
            }

            return true;
        }

    }
}

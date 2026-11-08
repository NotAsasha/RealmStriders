using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Player;

namespace Player.Equipment.Teleporter
{
    public sealed class ReturnTeleporter : Item, IChargeable
    {
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

        private float nextUseTime;

        protected override void ExecuteItemAction(GameObject player)
        {
            if (!IsClient || !IsSpawned || CurrentCharge < UseCost || player == null) return;

            ReturnServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ReturnServerRpc(RpcParams rpcParams = default)
        {
            if (!IsServer || GameManager.Instance == null ||
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
            ((IChargeable)this).ModifyCharge(-UseCost);
            nextUseTime = Time.time + 0.75f;
            Vector3 origin = human.transform.position;
            Vector3 destination = GameManager.Instance.spawnPoint;
            human.Teleport(destination, human.transform.rotation);
            ReturnClientRpc(origin, destination);
        }

        [ClientRpc]
        private void ReturnClientRpc(Vector3 origin, Vector3 destination)
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
    }
}

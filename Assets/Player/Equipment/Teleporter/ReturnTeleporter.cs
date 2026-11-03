using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Player;

namespace Player.Equipment.Teleporter
{
    public sealed class ReturnTeleporter : Item
    {
        [Header("Presentation")]
        [SerializeField] private Animator buttonAnimator;
        [SerializeField] private ParticleSystem useVfx;
        [SerializeField] private ParticleSystem destinationVfx;
        [SerializeField] private AudioSource teleportAudio;
        [SerializeField] private AudioClip teleportSound;

        private float nextUseTime;

        protected override void ExecuteItemAction(GameObject player)
        {
            if (!IsClient || !IsSpawned || player == null) return;

            ReturnServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ReturnServerRpc(RpcParams rpcParams = default)
        {
            if (!IsServer || GameManager.Instance == null ||
                !IsHeldByClient(rpcParams.Receive.SenderClientId) ||
                Time.time < nextUseTime)
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

            nextUseTime = Time.time + 0.75f;
            Vector3 origin = human.transform.position;
            Vector3 destination = GameManager.Instance.spawnPoint;
            human.Teleport(destination, human.transform.rotation);

            if (TryGetHolderInventory(out Inventory inventory))
            {
                inventory.ForgetItemServer(this);
            }

            ReturnClientRpc(origin, destination);
            StartCoroutine(ConsumeAfterPresentation());
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
                AudioSource.PlayClipAtPoint(teleportSound, origin);
            }
            else if (teleportAudio != null)
            {
                teleportAudio.Play();
            }
        }

        private IEnumerator ConsumeAfterPresentation()
        {
            yield return new WaitForSeconds(0.35f);
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
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

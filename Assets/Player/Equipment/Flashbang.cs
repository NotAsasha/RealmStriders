using Unity.Netcode;
using UnityEngine;

namespace Player.InventorySystem
{
    public class Flashbang : Item
    {
        [SerializeField] float effectRadius = 5;
        [SerializeField] LayerMask entityLayer;
        [SerializeField] ParticleSystem emit;
        [SerializeField] AudioSource audioS;
        protected override void ExecuteItemAction(GameObject player)
        {
            Debug.Log("---Landmine: Used!");
            ExplodeServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ExplodeServerRpc()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, effectRadius, entityLayer, QueryTriggerInteraction.Collide);
            foreach (Collider collider in hits)
            {
                if (collider.TryGetComponent<Enemy>(out var enemy))
                {
                    enemy.Run();
                }
            }

            ExplodeClientRpc();
            NetworkObject.Despawn(true);
        }

        [ClientRpc]
        private void ExplodeClientRpc()
        {
            Debug.Log("---Flashbang: Boom!");
            PlayParticles();
        }
        private void PlayParticles()
        {
            emit.transform.parent = null;
            emit.Play();
            audioS.Play();
            Destroy(emit.gameObject, 2f);
        }
    }
}
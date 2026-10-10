using Enemy;
using Unity.Netcode;
using UnityEngine;

namespace Player.Equipment.Landmine
{
    public class Landmine : Item, ICollidable
    {
        [SerializeField] float explosionRadius = 5;
        [SerializeField] float weakDuration = 8f;
        [SerializeField] ParticleSystem emit;
        [SerializeField] LayerMask entityLayer;
        [SerializeField] LayerMask wallLayer;

        [SerializeField] Renderer indicator;

        SoundProducer soundProducer;

        private void Start()
        {
            soundProducer = GetComponent<SoundProducer>();
        }

        bool isTriggered = false;
        protected override void ExecuteItemAction(GameObject player)
        {
            Debug.Log("---Landmine: Used!");
            ExplodeServerRpc();
        }

        public bool IsTaken() { return isCurrentlyHeld || isTriggered; }

        public void OnColliderEnter(GameObject collider)
        {
            if (isCurrentlyHeld) return;
            indicator.material.color = Color.green;
            Debug.Log($"---Landmine: Collided with {collider.name}!");
            isTriggered = true;
            soundProducer.EmitSoundServerRpc(0);
        }

        public void OnColliderExit(GameObject collider)
        {
            if (!IsServer || isCurrentlyHeld || !isTriggered) return;
            isTriggered = false;
            ExplodeServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ExplodeServerRpc()
        {
            soundProducer.EmitSoundServerRpc(1);
            Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, entityLayer, QueryTriggerInteraction.Collide);
            foreach (Collider collider in hits)
            {
                ApplyDamage(collider);
            }

            ExplodeClientRpc();
            NetworkObject.Despawn(true);
        }

        void ApplyDamage(Collider collider)
        {
            Vector3 toTarget = collider.transform.position - transform.position;
            float distanceToTarget = toTarget.magnitude;

            // Check for walls between mine and target
            if (Physics.Raycast(transform.position, toTarget.normalized, out RaycastHit hit, distanceToTarget, wallLayer)) return;

            Entity entity = collider.GetComponentInParent<Entity>();
            if (entity == null || entity.isDead.Value) return;

            // Slot 2: Landmine owns this weakness layer.
            entity.AddHealth(-1);
            entity.ApplyWeakStack(1, weakDuration, slot: 2);
            Debug.Log($"---Landmine: Applied weakness to {entity.name}.");
        }


        [ClientRpc]
        private void ExplodeClientRpc()
        {
            Debug.Log("---Landmine: Boom!");
            PlayParticles();
        }
        private void PlayParticles()
        {
            emit.transform.parent = null;
            emit.Play();
            Destroy(emit.gameObject, 2f);
        }
    }
}
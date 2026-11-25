using System.Collections.Generic;
using Enemy;
using Unity.Netcode;
using UnityEngine;

namespace Player.Equipment.Landmine
{
    public class Landmine : Item, ICollidable
    {
        [SerializeField] private float explosionRadius = 5;
        [SerializeField] private float weakDuration = 8f;
        [SerializeField] private ParticleSystem emit;
        [SerializeField] private LayerMask entityLayer;
        [SerializeField] private LayerMask wallLayer;

        [SerializeField] private Renderer indicator;

        private SoundProducer soundProducer;
        private readonly HashSet<Entity> affectedEntities = new();
        private readonly NetworkVariable<bool> isTriggered = new(false);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            soundProducer = GetComponent<SoundProducer>();
            isTriggered.OnValueChanged += UpdateIndicator;
            UpdateIndicator(false, isTriggered.Value);
        }

        public override void OnNetworkDespawn()
        {
            isTriggered.OnValueChanged -= UpdateIndicator;
            base.OnNetworkDespawn();
        }

        protected override void ExecuteItemAction(GameObject player)
        {
            if (IsClient && IsSpawned) ExplodeServerRpc();
        }

        public bool IsTaken() => isCurrentlyHeld || isTriggered.Value;

        private void UpdateIndicator(bool _, bool triggered)
        {
            if (triggered && indicator != null) indicator.material.color = Color.green;
        }

        public void OnColliderEnter(GameObject collider)
        {
            if (!IsServer || !IsSpawned || isCurrentlyHeld || isTriggered.Value) return;
            isTriggered.Value = true;
            soundProducer?.EmitSoundServer(0);
        }

        public void OnColliderExit(GameObject collider)
        {
            if (!IsServer || !IsSpawned || isCurrentlyHeld || !isTriggered.Value) return;
            ExplodeServer();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ExplodeServerRpc(RpcParams rpc = default)
        {
            if (TryGetUsableHolderServer(rpc.Receive.SenderClientId, out _))
            {
                UpdateRemoteHeldPose();
                ExplodeServer();
            }
        }

        private void ExplodeServer()
        {
            soundProducer?.EmitSoundServer(1);
            affectedEntities.Clear();
            Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, entityLayer, QueryTriggerInteraction.Collide);
            foreach (Collider collider in hits)
            {
                ApplyDamage(collider);
            }
            affectedEntities.Clear();

            ExplodeClientRpc();
            NetworkObject.Despawn(true);
        }

        private void ApplyDamage(Collider collider)
        {
            Vector3 toTarget = collider.transform.position - transform.position;
            float distanceToTarget = toTarget.magnitude;

            // Check for walls between mine and target
            if (Physics.Raycast(transform.position, toTarget.normalized, distanceToTarget, wallLayer)) return;

            Entity entity = collider.GetComponentInParent<Entity>();
            if (entity == null || !entity.IsSpawned || entity.IsDead() || !affectedEntities.Add(entity)) return;

            // Slot 2: Landmine owns this weakness layer.
            entity.AddHealth(-1);
            entity.ApplyWeakStack(1, weakDuration, slot: 2);
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

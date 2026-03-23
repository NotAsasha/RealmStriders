using Enemy;
using Unity.Netcode;
using UnityEngine;

namespace Player.Equipment.Landmine
{
    public class Landmine : Item, ICollidable
    {
        [SerializeField] float explosionRadius = 5;
        [SerializeField] float damage = 1f;
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

        public bool IsTaken() { return isTriggered; }

        public void OnColliderEnter(GameObject collider)
        {
            if (isCurrentlyHeld) return;
            indicator.material.color = Color.green;
            Debug.Log("---Landmine: Collided with something!");
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
            //Calculate all vectors
            Vector3 toTarget = collider.transform.position - transform.position;
            Vector3 direction = toTarget.normalized;
            float distanceToTarget = toTarget.magnitude;

            //Check for walls
            if (Physics.Raycast(transform.position, direction, out RaycastHit hit, distanceToTarget, wallLayer)) return;

            //Apply damage (can depand on distance)
            var entity = collider.GetComponent<Entity>();
            // float damageToApply = damage / Mathf.Max(distanceToTarget, 1f);
            entity.AddHealth(-damage);

            Debug.Log("---Landmine: Damaged entity.");


            if (entity.IsDead())
            {
                var rigidbody = collider.GetComponent<Rigidbody>();
                if (rigidbody != null)
                {
                    collider.GetComponent<Rigidbody>()?.AddForce(direction * 10f);
                }

                Debug.Log("---Landmine: Killed some entity.");
            }
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
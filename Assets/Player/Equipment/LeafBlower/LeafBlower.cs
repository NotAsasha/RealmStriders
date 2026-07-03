using Player.Movement;
using UnityEngine;

namespace Player.Equipment.LeafBlower
{
    public class LeafBlower : Item
    {
        [SerializeField] LayerMask entityLayer;
        [SerializeField] float maxDistance = 20f;
        [SerializeField] ParticleSystem particle;

        #region Item Specific Functionality

        protected override void ExecuteItemAction(GameObject player)
        {
            particle.Play();
            if (!Physics.Raycast(
                CameraMovement.Instance.transform.position,
                CameraMovement.Instance.transform.forward,
                out RaycastHit hit, maxDistance, entityLayer)) return;

            Entity entity = hit.transform.gameObject.GetComponent<Entity>();

            float entityHealth = entity.GetHealth() - (entity.IsEffectActive(EffectType.Weak) ? 1f : 0f);
            if (entityHealth < 1f && !entity.isDead.Value)
            {
                entity.TurnIntoSphereServerRpc();
            }


            Debug.Log($"---LeafBlower: Entity danger: {entity.GetHealth()}");

            Debug.Log("Blowing leaves with powerful wind!");

            // - Particle effects
            // - Sound effects
            // - Physics interactions with enemies
            // - Cooldown mechanics
        }
        #endregion

    }
}
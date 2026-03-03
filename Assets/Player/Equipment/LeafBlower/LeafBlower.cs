using UnityEngine;

namespace Player.InventorySystem
{
    public class LeafBlower : Item
    {
        [SerializeField] LayerMask entityLayer;
        [SerializeField] float maxDistance = 20f;
        [SerializeField] ParticleSystem particle;

        #region Item Specific Functionality

        override protected void ExecuteItemAction(GameObject player)
        {
            particle.Play();
            if (!Physics.Raycast(
                CameraMovement.instance.transform.position,
                CameraMovement.instance.transform.forward,
                out RaycastHit hit, maxDistance, entityLayer)) return;

            Entity entity = hit.transform.gameObject.GetComponent<Entity>();
            if (entity.GetHealth() < 1f && !entity.isDead.Value)
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
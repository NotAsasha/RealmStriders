using UnityEngine;
using TMPro;
using static UnityEngine.EventSystems.EventTrigger;

namespace InventorySystem
{
    public class Scanner : Item
    {
        [SerializeField] LayerMask entityLayer;
        [SerializeField] float maxDistance = 20f;
        [SerializeField] TMP_Text danger;

        #region Item Specific Functionality

        override protected void ExecuteItemAction(GameObject player)
        {

            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, maxDistance, entityLayer))
            {

                Entity entity = hit.transform.gameObject.GetComponent<Entity>();

                danger.text = entity.GetHealth().ToString();
                Debug.Log($"---Scanner: Entity danger: {entity.GetHealth()}");
            }
            else
            {
                danger.text = "Not found";
            }
            // - Particle effects
            // - Sound effects
            // - Physics interactions with enemies
            // - Cooldown mechanics

        }
        #endregion

    }
}
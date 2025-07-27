using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace InventorySystem
{
    public class LeafBlower : Item
    {

        #region Item Specific Functionality

        override protected void ExecuteItemAction(GameObject player)
        {
            // Example implementation - replace with actual leaf blower logic
            Debug.Log("Blowing leaves with powerful wind!");

            // You could add:
            // - Particle effects
            // - Sound effects
            // - Physics interactions with nearby objects
            // - Cooldown mechanics

            // If this is a single-use item, you might want to destroy it or remove it from inventory
            if (isSingleUse)
            {
                HandleSingleUseItem(player);
            }
        }
        #endregion
    }
}
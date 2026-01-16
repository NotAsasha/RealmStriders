using UnityEngine;
using TMPro;
using static UnityEngine.EventSystems.EventTrigger;
using UnityEngine.UI;
using Player;
using Unity.Netcode;

namespace InventorySystem
{
    public class Scanner : Item
    {
        [SerializeField] LayerMask entityLayer;
        [SerializeField] float maxDistance = 20f;
        [SerializeField] TMP_Text danger;
        [SerializeField] Image freezeIcon;
        [SerializeField] Image waterIcon;

        NetworkVariable<bool> isOn = new(false, 0, 0);

        #region Item Specific Functionality

        override protected void ExecuteItemAction(GameObject player)
        {
            SwitchStateServerRpc();
        }

        [ServerRpc]
        private void SwitchStateServerRpc()
        {
            isOn.Value = !isOn.Value;
        }

        private void Update()
        {
            if (!isOn.Value) return;
            if (Physics.Raycast(
                CameraMovement.instance.transform.position,
                CameraMovement.instance.transform.forward,
                out RaycastHit hit, maxDistance, entityLayer))
            {

                Entity entity = hit.transform.gameObject.GetComponent<Entity>();

                danger.text = entity.GetHealth().ToString();

                freezeIcon.color = entity.IsEffectActive(EffectType.Freeze) ? Color.white : Color.black;
                waterIcon.color = entity.IsEffectActive(EffectType.Water) ? Color.white : Color.black;

                Debug.Log($"---Scanner: Entity danger: {entity.GetHealth()}");
            }
            else
            {
                freezeIcon.color = Color.black;
                waterIcon.color = Color.black;
                danger.text = "Not Found";
            }
            // - Particle effects
            // - Sound effects
            // - Physics interactions with enemies
            // - Cooldown mechanics
        }

        #endregion
    }
}
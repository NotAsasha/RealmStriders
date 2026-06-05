using Player.Movement;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Player.Equipment.Taser
{
    public class BaseMover : Item
    {
        [SerializeField] float maxDistance = 5f;

        #region Item Specific Functionality

        override protected void ExecuteItemAction(GameObject player)
        {
            if (Physics.Raycast(
                CameraMovement.Instance.transform.position,
                CameraMovement.Instance.transform.forward,
                out RaycastHit hit, maxDistance))
            {
                if (hit.transform.TryGetComponent<IMovable>(out var obj))
                {
                    obj.Pack();
                    Debug.Log($"---BaseMover: Shot object: {hit.collider.gameObject.name}");
                }
            }
            else Debug.Log($"---BaseMover: Missed :(");

            // - Particle effects
            // - Sound effects
            // - Physics interactions with enemies
        }
        #endregion
    }
}
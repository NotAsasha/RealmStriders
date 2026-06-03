using Player;
using UnityEngine;
using UnityEngine.AI;

namespace Portals
{
    public class PortalTeleporter : MonoBehaviour
    {
        public Transform receiver;
        public Transform enemyReceiver;


        private bool objectIsOverlapping = false;
        private Transform objectToTeleport;

        private void Update()
        {
            if (!objectIsOverlapping || !objectToTeleport) return;

            Transform portalRoot = transform.parent;
            // Calculate position relative to the portal
            Vector3 portalToObject = objectToTeleport.position - portalRoot.position;
            
            // The portal faces -Z (visual), so its root forward (+Z) points INTO the portal.
            // Teleport when the player passes through the plane (dot product becomes positive).
            float dotProduct = Vector3.Dot(portalRoot.forward, portalToObject);

            if (dotProduct > 0f)
            {
                Debug.Log($"---Portal: Teleporting {objectToTeleport.name} to {receiver.name}");

                // Calculate rotation difference (180 degree flip to face out of the exit portal)
                Quaternion portalRotationDifference = receiver.rotation * Quaternion.Euler(0, 180, 0) * Quaternion.Inverse(portalRoot.rotation);

                CharacterController cc = objectToTeleport.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                // Rotate and position the player relative to the exit portal
                objectToTeleport.rotation = portalRotationDifference * objectToTeleport.rotation;
                
                // Switch active cameras via manager
                PortalManager.Instance.SwitchCameras();

                // Flip the offset as well to place the player in front of the exit portal
                Vector3 localOffset = portalRoot.InverseTransformPoint(objectToTeleport.position);
                Vector3 mirrorOffset = new Vector3(-localOffset.x, localOffset.y, -localOffset.z);
                objectToTeleport.position = receiver.TransformPoint(mirrorOffset);

                if (cc != null) cc.enabled = true;

                objectIsOverlapping = false;
                PortalManager.Instance.CallOnTeleport(objectToTeleport.GetComponent<Human>());
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                objectToTeleport = other.transform;
                objectIsOverlapping = true;
                //Debug.Log($"---{name}: {other.gameObject.name} entered.");
            }
            else if (other.gameObject.GetComponent<Enemy.Enemy>() != null)
            {
                if (other.TryGetComponent<NavMeshAgent>(out var nma)) nma.enabled = false;
                other.transform.position = enemyReceiver.position;
                if (nma != null) nma.enabled = true;

                if (other.TryGetComponent<Enemy.Enemy>(out var en)) en.Lure(enemyReceiver.position);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.transform == objectToTeleport)
            {
                //Debug.Log($"---{name}: {other.gameObject.name} exited.");
                objectIsOverlapping = false;
                objectToTeleport = null;
            }
        }
    }
}

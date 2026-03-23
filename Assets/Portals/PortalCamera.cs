using Player.Movement;
using UnityEngine;

namespace Portals
{
    public class PortalCamera : MonoBehaviour {

        public Transform playerCamera;
        public Transform portal;
        public Transform otherPortal;

        private void OnEnable()
        {
            playerCamera = PlayerMovement.Instance.playerCamera.transform;
        }

        private void LateUpdate()
        {
            if (!playerCamera) return;

            Vector3 playerOffsetFromPortal = playerCamera.position - portal.position;
            transform.position = otherPortal.position + playerOffsetFromPortal;

            Quaternion portalRotationalDifference = otherPortal.rotation * Quaternion.Inverse(portal.rotation);


            Vector3 newCameraDirection = portalRotationalDifference * playerCamera.forward;

            transform.rotation = Quaternion.LookRotation(newCameraDirection, Vector3.up);
        }

    }
}

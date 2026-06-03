using Player.Movement;
using UnityEngine;

namespace Portals
{
    public class PortalCamera : MonoBehaviour {

        public Transform playerCamera;
        public Transform portal;
        public Transform otherPortal;
        public MeshRenderer portalRenderer;
        
        private Camera portalCam;
        private Camera mainCam;

        private void Awake()
        {
            portalCam = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            if (PlayerMovement.Instance != null && PlayerMovement.Instance.playerCamera != null)
            {
                playerCamera = PlayerMovement.Instance.playerCamera.transform;
                mainCam = PlayerMovement.Instance.playerCamera;
            }
        }

        private void LateUpdate()
        {
            if (!playerCamera || !mainCam)
            {
                if (PlayerMovement.Instance != null && PlayerMovement.Instance.playerCamera != null)
                {
                    playerCamera = PlayerMovement.Instance.playerCamera.transform;
                    mainCam = PlayerMovement.Instance.playerCamera;
                }
                else return;
            }

            // Optimization: Only render if visible or close
            float distance = Vector3.Distance(playerCamera.position, portal.position);
            if (portalRenderer != null)
            {
                if (!portalRenderer.isVisible && distance > 3f)
                {
                    portalCam.enabled = false;
                    return;
                }
                portalCam.enabled = true;
            }

            // Position and rotation logic (Mirror effect)
            Vector3 localPos = portal.InverseTransformPoint(playerCamera.position);
            Vector3 mirrorPos = new Vector3(-localPos.x, localPos.y, -localPos.z);
            transform.position = otherPortal.TransformPoint(mirrorPos);

            Quaternion relativeRot = Quaternion.Inverse(portal.rotation) * playerCamera.rotation;
            transform.rotation = otherPortal.rotation * Quaternion.Euler(0, 180, 0) * relativeRot;

            // Oblique Clipping Plane to fix clipping through walls/geometry
            UpdateNearClipPlane();
        }

        private void UpdateNearClipPlane()
        {
            // Reset to main camera's projection matrix to avoid accumulation of errors
            portalCam.projectionMatrix = mainCam.projectionMatrix;

            Vector3 normal = otherPortal.forward;
            float dot = Vector3.Dot(normal, transform.position - otherPortal.position);
            
            // Stability check: If the camera is extremely close to the portal surface,
            // applying an oblique matrix can cause the projection to become degenerate.
            if (Mathf.Abs(dot) < 0.1f) return;

            if (dot > 0) normal = -normal;

            Vector3 camSpacePos = portalCam.worldToCameraMatrix.MultiplyPoint(otherPortal.position);
            Vector3 camSpaceNormal = portalCam.worldToCameraMatrix.MultiplyVector(normal).normalized;
            float camSpaceDist = -Vector3.Dot(camSpacePos, camSpaceNormal);
            
            Vector4 clipPlaneCameraSpace = new Vector4(camSpaceNormal.x, camSpaceNormal.y, camSpaceNormal.z, camSpaceDist);
            portalCam.projectionMatrix = mainCam.CalculateObliqueMatrix(clipPlaneCameraSpace);
        }
    }
}

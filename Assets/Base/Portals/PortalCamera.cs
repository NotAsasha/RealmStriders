using Player.Movement;
using UnityEngine;

namespace Portals
{
    public class PortalCamera : MonoBehaviour {

        public Transform playerCamera;
        public Transform portal;
        public Transform otherPortal;
        public MeshRenderer portalRenderer;

        [Header("Optimization Settings")]
        [Tooltip("Maximum distance from player to portal before the portal camera stops rendering.")]
        public float maxRenderDistance = 60f;

        [Tooltip("Throttles rendering to every 2nd frame when far away (Performant quality).")]
        public bool throttleFarDistance = false;

        private Camera portalCam;
        private Camera mainCam;
        private int originalCullingMask;
        private readonly Plane[] frustumPlanes = new Plane[6];
        private float lastVisibleTime;
        private const float SleepHysteresisTime = 0.35f;

        private void Awake()
        {
            portalCam = GetComponent<Camera>();
            if (portalCam != null)
            {
                originalCullingMask = portalCam.cullingMask;
                SetCameraRendering(false);
            }
        }

        public void SetQualityConfig(float maxDist, bool throttle)
        {
            maxRenderDistance = maxDist;
            throttleFarDistance = throttle;
        }

        private void OnEnable()
        {
            if (PlayerMovement.Instance != null && PlayerMovement.Instance.playerCamera != null)
            {
                playerCamera = PlayerMovement.Instance.playerCamera.transform;
                mainCam = PlayerMovement.Instance.playerCamera;
            }
            lastVisibleTime = float.NegativeInfinity;
            SetCameraRendering(false);
        }

        private void OnDisable()
        {
            SetCameraRendering(false);
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
                else
                {
                    SetCameraRendering(false);
                    return;
                }
            }

            if (!portalCam || !portal || !otherPortal)
            {
                SetCameraRendering(false);
                return;
            }

            // Optimization: Only render when visible, facing the player, and within range
            if (!ShouldRender())
            {
                SetCameraRendering(false);
                return;
            }

            // Position and rotation logic (Mirror effect)
            Vector3 localPos = portal.InverseTransformPoint(playerCamera.position);
            Vector3 mirrorPos = new Vector3(-localPos.x, localPos.y, -localPos.z);
            transform.position = otherPortal.TransformPoint(mirrorPos);

            Quaternion relativeRot = Quaternion.Inverse(portal.rotation) * playerCamera.rotation;
            transform.rotation = otherPortal.rotation * Quaternion.Euler(0, 180, 0) * relativeRot;

            // Oblique Clipping Plane to fix clipping through walls/geometry
            UpdateNearClipPlane();
            SetCameraRendering(true);
        }

        private bool ShouldRender()
        {
            if (!mainCam || !playerCamera || !portal) return false;
            if (portalRenderer != null &&
                (!portalRenderer.enabled || !portalRenderer.gameObject.activeInHierarchy)) return false;

            // 1. Distance culling
            Vector3 toPlayer = playerCamera.position - portal.position;
            float distanceSquared = toPlayer.sqrMagnitude;
            if (maxRenderDistance <= 0f || distanceSquared > maxRenderDistance * maxRenderDistance)
            {
                return false;
            }

            // 2. Facing culling: portal visual front faces -Z, so root forward (+Z) points into portal
            float dot = Vector3.Dot(portal.forward, toPlayer);
            // If dot > 0.3f, player is behind the portal and cannot see the front face
            if (dot > 0.3f && distanceSquared > 2.25f)
            {
                return false;
            }

            // 3. Frustum visibility check with hysteresis buffer
            bool inFrustum = true;
            if (portalRenderer != null)
            {
                GeometryUtility.CalculateFrustumPlanes(mainCam, frustumPlanes);
                inFrustum = GeometryUtility.TestPlanesAABB(frustumPlanes, portalRenderer.bounds);
            }

            if (inFrustum)
            {
                lastVisibleTime = Time.time;

                // 4. Far distance frame throttling (Performant mode)
                if (throttleFarDistance && distanceSquared > 64f)
                {
                    if ((Time.frameCount % 2) != 0)
                    {
                        return false;
                    }
                }
                return true;
            }

            // Keep alive for a brief hysteresis period so rotating camera doesn't flap on/off
            return (Time.time - lastVisibleTime) < SleepHysteresisTime;
        }

        private void SetCameraRendering(bool render)
        {
            if (portalCam == null) return;
            if (portalCam.enabled == render) return;

            // Skip the entire URP camera and keep its last image on throttled frames.
            if (render) portalCam.cullingMask = originalCullingMask;
            portalCam.enabled = render;
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

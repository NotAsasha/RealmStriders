using Player.Movement;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Player.Equipment
{
    /// <summary>
    /// Controls first-person held item positioning, viewmodel sway, and hand IK adaptation.
    /// The item is firmly held relative to the camera (following pitch and yaw) with Phasmophobia-style
    /// subtle mouse sway and soft camera-bob cancellation. Hand IK (TwoBoneIKConstraint) adapts to
    /// the item's grip point rather than the item shaking with the hand animation.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public class ItemHolder : MonoBehaviour
    {
        [Header("Camera Reference")]
        [Tooltip("Camera transform used to anchor the held item.")]
        [SerializeField] private Transform cameraTransform;

        [Header("Hold Positioning")]
        [Tooltip("Base position of the held item relative to the camera in local space.")]
        [SerializeField] private Vector3 holdPositionLocal = new Vector3(0.18f, -0.20f, 0.18f);

        [Tooltip("Base rotation Euler angles applied to the held item.")]
        [SerializeField] private Vector3 holdRotationEuler = Vector3.zero;

        [Header("Sway Settings (Phasmophobia Style)")]
        [Tooltip("Enable subtle viewmodel sway when moving the mouse.")]
        [SerializeField] private bool enableSway = true;

        [Tooltip("Maximum positional sway offset (X = lateral, Y = vertical).")]
        [SerializeField] private Vector2 swayMaxOffset = new Vector2(0.04f, 0.025f);

        [Tooltip("Speed at which the sway offset returns to neutral.")]
        [SerializeField] private float swayReturnSpeed = 6f;

        [Tooltip("Multiplier for mouse movement to sway offset.")]
        [SerializeField] private Vector2 swayAmount = new Vector2(0.002f, 0.001f);

        [Header("Look Coupling")]
        [Tooltip("Coupling factor adjusting item distance and height when looking vertically.")]
        [SerializeField] private float verticalLookCoupling = 0.1f;

        [Header("Pitch Limits")]
        [Tooltip("Clamp item pitch rotation to prevent extreme twisting at camera limits.")]
        [SerializeField] private bool clampItemPitch = true;

        [Tooltip("Maximum item pitch angle when looking UP in degrees (negative value, e.g. -45).")]
        [SerializeField] private float minItemPitch = -45f;

        [Tooltip("Maximum item pitch angle when looking DOWN in degrees (positive value, e.g. 45).")]
        [SerializeField] private float maxItemPitch = 45f;

        public bool ClampItemPitch { get => clampItemPitch; set => clampItemPitch = value; }
        public float MinItemPitch { get => minItemPitch; set => minItemPitch = value; }
        public float MaxItemPitch { get => maxItemPitch; set => maxItemPitch = value; }

        [Header("IK & Socket References")]
        [Tooltip("Transform driving TwoBoneIKConstraint for the right hand.")]
        [SerializeField] private Transform handTarget;

        [Tooltip("Optional rotation offset for the hand on the grip.")]
        [SerializeField] private Vector3 handGripRotationOffset = Vector3.zero;

        [Tooltip("Intermediate socket transform to which the item is anchored.")]
        [SerializeField] private Transform heldItemSocket;

        public Transform HeldItemSocket => heldItemSocket;

        private Item currentActiveItem;
        private Vector2 currentSway;
        private TwoBoneIKConstraint handIKConstraint;
        private bool isInitialized;

        private void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (isInitialized) return;

            InitializeSocket();
            ResolveReferences();
        }

        private void InitializeSocket()
        {
            if (cameraTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam == null) cam = Camera.main;
                if (cam != null) cameraTransform = cam.transform;
            }

            if (heldItemSocket == null)
            {
                Transform existing = cameraTransform != null ? cameraTransform.Find("HeldItemSocket") : null;
                if (existing == null) existing = transform.Find("HeldItemSocket");
                if (existing != null)
                {
                    heldItemSocket = existing;
                }
                else
                {
                    GameObject socketObj = new GameObject("HeldItemSocket");
                    Transform parentT = cameraTransform != null ? cameraTransform : transform;
                    socketObj.transform.SetParent(parentT, false);
                    socketObj.transform.localPosition = holdPositionLocal;
                    socketObj.transform.localRotation = Quaternion.Euler(holdRotationEuler);
                    heldItemSocket = socketObj.transform;
                }
            }

            if (cameraTransform != null && heldItemSocket != null && heldItemSocket.parent != cameraTransform)
            {
                heldItemSocket.SetParent(cameraTransform, false);
                heldItemSocket.localPosition = holdPositionLocal;
                heldItemSocket.localRotation = Quaternion.Euler(holdRotationEuler);
            }
        }

        private void ResolveReferences()
        {
            if (cameraTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam == null) cam = Camera.main;
                if (cam != null) cameraTransform = cam.transform;
                else cameraTransform = transform;
            }

            Transform root = transform.root;
            if (handIKConstraint == null)
            {
                handIKConstraint = root.GetComponentInChildren<TwoBoneIKConstraint>(true);
            }

            if (handIKConstraint != null && handTarget == null)
            {
                handTarget = handIKConstraint.data.target;
            }

            if (handTarget == null)
            {
                Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
                foreach (var child in allChildren)
                {
                    if (child.name == "HandTarget")
                    {
                        handTarget = child;
                        break;
                    }
                }
            }

            if (cameraTransform != null && heldItemSocket != null)
            {
                if (heldItemSocket.parent != cameraTransform)
                {
                    heldItemSocket.SetParent(cameraTransform, false);
                }
                heldItemSocket.localPosition = holdPositionLocal;
                heldItemSocket.localRotation = Quaternion.Euler(holdRotationEuler);
            }

            isInitialized = true;
        }

        public void SetActiveItem(Item item)
        {
            currentActiveItem = item;
            if (item != null)
            {
                SnapToTarget();
            }
        }

        public void SnapToTarget()
        {
            EnsureInitialized();
            if (cameraTransform == null || heldItemSocket == null) return;

            UpdateSocketTransform();
            UpdateHeldItemAndHand();
        }

        private void Update()
        {
            EnsureInitialized();
            if (!isInitialized || cameraTransform == null || heldItemSocket == null) return;

            UpdateSocketTransform();
            UpdateHeldItemAndHand();
        }

        private void LateUpdate()
        {
            if (!isInitialized || cameraTransform == null || heldItemSocket == null) return;

            UpdateHeldItemAndHand();
        }

        private float GetCameraPitch()
        {
            if (CameraMovement.Instance != null)
            {
                return CameraMovement.Instance.CurrentPitch;
            }

            if (cameraTransform != null)
            {
                CameraMovement camMove = cameraTransform.GetComponent<CameraMovement>();
                if (camMove == null) camMove = cameraTransform.GetComponentInParent<CameraMovement>();
                if (camMove != null) return camMove.CurrentPitch;

                float pitch = cameraTransform.localEulerAngles.x;
                if (pitch > 180f) pitch -= 360f;
                return pitch;
            }

            return 0f;
        }

        private void UpdateSocketTransform()
        {
            // 1. Procedural mouse sway (Phasmophobia style)
            if (enableSway && PlayerMovement.Instance != null && PlayerMovement.Instance.controls != null)
            {
                Vector2 lookInput = PlayerMovement.Instance.controls.Gameplay.Look.ReadValue<Vector2>();
                Vector2 targetSway = new Vector2(
                    Mathf.Clamp(-lookInput.x * swayAmount.x, -swayMaxOffset.x, swayMaxOffset.x),
                    Mathf.Clamp(-lookInput.y * swayAmount.y, -swayMaxOffset.y, swayMaxOffset.y)
                );
                currentSway = Vector2.Lerp(currentSway, targetSway, Time.deltaTime * swayReturnSpeed * 2.5f);
            }
            else
            {
                currentSway = Vector2.Lerp(currentSway, Vector2.zero, Time.deltaTime * swayReturnSpeed);
            }

            // 2. Camera pitch & limits
            float camPitch = GetCameraPitch();
            float clampedPitch = clampItemPitch ? Mathf.Clamp(camPitch, minItemPitch, maxItemPitch) : camPitch;
            float pitchOffset = clampedPitch - camPitch;

            // 3. Base local hold offset + pitch coupling (when looking up/down)
            Vector3 localPos = holdPositionLocal + new Vector3(currentSway.x, currentSway.y, 0f);
            float pitchNorm = clampedPitch / 90f; // -1 (up) to +1 (down)

            if (pitchNorm < 0f)
            {
                // Looking up: smoothly pull item slightly closer so player arm doesn't overreach
                localPos.z += pitchNorm * verticalLookCoupling * 0.12f;
                localPos.y += pitchNorm * verticalLookCoupling * 0.05f;
            }
            else
            {
                // Looking down: slight natural forward angle
                localPos.z -= pitchNorm * verticalLookCoupling * 0.03f;
                localPos.y -= pitchNorm * verticalLookCoupling * 0.02f;
            }

            // 5. Rotation with sway tilt and clamped pitch offset
            Quaternion swayTilt = Quaternion.identity;
            if (enableSway)
            {
                swayTilt = Quaternion.Euler(
                    currentSway.y * 100f,
                    currentSway.x * 120f,
                    -currentSway.x * 150f
                );
            }

            Quaternion pitchOffsetRot = Quaternion.Euler(pitchOffset, 0f, 0f);
            Quaternion targetLocalRot = pitchOffsetRot * Quaternion.Euler(holdRotationEuler) * swayTilt;

            // 6. Driven purely in local camera space - ZERO world space lerp lag, ZERO running shake!
            if (heldItemSocket.parent == cameraTransform)
            {
                heldItemSocket.localPosition = localPos;
                heldItemSocket.localRotation = targetLocalRot;
            }
            else
            {
                heldItemSocket.position = cameraTransform.TransformPoint(localPos);
                heldItemSocket.rotation = cameraTransform.rotation * targetLocalRot;
            }
        }

        private void UpdateHeldItemAndHand()
        {
            if (currentActiveItem == null || !currentActiveItem.gameObject.activeInHierarchy)
            {
                return;
            }

            // 1. Align active item directly to heldItemSocket
            (Vector3 transOffset, Vector3 rotOffset) = currentActiveItem.GetGripOffsets();
            currentActiveItem.transform.position = heldItemSocket.position + heldItemSocket.rotation * transOffset;
            currentActiveItem.transform.rotation = heldItemSocket.rotation * Quaternion.Euler(rotOffset);

            // 2. Adapt hand IK target to item grip point
            if (handTarget != null)
            {
                Vector3 gripWorldPos = currentActiveItem.gripPoint != null ? currentActiveItem.gripPoint.position : heldItemSocket.position;
                Quaternion gripWorldRot = currentActiveItem.gripPoint != null ? currentActiveItem.gripPoint.rotation : heldItemSocket.rotation;

                handTarget.position = gripWorldPos;
                handTarget.rotation = gripWorldRot * Quaternion.Euler(handGripRotationOffset);
            }
        }
    }
}


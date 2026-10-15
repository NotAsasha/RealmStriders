using Player.Movement;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Player.Equipment
{
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

        public Transform HeldItemSocket
        {
            get
            {
                EnsureInitialized();
                return heldItemSocket;
            }
        }

        private Item currentActiveItem;
        private Vector2 currentSway;
        private TwoBoneIKConstraint handIKConstraint;
        private CameraMovement cachedCameraMovement;
        private PlayerHeadSync cachedHeadSync;
        private bool isInitialized;
        private bool wasInteracting;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            if (PlayerMovement.Instance != null)
            {
                PlayerMovement.Instance.onInteractionStateChanged += OnInteractionStateChangedListener;
            }
        }

        private void OnDisable()
        {
            if (PlayerMovement.Instance != null)
            {
                PlayerMovement.Instance.onInteractionStateChanged -= OnInteractionStateChangedListener;
            }

            if (wasInteracting && heldItemSocket != null && cameraTransform != null)
            {
                heldItemSocket.SetParent(cameraTransform, false);
                heldItemSocket.localPosition = holdPositionLocal;
                heldItemSocket.localRotation = Quaternion.Euler(holdRotationEuler);
                heldItemSocket.localScale = Vector3.one;
                wasInteracting = false;
            }
        }

        private void OnInteractionStateChangedListener(bool isInteracting)
        {
            if (isInteracting != wasInteracting)
            {
                HandleInteractionStateChange(isInteracting);
                wasInteracting = isInteracting;
            }
        }

        private bool IsInteracting()
        {
            return PlayerMovement.Instance != null && PlayerMovement.Instance.isInInteraction;
        }

        private Transform GetBodyTransform()
        {
            if (CameraMovement.Instance != null && CameraMovement.Instance.playerBody != null)
            {
                return CameraMovement.Instance.playerBody;
            }

            return transform.root;
        }

        private void HandleInteractionStateChange(bool isInteracting)
        {
            if (heldItemSocket == null) return;

            if (isInteracting)
            {
                Transform bodyTransform = GetBodyTransform();
                if (bodyTransform != null)
                {
                    heldItemSocket.SetParent(bodyTransform, true);
                    heldItemSocket.localScale = Vector3.one;
                }
            }
            else
            {
                if (cameraTransform != null)
                {
                    heldItemSocket.SetParent(cameraTransform, false);
                    heldItemSocket.localPosition = holdPositionLocal;
                    heldItemSocket.localRotation = Quaternion.Euler(holdRotationEuler);
                    heldItemSocket.localScale = Vector3.one;
                }
                currentSway = Vector2.zero;
            }
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

            if (!IsInteracting() && cameraTransform != null && heldItemSocket != null && heldItemSocket.parent != cameraTransform)
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
                cameraTransform = cam != null ? cam.transform : transform;
            }

            if (cameraTransform != null)
            {
                cachedCameraMovement = cameraTransform.GetComponent<CameraMovement>();
                if (cachedCameraMovement == null)
                {
                    cachedCameraMovement = cameraTransform.GetComponentInParent<CameraMovement>();
                }
            }

            Transform root = transform.root;
            if (cachedHeadSync == null)
            {
                cachedHeadSync = root.GetComponentInChildren<PlayerHeadSync>(true);
            }

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

            if (!IsInteracting() && cameraTransform != null && heldItemSocket != null)
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

            Transform shoulder = cachedHeadSync != null ? cachedHeadSync.ShoulderBone : null;
            float minReach = cachedHeadSync != null ? cachedHeadSync.MinSafeArmReach : 0.15f;
            float maxReach = cachedHeadSync != null ? cachedHeadSync.MaxSafeArmReach : 0.575f;

            EvaluateViewModelAndIKTarget(shoulder, minReach, maxReach);
        }

        private void Update()
        {
            EnsureInitialized();
            if (!isInitialized) return;

            bool isInteracting = IsInteracting();
            if (isInteracting != wasInteracting)
            {
                HandleInteractionStateChange(isInteracting);
                wasInteracting = isInteracting;
            }

            SampleMouseSway();
        }

        /// <summary>
        /// Called deterministically from PlayerHeadSync.LateUpdate() after spine/head rotation
        /// and right before SolveTwoBoneIK() so the socket, item, and hand IK target are 100% in sync.
        /// </summary>
        public void EvaluateViewModelAndIKTarget(Transform shoulderBone, float minSafeReach, float maxSafeReach)
        {
            EnsureInitialized();
            if (!isInitialized || cameraTransform == null || heldItemSocket == null) return;

            if (!IsInteracting())
            {
                UpdateSocketTransform();
            }

            UpdateHeldItemAndHand(shoulderBone, minSafeReach, maxSafeReach);
        }

        private void SampleMouseSway()
        {
            if (!IsInteracting() && enableSway && PlayerMovement.Instance != null && PlayerMovement.Instance.controls != null)
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
        }

        private float GetCameraPitch()
        {
            if (CameraMovement.Instance != null)
            {
                return CameraMovement.Instance.CurrentPitch;
            }

            if (cachedCameraMovement != null)
            {
                return cachedCameraMovement.CurrentPitch;
            }

            if (cameraTransform != null)
            {
                float pitch = cameraTransform.localEulerAngles.x;
                if (pitch > 180f) pitch -= 360f;
                return pitch;
            }

            return 0f;
        }

        private void UpdateSocketTransform()
        {
            float camPitch = GetCameraPitch();
            float clampedPitch = clampItemPitch ? Mathf.Clamp(camPitch, minItemPitch, maxItemPitch) : camPitch;
            float pitchOffset = clampedPitch - camPitch;

            Vector3 localPos = holdPositionLocal + new Vector3(currentSway.x, currentSway.y, 0f);
            float pitchNorm = clampedPitch / 90f;

            if (pitchNorm < 0f)
            {
                localPos.z += pitchNorm * verticalLookCoupling * 0.12f;
                localPos.y += pitchNorm * verticalLookCoupling * 0.05f;
            }
            else
            {
                localPos.z -= pitchNorm * verticalLookCoupling * 0.03f;
                localPos.y -= pitchNorm * verticalLookCoupling * 0.02f;
            }

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

        private void UpdateHeldItemAndHand(Transform shoulderBone, float minSafeReach, float maxSafeReach)
        {
            if (currentActiveItem == null || !currentActiveItem.gameObject.activeInHierarchy)
            {
                return;
            }

            Vector3 desiredGripPos = heldItemSocket.position;
            Quaternion desiredGripRot = heldItemSocket.rotation;

            // Clamp grip world position to the animated shoulder's physical reach envelope
            // so the arm NEVER overstretches and the hand NEVER detaches from the item handle.
            if (shoulderBone != null)
            {
                Vector3 shoulderToGrip = desiredGripPos - shoulderBone.position;
                float dist = shoulderToGrip.magnitude;
                if (dist > maxSafeReach && dist > 1e-5f)
                {
                    desiredGripPos = shoulderBone.position + (shoulderToGrip / dist) * maxSafeReach;
                }
                else if (dist < minSafeReach && dist > 1e-5f)
                {
                    desiredGripPos = shoulderBone.position + (shoulderToGrip / dist) * minSafeReach;
                }
            }

            // Align item so its gripPoint (or grip offset) lands exactly on desiredGripPos / desiredGripRot
            currentActiveItem.AlignToGripPose(desiredGripPos, desiredGripRot);

            if (handTarget != null)
            {
                handTarget.position = desiredGripPos;
                handTarget.rotation = desiredGripRot * Quaternion.Euler(handGripRotationOffset);
            }
        }
    }
}
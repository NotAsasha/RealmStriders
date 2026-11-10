using Player.Movement;
using UnityEngine;

namespace Player.Equipment
{
    // Presentation only; Inventory and PlayerHeadSync choose the owning peer's pose.
    public class ItemHolder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Inventory inventory;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private CameraMovement cameraMovement;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Transform handTarget;
        [SerializeField] private Transform heldItemSocket;

        [Header("Palm calibration (in hand-bone space)")]
        [SerializeField] private Vector3 palmPositionLocal = new(0f, 0.025f, -0.015f);
        [Tooltip("Wrist orientation relative to the item's grip coordinate system. Shared by both views.")]
        [SerializeField] private Vector3 handGripRotationOffset = new(0f, 50f, 90f);

        [Header("Hold Positioning")]
        [SerializeField] private Vector3 holdPositionLocal = new(0.20f, -0.12f, 0.38f);
        [SerializeField] private Vector3 holdRotationEuler;
        [SerializeField] private Vector3 remotePositionFromShoulder = new(0.02f, -0.14f, 0.38f);
        [SerializeField, Min(0.01f)] private float cameraClearance = 0.12f;
        [SerializeField] private bool clampItemPitch = true;
        [SerializeField] private float minItemPitch = -45f;
        [SerializeField] private float maxItemPitch = 45f;

        [Header("Equip Animation")]
        [SerializeField, Min(0.01f)] private float equipDuration = 0.32f;
        [Tooltip("Starting grip offset in camera space for the owner, aim space for other players.")]
        [SerializeField] private Vector3 equipPositionOffset = new(0.03f, -0.24f, 0f);
        [SerializeField] private Vector3 equipRotationOffset = new(25f, -10f, -12f);

        [Header("Sway")]
        [SerializeField] private bool enableSway = true;
        [SerializeField] private Vector2 swayMaxOffset = new(0.015f, 0.01f);
        [SerializeField] private Vector2 swayAmount = new(0.001f, 0.0006f);
        [SerializeField] private float swayReturnSpeed = 8f;

        [Header("Walking Bob")]
        [SerializeField] private bool enableWalkBob = true;
        [SerializeField] private Vector3 walkBobPosition = new(0.006f, 0.008f, 0.003f);
        [SerializeField] private Vector3 walkBobRotation = new(0.35f, 0.45f, 0.65f);
        [Tooltip("Distance travelled for a full left/right sway cycle, in metres.")]
        [SerializeField, Min(0.1f)] private float walkBobStrideLength = 7.5f;
        [Tooltip("Additional bob frequency multiplier while running; blends without restarting the stride.")]
        [SerializeField, Min(1f)] private float runBobFrequencyMultiplier = 1.45f;
        [SerializeField, Min(0.01f)] private float walkBobBlendSpeed = 10f;

        public bool ClampItemPitch { get => clampItemPitch; set => clampItemPitch = value; }
        public float MinItemPitch { get => minItemPitch; set => minItemPitch = value; }
        public float MaxItemPitch { get => maxItemPitch; set => maxItemPitch = value; }
        public Transform HeldItemSocket => heldItemSocket;
        public bool RequiresFirstPersonReach { get; private set; }

        private Item currentActiveItem;
        private Vector2 currentSway;
        private Vector3 currentWalkPosition;
        private Vector3 currentWalkRotation;
        private float walkPhase;
        private float currentBobFrequencyMultiplier = 1f;
        private float actionWeight;
        private float equipStartedAt;
        private bool wasInteracting;
        private Pose interactionPose;

        public void SetActiveItem(Item item)
        {
            if (currentActiveItem == item) return;
            currentActiveItem = item;
            equipStartedAt = Time.time;
            RequiresFirstPersonReach = false;
            actionWeight = item != null && item.IsHoldActionActive ? 1f : 0f;
            currentSway = Vector2.zero;
            ResetWalkBob();
        }

        private void OnDisable()
        {
            ResetWalkBob();
        }

        private void ResetWalkBob()
        {
            currentWalkPosition = Vector3.zero;
            currentWalkRotation = Vector3.zero;
            walkPhase = 0f;
            currentBobFrequencyMultiplier = 1f;
        }

        private void EvaluateWalkBob(bool interacting, float heldActionWeight)
        {
            Vector3 targetPosition = Vector3.zero;
            Vector3 targetRotation = Vector3.zero;
            bool canBob = enableWalkBob && movement != null && movement.isActiveAndEnabled &&
                !movement.isPaused && !interacting && movement.IsGrounded &&
                (movement.human == null || (!movement.human.isDead.Value &&
                    !movement.human.IsBeingCaptured && !movement.human.IsEffectActive(EffectType.Freeze)));
            float smoothing = 1f - Mathf.Exp(-walkBobBlendSpeed * Time.deltaTime);
            float targetFrequencyMultiplier = canBob && movement.IsRunning ? Mathf.Max(1f, runBobFrequencyMultiplier) : 1f;
            currentBobFrequencyMultiplier = Mathf.Lerp(currentBobFrequencyMultiplier, targetFrequencyMultiplier, smoothing);
            if (canBob)
            {
                Vector3 velocity = movement.Velocity;
                float speed = new Vector2(velocity.x, velocity.z).magnitude;
                if (speed > 0.15f)
                {
                    // Actual travel keeps pushing against a wall from playing a walk cycle.
                    walkPhase = (walkPhase + speed * currentBobFrequencyMultiplier * Time.deltaTime * Mathf.PI * 2f /
                        Mathf.Max(0.1f, walkBobStrideLength)) % (Mathf.PI * 2f);
                    float weight = Mathf.Clamp(speed / 7.5f, 0f, 1.2f) *
                        (movement.IsCrouching ? 0.55f : 1f) * Mathf.Lerp(1f, 0.35f, heldActionWeight);
                    float lateral = Mathf.Sin(walkPhase);
                    float step = Mathf.Sin(walkPhase * 2f);
                    targetPosition = new Vector3(lateral * walkBobPosition.x,
                        step * walkBobPosition.y, -step * walkBobPosition.z) * weight;
                    targetRotation = new Vector3(step * walkBobRotation.x,
                        lateral * walkBobRotation.y, -lateral * walkBobRotation.z) * weight;
                }
            }

            currentWalkPosition = Vector3.Lerp(currentWalkPosition, targetPosition, smoothing);
            currentWalkRotation = Vector3.Lerp(currentWalkRotation, targetRotation, smoothing);
        }

        public void EvaluateViewModelAndIKTarget(Transform shoulder, float minReach, float maxReach)
        {
            EvaluatePose(true, shoulder, minReach, maxReach, cameraMovement != null ? cameraMovement.CurrentPitch : 0f);
        }

        public void EvaluateRemotePose(Transform shoulder, float minReach, float maxReach, float pitch)
        {
            EvaluatePose(false, shoulder, minReach, maxReach, pitch);
        }

        private void EvaluatePose(bool firstPerson, Transform shoulder, float minReach, float maxReach, float pitch)
        {
            RequiresFirstPersonReach = false;
            if (inventory == null || inventory.HandAnchor == null || handTarget == null ||
                currentActiveItem == null || shoulder == null || (firstPerson && cameraTransform == null)) return;

            actionWeight = Mathf.MoveTowards(actionWeight, currentActiveItem.IsHoldActionActive ? 1f : 0f,
                Time.deltaTime / currentActiveItem.PoseBlendDuration);
            float blend = Mathf.SmoothStep(0f, 1f, actionWeight);
            Pose offset = currentActiveItem.GetHeldPoseOffset(firstPerson, blend);
            // Apply before reach/camera correction so the hand and handle stay together.
            float equipProgress = Mathf.Clamp01((Time.time - equipStartedAt) / Mathf.Max(0.01f, equipDuration));
            float loweredWeight = 1f - Mathf.SmoothStep(0f, 1f, equipProgress);
            offset.position += equipPositionOffset * loweredWeight;
            offset.rotation = Quaternion.Slerp(Quaternion.identity,
                Quaternion.Euler(equipRotationOffset), loweredWeight) * offset.rotation;
            float clampedPitch = clampItemPitch ? Mathf.Clamp(pitch, minItemPitch, maxItemPitch) : pitch;
            Vector3 gripPosition;
            Quaternion gripRotation;

            if (firstPerson)
            {
                bool interacting = movement != null && movement.isInInteraction;
                if (interacting && !wasInteracting && heldItemSocket != null)
                {
                    interactionPose = new Pose(inventory.transform.InverseTransformPoint(heldItemSocket.position),
                        Quaternion.Inverse(inventory.transform.rotation) * heldItemSocket.rotation);
                }
                wasInteracting = interacting;
                Vector2 targetSway = Vector2.zero;
                if (!interacting && enableSway && movement != null && movement.controls != null)
                {
                    Vector2 look = movement.controls.Gameplay.Look.ReadValue<Vector2>();
                    targetSway = new Vector2(Mathf.Clamp(-look.x * swayAmount.x, -swayMaxOffset.x, swayMaxOffset.x),
                        Mathf.Clamp(-look.y * swayAmount.y, -swayMaxOffset.y, swayMaxOffset.y));
                }
                currentSway = Vector2.Lerp(currentSway, targetSway, 1f - Mathf.Exp(-swayReturnSpeed * Time.deltaTime));
                EvaluateWalkBob(interacting, blend);
                gripPosition = cameraTransform.TransformPoint(holdPositionLocal + offset.position +
                    new Vector3(currentSway.x, currentSway.y, 0f) + currentWalkPosition);
                gripRotation = cameraTransform.rotation * Quaternion.Euler(clampedPitch - pitch, 0f, 0f) *
                    Quaternion.Euler(holdRotationEuler) * offset.rotation *
                    Quaternion.Euler(currentSway.y * 60f, currentSway.x * 60f, -currentSway.x * 80f) *
                    Quaternion.Euler(currentWalkRotation);
                if (interacting)
                {
                    gripPosition = inventory.transform.TransformPoint(interactionPose.position);
                    gripRotation = inventory.transform.rotation * interactionPose.rotation;
                }
                if (heldItemSocket != null) heldItemSocket.SetPositionAndRotation(gripPosition, gripRotation);
            }
            else
            {
                Quaternion aim = inventory.transform.rotation * Quaternion.Euler(clampedPitch, 0f, 0f);
                gripPosition = shoulder.position + aim * (remotePositionFromShoulder + offset.position);
                gripRotation = aim * Quaternion.Euler(holdRotationEuler) * offset.rotation;
            }

            Quaternion wristRotation = gripRotation * Quaternion.Euler(handGripRotationOffset);
            Vector3 palmOffset = Vector3.Scale(palmPositionLocal, inventory.HandAnchor.lossyScale);
            Vector3 wristPosition = gripPosition - wristRotation * palmOffset;
            Vector3 reach = wristPosition - shoulder.position;
            float distance = reach.magnitude;
            bool protectCamera = firstPerson && (movement == null || !movement.isInInteraction);
            if (!protectCamera && distance > 0.00001f)
                wristPosition = shoulder.position + reach * (Mathf.Clamp(distance, minReach, maxReach) / distance);

            if (protectCamera)
            {
                // Keep the whole item ahead of the camera; the local arm solve follows
                // the corrected grip without detaching the handle.
                Vector3 actualGrip = wristPosition + wristRotation * palmOffset;
                float depth = Vector3.Dot(actualGrip - cameraTransform.position, cameraTransform.forward);
                float minimumDepth = currentActiveItem.GetMinimumGripCameraDepth(gripRotation, cameraTransform.forward, cameraClearance);
                wristPosition += cameraTransform.forward * Mathf.Max(0f, minimumDepth - depth);
                RequiresFirstPersonReach = true;
                if (heldItemSocket != null)
                    heldItemSocket.SetPositionAndRotation(wristPosition + wristRotation * palmOffset, gripRotation);
            }
            handTarget.SetPositionAndRotation(wristPosition, wristRotation);
        }

        public void AlignItemToHand(Item item)
        {
            if (inventory == null || inventory.HandAnchor == null || !inventory.IsActiveItem(item)) return;
            Transform hand = inventory.HandAnchor;
            Quaternion gripRotation = hand.rotation * Quaternion.Inverse(Quaternion.Euler(handGripRotationOffset));
            item.AlignToGripPose(hand.TransformPoint(palmPositionLocal), gripRotation);
        }
    }
}

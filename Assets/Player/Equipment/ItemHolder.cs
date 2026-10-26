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
        [SerializeField] private bool clampItemPitch = true;
        [SerializeField] private float minItemPitch = -45f;
        [SerializeField] private float maxItemPitch = 45f;

        [Header("Sway")]
        [SerializeField] private bool enableSway = true;
        [SerializeField] private Vector2 swayMaxOffset = new(0.015f, 0.01f);
        [SerializeField] private Vector2 swayAmount = new(0.001f, 0.0006f);
        [SerializeField] private float swayReturnSpeed = 8f;

        public bool ClampItemPitch { get => clampItemPitch; set => clampItemPitch = value; }
        public float MinItemPitch { get => minItemPitch; set => minItemPitch = value; }
        public float MaxItemPitch { get => maxItemPitch; set => maxItemPitch = value; }
        public Transform HeldItemSocket => heldItemSocket;

        private Item currentActiveItem;
        private Vector2 currentSway;
        private float actionWeight;
        private bool wasInteracting;
        private Pose interactionPose;

        public void SetActiveItem(Item item)
        {
            if (currentActiveItem == item) return;
            currentActiveItem = item;
            actionWeight = item != null && item.IsHoldActionActive ? 1f : 0f;
            currentSway = Vector2.zero;
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
            if (inventory == null || inventory.HandAnchor == null || handTarget == null ||
                currentActiveItem == null || shoulder == null || (firstPerson && cameraTransform == null)) return;

            actionWeight = Mathf.MoveTowards(actionWeight, currentActiveItem.IsHoldActionActive ? 1f : 0f,
                Time.deltaTime / currentActiveItem.PoseBlendDuration);
            float blend = Mathf.SmoothStep(0f, 1f, actionWeight);
            Pose offset = currentActiveItem.GetHeldPoseOffset(firstPerson, blend);
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
                gripPosition = cameraTransform.TransformPoint(holdPositionLocal + offset.position +
                    new Vector3(currentSway.x, currentSway.y, 0f));
                gripRotation = cameraTransform.rotation * Quaternion.Euler(clampedPitch - pitch, 0f, 0f) *
                    Quaternion.Euler(holdRotationEuler) * offset.rotation *
                    Quaternion.Euler(currentSway.y * 60f, currentSway.x * 60f, -currentSway.x * 80f);
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
            if (distance > 0.00001f)
                wristPosition = shoulder.position + reach * (Mathf.Clamp(distance, minReach, maxReach) / distance);
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

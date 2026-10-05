using Unity.Netcode;
using UnityEngine;

namespace Player.Movement
{
    /// <summary>
    /// Procedural first-person head-bob — purely a client-side visual effect.
    ///
    /// ── Scene Hierarchy Required ────────────────────────────────────────────
    ///
    ///   [PlayerRoot]  (CharacterController + PlayerMovement)
    ///   └─ [PlayerBody]
    ///      └─ [CameraHolder]        ← CameraMovement lives here; drives mouse look
    ///         └─ [BobPivot]         ← assign to bobPivot; THIS script drives localPosition + tilt
    ///            └─ [PlayerCamera]  ← Camera + PlayerProximityCameraFX (jitter offset on top)
    ///
    /// ── Inspector Checklist ─────────────────────────────────────────────────
    ///   1. Add PlayerCameraBob to CameraHolder (same GO as CameraMovement).
    ///   2. Create an empty child "BobPivot" under CameraHolder; assign it.
    ///   3. Move [PlayerCamera] to be a child of [BobPivot].
    ///   4. Update CameraMovement.startPosition to match the camera's new
    ///      local position relative to BobPivot (usually Vector3.zero).
    /// ────────────────────────────────────────────────────────────────────────
    /// </summary>
    public class PlayerCameraBob : NetworkBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("Intermediate transform that receives bob offsets. " +
                 "Must be a child of CameraHolder and a parent of the Camera.")]
        [SerializeField] private Transform bobPivot;

        [Tooltip("CharacterController on the player root — used for velocity and isGrounded.")]
        [SerializeField] private CharacterController characterController;

        [Header("Walk Settings")]
        [SerializeField] private BobSettings walk = new BobSettings
        {
            frequency   = 1.8f,
            amplitudeY  = 0.0055f,
            amplitudeX  = 0.0028f,
            amplitudeZ  = 0.35f,
        };

        [Header("Run Settings")]
        [SerializeField] private BobSettings run = new BobSettings
        {
            frequency   = 2.6f,
            amplitudeY  = 0.009f,
            amplitudeX  = 0.005f,
            amplitudeZ  = 0.6f,
        };

        [Header("Smoothing")]
        [Tooltip("How quickly the bob blends in and out (higher = snappier).")]
        [SerializeField] [Range(1f, 20f)] private float returnSpeed = 8f;

        [Tooltip("Minimum horizontal speed (m/s) before bobbing starts.")]
        [SerializeField] private float minSpeedThreshold = 0.15f;

        // ─────────────────────────────────────────────────────────────────────
        //  Private state
        // ─────────────────────────────────────────────────────────────────────

        // Accumulated phase timer — advances proportional to speed, preventing
        // phase jumps when the player stops and restarts.
        private float _bobTimer;

        // Cached reference to PlayerMovement for run-state query (set once).
        private PlayerMovement _movement;

        // ─────────────────────────────────────────────────────────────────────
        //  NetworkBehaviour lifecycle
        // ─────────────────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            // Head-bob is a local visual effect — disable entirely for remote players.
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            // Auto-resolve CharacterController if not assigned in the Inspector.
            if (characterController == null)
                characterController = GetComponentInParent<CharacterController>();

            // Auto-resolve PlayerMovement for run detection.
            _movement = GetComponentInParent<PlayerMovement>();

            if (bobPivot == null)
                Debug.LogError("[PlayerCameraBob] bobPivot is not assigned! " +
                               "Create an empty child 'BobPivot' under CameraHolder and assign it.");
        }

        public override void OnNetworkDespawn()
        {
            // Reset pivot so the camera returns to neutral on disconnect.
            if (IsOwner && bobPivot != null)
                bobPivot.localPosition = Vector3.zero;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Per-frame (LateUpdate keeps bob applied after CameraMovement's Update)
        // ─────────────────────────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (bobPivot == null || characterController == null) return;

            // ── Horizontal speed (ignores vertical velocity for air phases) ──
            Vector3 vel       = characterController.velocity;
            float horizontalSpeed = new Vector3(vel.x, 0f, vel.z).magnitude;

            bool isGrounded = characterController.isGrounded;
            bool isMoving   = isGrounded && horizontalSpeed > minSpeedThreshold;

            if (isMoving)
                ApplyBob(horizontalSpeed);
            else
                ReturnToNeutral();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Core math
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyBob(float horizontalSpeed)
        {
            bool isRunning = _movement != null && _movement.IsRunning;
            BobSettings s  = isRunning ? run : walk;

            // Speed ratio drives both the blend weight and the timer advance,
            // so faster movement produces proportionally faster oscillation.
            float speedRatio = Mathf.Clamp01(horizontalSpeed / Mathf.Max(
                isRunning ? 10f : 7.5f, 0.01f));         // normalised 0–1

            // Timer advances proportionally to speed — avoids phase snapping.
            _bobTimer += Time.deltaTime * s.frequency * speedRatio;

            // ── Position offsets ────────────────────────────────────────────
            // Y: doubles the frequency so there is a dip on every footstep.
            float offsetY = Mathf.Sin(_bobTimer * Mathf.PI * 2f) * s.amplitudeY * speedRatio;

            // X: lateral weight-shift, half the frequency of Y.
            float offsetX = Mathf.Cos(_bobTimer * Mathf.PI) * s.amplitudeX * speedRatio;

            bobPivot.localPosition = new Vector3(offsetX, offsetY, 0f);

            // ── Roll / tilt on Z ─────────────────────────────────────────────
            // Follows the X lateral offset so the camera leans into each step.
            float tiltZ = -offsetX / Mathf.Max(s.amplitudeX, 0.0001f) * s.amplitudeZ * speedRatio;
            bobPivot.localEulerAngles = new Vector3(0f, 0f, tiltZ);
        }

        private void ReturnToNeutral()
        {
            // Smoothly lerp position and tilt back to zero — no abrupt snapping.
            float t = returnSpeed * Time.deltaTime;
            bobPivot.localPosition    = Vector3.Lerp(bobPivot.localPosition, Vector3.zero, t);

            Vector3 angles = bobPivot.localEulerAngles;
            // Convert Z from 0–360 range to signed -180…180 before lerping.
            float signedZ  = angles.z > 180f ? angles.z - 360f : angles.z;
            float newZ     = Mathf.Lerp(signedZ, 0f, t);
            bobPivot.localEulerAngles = new Vector3(0f, 0f, newZ);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Editor gizmo
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (bobPivot == null) return;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireSphere(bobPivot.position, 0.04f);
            UnityEditor.Handles.Label(bobPivot.position + Vector3.up * 0.06f, "BobPivot");
        }
#endif
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Data container — serialisable inline in the Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [System.Serializable]
    public struct BobSettings
    {
        [Tooltip("Oscillations per second at full speed.")]
        public float frequency;

        [Tooltip("Max vertical displacement (m) at full speed.")]
        public float amplitudeY;

        [Tooltip("Max lateral displacement (m) at full speed.")]
        public float amplitudeX;

        [Tooltip("Max camera roll (degrees) at full speed.")]
        public float amplitudeZ;
    }
}

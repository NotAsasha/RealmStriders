using Steamworks;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    [RequireComponent(typeof(CharacterController))]
    public class Movement : NetworkBehaviour
    {
        [Header("Movement")]
        CharacterController player;
        [SerializeField] private float moveSpeed = 7.5f;
        [SerializeField] private float runSpeed = 10f;
        [SerializeField] private float gravity = Physics.gravity.y;
        [SerializeField] private float jumpHeight = 1f;

        [Header("Keybinds")]
        public KeyCode jumpKey = KeyCode.Space;

        [Header("Ground Check")]
        public float playerHeight = 1.75f;
        public LayerMask whatIsGround;

        [Header("Other Settings")]
        public bool isNetwork = true;

        private bool isGrounded;
        private Vector3 velocity = new();
        private float currentSpeed;
        private Transform playerTransform;

        private const KeyCode EscapeKey = KeyCode.Escape;
        public bool isPaused = false;
        void Start()
        {
            // Checks of you are owner of this Network Object and if Network is turned on
            if (!IsOwner && isNetwork) enabled = false;

            Application.targetFrameRate = 240;
            player = GetComponent<CharacterController>();
            playerTransform = transform;
        }

        void FixedUpdate()
        {
            // Pauses the player -- In future should be moved to the separated class
            if (Input.GetKeyDown(EscapeKey)) isPaused = !isPaused;
            if (Input.GetMouseButton(0)) isPaused = false;
            if (isPaused) return;

            isGrounded = Physics.Raycast(playerTransform.position, Vector3.down, playerHeight * 0.5f + 0.3f, whatIsGround);
            if (isGrounded && velocity.y < 0) velocity.y = 0f;

            // Calculate movement direction and speed
            Vector3 move = playerTransform.right * Input.GetAxis("Horizontal") + playerTransform.forward * Input.GetAxis("Vertical");
            if (move.magnitude > 1) move.Normalize();

            // Set the current speed based on whether the run key is pressed
            currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : moveSpeed;

            // Calculate the final position using the movement direction and speed
            Vector3 finalPosition = currentSpeed * move + velocity;

            // Apply the final position to the character controller
            player.Move(finalPosition * Time.deltaTime);

            // Handle jumping
            if (Input.GetKey(jumpKey) && isGrounded)
            {
                velocity.y = Mathf.Sqrt(2f * jumpHeight * Mathf.Abs(gravity));
            }

            // Apply gravity to the velocity
            velocity.y += gravity * Time.deltaTime;
        }
    }
}
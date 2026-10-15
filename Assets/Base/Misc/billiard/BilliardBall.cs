using Unity.Netcode;
using UnityEngine;

namespace Base.Billiard
{
    /// <summary>
    /// Контролер фізики більярдної кулі.
    /// Налаштовує Rigidbody, стабілізує кочення по столу та плавно зупиняє кулю після руху.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider), typeof(NetworkObject))]
    public class BilliardBall : NetworkBehaviour
    {
        [Header("Rigidbody Configuration")]
        [SerializeField, Min(0.01f)] private float ballMass = 0.17f;
        [SerializeField, Min(0f)] private float linearDamping = 0.45f;
        [SerializeField, Min(0f)] private float angularDamping = 0.35f;
        [SerializeField, Min(10f)] private float maxAngularVelocity = 150f;

        [Header("Table Stabilization")]
        [SerializeField] private bool suppressVerticalBounce = true;
        [SerializeField, Min(0.01f)] private float maxUpwardSpeed = 0.12f;
        [Tooltip("Наскільки нижче стартової точки має впасти куля (наприклад, на підлогу), щоб повернутися на стіл.")]
        [SerializeField] private float minVerticalDrop = -0.6f;

        [Header("Smooth Stop")]
        [SerializeField, Min(0.001f)] private float stopLinearSpeed = 0.035f;
        [SerializeField, Min(0.01f)] private float stopAngularSpeed = 0.4f;
        [Tooltip("Час (сек), протягом якого куля має рухатися повільніше порогу перед повною зупинкою.")]
        [SerializeField, Min(0.05f)] private float stopDelay = 0.25f;
        [SerializeField, Min(1f)] private float settleDampingRate = 8f;

        private Rigidbody rb;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private bool spawnPointRecorded;
        private float lowSpeedTimer;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            ConfigureRigidbody();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                ActivateServerPhysics();
                RecordSpawnPoint();
                Billiard.Balls.Add(this);
            }
        }
        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                Billiard.Balls.Remove(this);
                base.OnNetworkSpawn();
            }
        }
        private void Start()
        {
            // Якщо сцену запущено або стіл створено, гарантуємо фіксацію стартової точки
            if (!spawnPointRecorded)
            {
                RecordSpawnPoint();
            }

            if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer)
            {
                ActivateServerPhysics();
            }
        }

        private void ConfigureRigidbody()
        {
            if (rb == null) return;

            rb.mass = ballMass;
            rb.linearDamping = linearDamping;
            rb.angularDamping = angularDamping;
            rb.maxAngularVelocity = maxAngularVelocity;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void ActivateServerPhysics()
        {
            if (rb == null) return;

            rb.isKinematic = false;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.None;
            rb.WakeUp();
        }

        private void RecordSpawnPoint()
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            spawnPointRecorded = true;
        }

        private void FixedUpdate()
        {
            // Працюємо на сервері (або локально, якщо мережа ще не піднята)
            bool hasAuthority = !IsSpawned || IsServer;
            if (!hasAuthority || rb == null || rb.isKinematic) return;

            if (rb.IsSleeping())
            {
                lowSpeedTimer = 0f;
                return;
            }

            StabilizeVerticalMotion();
            HandleLowSpeedStop();
        }

        private void StabilizeVerticalMotion()
        {
            if (!suppressVerticalBounce) return;

            Vector3 vel = rb.linearVelocity;
            if (vel.y > maxUpwardSpeed)
            {
                vel.y = maxUpwardSpeed;
                rb.linearVelocity = vel;
            }
        }

        private void HandleLowSpeedStop()
        {
            Vector3 linVel = rb.linearVelocity;
            Vector3 angVel = rb.angularVelocity;

            // Якщо куля падає у повітрі — не втручаємось у гравітацію
            if (Mathf.Abs(linVel.y) > 0.03f)
            {
                lowSpeedTimer = 0f;
                return;
            }

            float linSpeedSq = linVel.sqrMagnitude;
            float angSpeedSq = angVel.sqrMagnitude;

            // Ігноруємо вже повністю нерухому кулю, щоб не блокувати старт руху
            if (linSpeedSq < 0.000001f && angSpeedSq < 0.00001f)
            {
                lowSpeedTimer = 0f;
                return;
            }

            bool isBelowThreshold =
                linSpeedSq < (stopLinearSpeed * stopLinearSpeed) &&
                angSpeedSq < (stopAngularSpeed * stopAngularSpeed);

            if (!isBelowThreshold)
            {
                lowSpeedTimer = 0f;
                return;
            }

            lowSpeedTimer += Time.fixedDeltaTime;
            if (lowSpeedTimer < stopDelay) return;

            rb.linearVelocity = Vector3.MoveTowards(
                linVel,
                Vector3.zero,
                settleDampingRate * Time.fixedDeltaTime);

            rb.angularVelocity = Vector3.MoveTowards(
                angVel,
                Vector3.zero,
                settleDampingRate * 10f * Time.fixedDeltaTime);
        }

        public void ResetToSpawnServer()
        {
            if (rb == null) return;

            lowSpeedTimer = 0f;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = spawnPosition + Vector3.up * 0.02f;
            rb.rotation = spawnRotation;
            transform.SetPositionAndRotation(rb.position, rb.rotation);
            rb.WakeUp();
        }
    }
}
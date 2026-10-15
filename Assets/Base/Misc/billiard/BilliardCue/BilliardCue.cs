using Player.Movement;
using Unity.Netcode;
using UnityEngine;

namespace Player.Equipment.BilliardCue
{
    /// <summary>
    /// Предмет більярдного кия.
    /// Утримання кнопки Use накопичує силу удару, відпускання — наносить планарний удар по кулі під прицілом.
    /// </summary>
    public class BilliardCue : Item
    {
        [Header("Cue Settings")]
        [SerializeField, Min(0.1f)]  private float maxReach       = 3.5f;
        [Tooltip("Радіус допоміжного променя прицілювання (полегшує влучання по малих кулях).")]
        [SerializeField, Min(0.005f)] private float aimAssistRadius = 0.035f;
        [Tooltip("Мінімальна початкова швидкість кулі при легкому ударі (м/с).")]
        [SerializeField, Min(0.1f)]  private float minLaunchSpeed = 1.5f;
        [Tooltip("Максимальна початкова швидкість кулі при повному заряді (м/с).")]
        [SerializeField, Min(0.5f)]  private float maxLaunchSpeed = 11f;
        [SerializeField, Min(0.1f)]  private float chargeTime     = 1.2f;
        [SerializeField, Min(0f)]    private float cooldown       = 0.5f;

        [Header("Billiard Physics & Spin")]
        [Tooltip("Інтенсивність кручення (topspin / backspin / sidespin) при ударі не в центр кулі.")]
        [SerializeField, Min(0f)] private float spinFactor = 1.5f;
        [Tooltip("Частка початкового прямого кочення вперед (1 = чисте кочення без ковзання).")]
        [SerializeField, Range(0f, 1.5f)] private float naturalRollRatio = 0.85f;
        [SerializeField] private LayerMask tableSurfaceMask = ~0;

        [Header("Jump Shot Settings")]
        [SerializeField] private bool allowJumpShots = false;
        [SerializeField, Range(0.1f, 0.9f)] private float minDownwardDotForJump = 0.5f;
        [SerializeField, Range(0f, 0.5f)]   private float jumpVelocityFactor    = 0.15f;

        [Header("Layers")]
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Arrow Indicator")]
        [SerializeField] private Transform arrow;
        [SerializeField] private float arrowMinAngle = -60f;
        [SerializeField] private float arrowMaxAngle =  60f;
        [SerializeField, Min(0.01f)] private float arrowSmoothTime = 0.12f;

        [Header("Hit Particles")]
        [SerializeField] private ParticleSystem hitParticlePrefab;
        [SerializeField, Min(0f)] private float hitParticleLifetime = 3f;

        [Header("Audio")]
        [SerializeField] private AudioClip chargeStartClip;
        [SerializeField] private AudioClip hitClip;

        // Буфер без алокацій пам'яті для відсіювання колайдерів гравця та самого кия
        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

        private bool  isCharging;
        private float chargeStartTime;
        private float nextFireTime;

        private float arrowCurrentAngle;
        private float arrowTargetAngle;
        private float arrowVelocity;
        private float initialArrowRotY;
        private float initialArrowRotZ;

        /// <summary>
        /// Перевіряє, чи цей кий зараз у руках локального гравця (враховує архітектуру Inventory.IsOwner).
        /// </summary>
        private bool IsHeldByLocalPlayer
        {
            get
            {
                if (TryGetHolderInventory(out var inv))
                    return inv.IsOwner;

                Inventory parentInv = GetComponentInParent<Inventory>();
                if (parentInv != null)
                    return parentInv.IsOwner;

                return IsOwner;
            }
        }

        private bool UseButtonHeld =>
            IsHeldByLocalPlayer &&
            PlayerMovement.Instance?.controls != null &&
            PlayerMovement.Instance.controls.Gameplay.Use.IsPressed();

        public float ChargeNormalized =>
            isCharging ? Mathf.Clamp01((Time.time - chargeStartTime) / chargeTime) : 0f;

        private void Start()
        {
            if (arrow != null)
            {
                Vector3 rot = arrow.localEulerAngles;
                arrowCurrentAngle = Mathf.DeltaAngle(0f, rot.x);
                arrowTargetAngle  = arrowCurrentAngle;
                initialArrowRotY  = rot.y;
                initialArrowRotZ  = rot.z;
            }
        }

        protected override void ExecuteItemAction(GameObject player)
        {
            if (isCharging) return;
            if (Time.time < nextFireTime) return;

            // Починаємо заряджати удар одразу при натисканні ЛКМ (прицілитися в кулю можна під час замаху)
            isCharging = true;
            chargeStartTime = Time.time;
            PlayOneShot(chargeStartClip);
        }

        public override void TurnOffServer() => isCharging = false;

        public override void OnUnequip()
        {
            isCharging = false;
            base.OnUnequip();
        }

        private void Update()
        {
            UpdateArrow();

            if (!IsHeldByLocalPlayer || !isCharging) return;
            if (UseButtonHeld) return;

            FireShot();
        }

        private void UpdateArrow()
        {
            if (arrow == null) return;

            float charge = IsHeldByLocalPlayer ? ChargeNormalized : 0f;
            arrowTargetAngle = Mathf.Lerp(arrowMinAngle, arrowMaxAngle, charge);

            arrowCurrentAngle = Mathf.SmoothDamp(
                arrowCurrentAngle,
                arrowTargetAngle,
                ref arrowVelocity,
                arrowSmoothTime);

            arrow.localRotation = Quaternion.Euler(
                arrowCurrentAngle,
                initialArrowRotY,
                initialArrowRotZ);
        }

        private void FireShot()
        {
            float normalizedCharge = ChargeNormalized;
            isCharging = false;

            if (!TryGetBallInSight(out RaycastHit hit, out Rigidbody targetRb)) return;

            Transform cam = CameraMovement.Instance.transform;
            float launchSpeed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, normalizedCharge);

            nextFireTime = Time.time + cooldown;

            PlayOneShot(hitClip);
            SpawnHitParticle(hit.point, hit.normal);

            // Якщо на кулі є заспавнений NetworkObject — відправляємо серверний RPC
            NetworkObject netObj = targetRb.GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                StrikeServerRpc(netObj, cam.forward, launchSpeed, hit.point, hit.normal);
            }
            else
            {
                // Fallback, якщо куля на сцені не має NetworkObject або тестується локально
                ApplyStrikePhysics(targetRb, cam.forward, launchSpeed, hit.point);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void StrikeServerRpc(
            NetworkObjectReference targetRef,
            Vector3 rawViewDirection,
            float launchSpeed,
            Vector3 hitPoint,
            Vector3 hitNormal,
            RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!ValidateSenderHoldsCue(senderId)) return;
            if (!targetRef.TryGet(out NetworkObject targetNetObj)) return;

            Rigidbody rb = targetNetObj.GetComponent<Rigidbody>();
            if (rb == null) rb = targetNetObj.GetComponentInChildren<Rigidbody>();
            if (rb == null) return;

            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(senderId, out var client) &&
                client.PlayerObject != null)
            {
                float distSqr    = (rb.position - client.PlayerObject.transform.position).sqrMagnitude;
                float maxReachSq = (maxReach * 2.5f) * (maxReach * 2.5f);
                if (distSqr > maxReachSq) return;
            }

            ApplyStrikePhysics(rb, rawViewDirection, launchSpeed, hitPoint);
            BroadcastHitFxRpc(senderId, hitPoint, hitNormal);
        }

        private void ApplyStrikePhysics(
            Rigidbody rb,
            Vector3 rawViewDirection,
            float launchSpeed,
            Vector3 hitPoint)
        {
            if (rb.isKinematic) rb.isKinematic = false;

            // 1. Визначаємо нормаль площини столу під кулею
            Vector3 tableNormal = GetSurfaceNormalUnderBall(rb);

            // 2. Проєктуємо погляд камери на площину столу
            Vector3 planarDirection = Vector3.ProjectOnPlane(rawViewDirection, tableNormal);
            if (planarDirection.sqrMagnitude < 0.0001f)
            {
                planarDirection = Vector3.ProjectOnPlane(transform.forward, tableNormal);
            }
            planarDirection.Normalize();

            // 3. Розраховуємо вектор зміни лінійної швидкості
            Vector3 deltaVelocity = planarDirection * launchSpeed;

            if (allowJumpShots)
            {
                float downwardDot = Vector3.Dot(-rawViewDirection.normalized, tableNormal);
                if (downwardDot >= minDownwardDotForJump)
                {
                    deltaVelocity += tableNormal * (launchSpeed * downwardDot * jumpVelocityFactor);
                }
            }

            // 4. Розраховуємо кутову швидкість (природне кочення + кручення від зміщення точки удару)
            float ballRadius = GetBallRadius(rb);
            Vector3 hitOffset = Vector3.ClampMagnitude(hitPoint - rb.worldCenterOfMass, ballRadius);

            // Вісь прямого кочення: n x v / r
            Vector3 forwardRollAxis = Vector3.Cross(tableNormal, planarDirection).normalized;
            Vector3 rollAngularVel  = forwardRollAxis * ((launchSpeed / ballRadius) * naturalRollRatio);

            // Додатковий спін (topspin / backspin / sidespin): (r_offset x v) / r^2
            Vector3 spinAngularVel  = Vector3.Cross(hitOffset, planarDirection * launchSpeed) /
                                      (ballRadius * ballRadius) * spinFactor;

            rb.WakeUp();
            rb.AddForce(deltaVelocity * rb.mass, ForceMode.Impulse);
            rb.AddTorque(rollAngularVel + spinAngularVel, ForceMode.VelocityChange);
        }

        private bool ValidateSenderHoldsCue(ulong senderClientId)
        {
            if (IsHeldByClient(senderClientId) || OwnerClientId == senderClientId) return true;

            Inventory parentInv = GetComponentInParent<Inventory>();
            return parentInv != null && parentInv.OwnerClientId == senderClientId;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void BroadcastHitFxRpc(ulong senderClientId, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (NetworkManager.Singleton.LocalClientId == senderClientId) return;

            PlayOneShot(hitClip);
            SpawnHitParticle(hitPoint, hitNormal);
        }

        /// <summary>
        /// Проникний пошук кулі під прицілом: ігнорує колайдер самого кия в руках, колайдери гравця та статичний стіл.
        /// </summary>
        private bool TryGetBallInSight(out RaycastHit bestHit, out Rigidbody bestRb)
        {
            bestHit = default;
            bestRb  = null;

            if (CameraMovement.Instance == null) return false;

            Transform cam = CameraMovement.Instance.transform;
            Transform playerRoot = PlayerMovement.Instance != null
                ? PlayerMovement.Instance.transform.root
                : transform.root;

            // Спочатку точний промінь
            int count = Physics.RaycastNonAlloc(
                cam.position,
                cam.forward,
                hitBuffer,
                maxReach,
                hitMask,
                QueryTriggerInteraction.Ignore);

            if (TrySelectClosestValidBall(count, playerRoot, out bestHit, out bestRb))
                return true;

            // Якщо точний промінь пройшов на міліметр повз кулю — перевіряємо тонкий SphereCast
            count = Physics.SphereCastNonAlloc(
                cam.position,
                aimAssistRadius,
                cam.forward,
                hitBuffer,
                maxReach,
                hitMask,
                QueryTriggerInteraction.Ignore);

            return TrySelectClosestValidBall(count, playerRoot, out bestHit, out bestRb);
        }

        private bool TrySelectClosestValidBall(
            int hitCount,
            Transform playerRoot,
            out RaycastHit bestHit,
            out Rigidbody bestRb)
        {
            bestHit = default;
            bestRb  = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = hitBuffer[i];
                if (candidate.collider == null) continue;

                Transform hitTransform = candidate.collider.transform;

                // Ігноруємо сам кий та тіло гравця
                if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
                if (playerRoot != null && (hitTransform == playerRoot || hitTransform.IsChildOf(playerRoot))) continue;

                Rigidbody rb = candidate.rigidbody;
                if (rb == null) rb = candidate.collider.GetComponentInParent<Rigidbody>();

                // Ігноруємо об'єкти без Rigidbody та предмети в руках (isKinematic)
                if (rb == null || rb.isKinematic) continue;
                if (rb.TryGetComponent<Item>(out var item) && item.isCurrentlyHeld) continue;

                if (candidate.distance < closestDist)
                {
                    closestDist = candidate.distance;
                    bestHit     = candidate;
                    bestRb      = rb;
                }
            }

            return bestRb != null;
        }

        private Vector3 GetSurfaceNormalUnderBall(Rigidbody rb)
        {
            float checkDist = GetBallRadius(rb) * 3f;
            if (Physics.Raycast(
                    rb.worldCenterOfMass,
                    Vector3.down,
                    out RaycastHit groundHit,
                    checkDist,
                    tableSurfaceMask,
                    QueryTriggerInteraction.Ignore))
            {
                if (groundHit.rigidbody != rb && groundHit.normal.y > 0.5f)
                {
                    return groundHit.normal;
                }
            }
            return Vector3.up;
        }

        private static float GetBallRadius(Rigidbody rb)
        {
            if (rb.TryGetComponent<SphereCollider>(out var sphere))
            {
                float maxScale = Mathf.Max(
                    Mathf.Abs(rb.transform.lossyScale.x),
                    Mathf.Abs(rb.transform.lossyScale.y),
                    Mathf.Abs(rb.transform.lossyScale.z));
                return Mathf.Max(0.01f, sphere.radius * maxScale);
            }
            return 0.03f;
        }

        private void SpawnHitParticle(Vector3 point, Vector3 normal)
        {
            if (hitParticlePrefab == null) return;

            Quaternion rotation = normal.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(normal)
                : Quaternion.identity;

            ParticleSystem ps = Instantiate(hitParticlePrefab, point, rotation);
            ps.Play();
            Destroy(ps.gameObject, hitParticleLifetime);
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }
    }
}
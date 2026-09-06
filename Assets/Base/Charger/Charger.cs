using Player.Equipment;
using Unity.Netcode;
using UnityEngine;

public class Charger : NetworkBehaviour
{
    [Header("Charge Settings")]
    [SerializeField] private float chargeDistance = 5.0f;
    [SerializeField] private float chargeInterval = 0.5f;
    [SerializeField] private int chargePerTick = 10;

    [Header("Detection & Layers")]
    [SerializeField] private Transform beamOrigin;
    [SerializeField] private LayerMask itemLayer;
    [SerializeField] private LayerMask wallLayer;

    [Header("Visual & Audio Effects")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private ParticleSystem targetParticles;
    [SerializeField] private AudioSource chargeSound;

    // Мережеве посилання на поточний заряджуваний об'єкт
    private readonly NetworkVariable<NetworkObjectReference> currentTargetNetRef = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly Collider[] nearbyItems = new Collider[20];
    private float tickTimer = 0f;

    // Кешовані локальні посилання для клієнтського рендеру
    private Transform clientTargetTransform;

    private void Awake()
    {
        if (beamOrigin == null) beamOrigin = transform;
        if (lineRenderer != null) lineRenderer.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        currentTargetNetRef.OnValueChanged += OnTargetChanged;
        ResolveTargetTransform(currentTargetNetRef.Value);
    }

    public override void OnNetworkDespawn()
    {
        currentTargetNetRef.OnValueChanged -= OnTargetChanged;
        base.OnNetworkDespawn();
    }

    private void Update()
    {
        // 1. Логіка заряду працює суворо на сервері
        if (IsServer)
        {
            tickTimer += Time.deltaTime;
            if (tickTimer >= chargeInterval)
            {
                tickTimer -= chargeInterval;
                ServerProcessCharging();
            }
        }

        // 2. Оновлення візуалу працює локально на кожному клієнті
        UpdateVisuals();
    }

    private void ServerProcessCharging()
    {
        IChargeable targetItem = GetBestChargeableTarget(out NetworkObject targetNetObj);

        if (targetItem != null && targetNetObj != null)
        {
            targetItem.ModifyCharge(chargePerTick);

            // Якщо предмет зарядився — скидаємо ціль
            if (targetItem.IsFullyCharged)
            {
                currentTargetNetRef.Value = default;
            }
            else
            {
                currentTargetNetRef.Value = targetNetObj;
            }
        }
        else
        {
            currentTargetNetRef.Value = default;
        }
    }

    private IChargeable GetBestChargeableTarget(out NetworkObject bestNetObj)
    {
        bestNetObj = null;
        Vector3 originPos = beamOrigin.position;
        int numColliders = Physics.OverlapSphereNonAlloc(originPos, chargeDistance, nearbyItems, itemLayer);

        IChargeable bestItem = null;
        float closestDistanceSqr = float.MaxValue;

        for (int i = 0; i < numColliders; i++)
        {
            Collider col = nearbyItems[i];
            if (col == null) continue;

            var item = col.GetComponentInParent<IChargeable>();
            if (item == null || item.IsFullyCharged) continue;

            if (!col.TryGetComponent<NetworkObject>(out var netObj))
                netObj = col.GetComponentInParent<NetworkObject>();

            if (netObj == null) continue;

            Vector3 targetPos = col.bounds.center;
            Vector3 direction = targetPos - originPos;
            float distance = direction.magnitude;

            if (Physics.Raycast(originPos, direction.normalized, distance, wallLayer))
                continue;

            float distSqr = direction.sqrMagnitude;
            if (distSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distSqr;
                bestItem = item;
                bestNetObj = netObj;
            }
        }

        return bestItem;
    }

    private void OnTargetChanged(NetworkObjectReference previousValue, NetworkObjectReference newValue)
    {
        ResolveTargetTransform(newValue);
    }

    private void ResolveTargetTransform(NetworkObjectReference targetRef)
    {
        if (targetRef.TryGet(out NetworkObject netObj))
        {
            clientTargetTransform = netObj.transform;
        }
        else
        {
            clientTargetTransform = null;
        }
    }

    private void UpdateVisuals()
    {
        bool isCharging = clientTargetTransform != null;

        if (lineRenderer != null)
        {
            if (isCharging)
            {
                lineRenderer.enabled = true;
                lineRenderer.SetPosition(0, beamOrigin.position);
                lineRenderer.SetPosition(1, clientTargetTransform.position);
            }
            else
            {
                lineRenderer.enabled = false;
            }
        }

        if (targetParticles != null)
        {
            if (isCharging)
            {
                targetParticles.transform.position = clientTargetTransform.position;
                if (!targetParticles.isPlaying) targetParticles.Play();
            }
            else if (targetParticles.isPlaying)
            {
                targetParticles.Stop();
            }
        }

        if (chargeSound != null)
        {
            if (isCharging && !chargeSound.isPlaying) chargeSound.Play();
            else if (!isCharging && chargeSound.isPlaying) chargeSound.Stop();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = beamOrigin != null ? beamOrigin.position : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, chargeDistance);
    }
}
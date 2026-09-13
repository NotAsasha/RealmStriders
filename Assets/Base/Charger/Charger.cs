using Base.BaseUpgrader;
using Player.Equipment;
using Unity.Netcode;
using UnityEngine;

public class Charger : NetworkBehaviour, IPowerConsumer
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

    // Мережеве посилання на цільовий об'єкт
    private readonly NetworkVariable<NetworkObjectReference> currentTargetNetRef = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly Collider[] nearbyItems = new Collider[20];
    private float tickTimer = 0f;

    // Кеш для локального візуалу
    private Transform clientTargetTransform;
    private PowerGrid powerGrid;
    private bool isPowered = true;

    public int PowerDemand => currentTargetNetRef.Value.NetworkObjectId == 0 ? 0 : (int)(chargePerTick / chargeInterval); // Power demand based on charging activity
    public int Priority => 20;
    public bool IsPowered => isPowered;

    private void Awake()
    {
        if (beamOrigin == null) beamOrigin = transform;
        if (lineRenderer != null) lineRenderer.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        powerGrid = BaseManager.Instance != null ? BaseManager.Instance.PowerGrid : null;
        powerGrid?.RegisterConsumer(this);
        currentTargetNetRef.OnValueChanged += OnTargetChanged;
        ResolveTargetTransform(currentTargetNetRef.Value);
    }

    public override void OnNetworkDespawn()
    {
        powerGrid?.UnregisterConsumer(this);
        currentTargetNetRef.OnValueChanged -= OnTargetChanged;
        base.OnNetworkDespawn();
    }

    private void Update()
    {
        // 1. Серверна логіка нарахування заряду
        if (IsServer)
        {
            tickTimer += Time.deltaTime;
            if (tickTimer >= chargeInterval)
            {
                tickTimer -= chargeInterval;
                ServerProcessCharging();
            }
        }

        // 2. Безперервна перевірка посилання на клієнті (страховка від мережевої затримки)
        if (clientTargetTransform == null && currentTargetNetRef.Value.NetworkObjectId != 0)
        {
            ResolveTargetTransform(currentTargetNetRef.Value);
        }

        // 3. Рендер ефектів
        UpdateVisuals();
    }

    private void ServerProcessCharging()
    {
        if (!isPowered)
        {
            currentTargetNetRef.Value = default;
            return;
        }

        IChargeable targetItem = GetBestChargeableTarget(out NetworkObject targetNetObj);

        if (targetItem != null && targetNetObj != null)
        {
            targetItem.ModifyCharge(chargePerTick);

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
            {
                netObj = col.GetComponentInParent<NetworkObject>();
            }

            if (netObj == null || !netObj.IsSpawned) continue;

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
        bool isCharging = isPowered && clientTargetTransform != null && currentTargetNetRef.Value.NetworkObjectId != 0;

        // Керування LineRenderer
        if (lineRenderer != null)
        {
            if (isCharging)
            {
                if (!lineRenderer.enabled) lineRenderer.enabled = true;
                lineRenderer.SetPosition(0, beamOrigin.position);
                lineRenderer.SetPosition(1, clientTargetTransform.position);
            }
            else if (lineRenderer.enabled)
            {
                lineRenderer.enabled = false;
            }
        }

        // Керування частками
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

        // Керування аудіо
        if (chargeSound != null)
        {
            if (isCharging && !chargeSound.isPlaying) chargeSound.Play();
            else if (!isCharging && chargeSound.isPlaying) chargeSound.Stop();
        }
    }

    public void OnPowerStateChanged(bool powered)
    {
        isPowered = powered;

        if (!powered)
        {
            if (IsServer) currentTargetNetRef.Value = default;
            clientTargetTransform = null;
            tickTimer = 0f;

            if (lineRenderer != null) lineRenderer.enabled = false;
            if (targetParticles != null) targetParticles.Stop();
            if (chargeSound != null) chargeSound.Stop();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = beamOrigin != null ? beamOrigin.position : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, chargeDistance);
    }
}
using System.Collections;
using Player;
using Player.Equipment;
using Unity.Netcode;
using UnityEngine;

public class BrokenWires : NetworkBehaviour, ICollidable
{
    [SerializeField] private float freezeDuration = 3f;
    [SerializeField] private float cooldownDuration = 5f;
    [SerializeField] private ParticleSystem particles;

    public NetworkVariable<bool> isReady = new(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool isReadyLocal = true;
    public bool IsReady => IsSpawned ? isReady.Value : isReadyLocal;

    private bool HasAuthority => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

    private void Awake()
    {
        // Kinematic Rigidbody is required for Unity physics to dispatch compound child trigger events
        // and register collisions between CharacterController and static triggers.
        if (!TryGetComponent<Rigidbody>(out var rb))
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    private void Start()
    {
        if (!IsSpawned)
        {
            UpdateParticles(isReadyLocal);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        isReady.OnValueChanged += OnReadyChanged;
        UpdateParticles(isReady.Value);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        isReady.OnValueChanged -= OnReadyChanged;
    }

    public void OnColliderEnter(GameObject collider)
    {
        if (!HasAuthority || !IsReady) return;

        Entity entity = collider.GetComponentInParent<Entity>();
        if (entity == null || entity.isDead.Value) return;

        entity.ApplyEffect(EffectType.Freeze, freezeDuration);
        StartCoroutine(CooldownRoutine());
    }

    private void OnTriggerEnter(Collider other)
    {
        OnColliderEnter(other.gameObject);
    }

    private IEnumerator CooldownRoutine()
    {
        SetReady(false);
        yield return new WaitForSeconds(cooldownDuration);
        SetReady(true);
    }

    private void SetReady(bool ready)
    {
        if (IsSpawned)
        {
            isReady.Value = ready;
        }
        else
        {
            isReadyLocal = ready;
            UpdateParticles(ready);
        }
    }

    private void OnReadyChanged(bool oldValue, bool newValue)
    {
        UpdateParticles(newValue);
    }

    private void UpdateParticles(bool active)
    {
        if (particles == null) return;

        if (active)
        {
            particles.gameObject.SetActive(true);
            particles.Play(true);
        }
        else
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.gameObject.SetActive(false);
        }
    }
}

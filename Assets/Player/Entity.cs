using Unity.Netcode;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;
using System.Collections;

public abstract class Entity : NetworkBehaviour
{
    public float dangerLevel = 1f;
    [SerializeField] NetworkObject glassCage; 

    public NetworkVariable<bool> isDead = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> entityHealth = new(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isFreezed = new(false);
    public override void OnNetworkSpawn()
    {
        isDead.OnValueChanged += OnDeathStateChange;
        isFreezed.OnValueChanged += OnFreezeStateChange;
    }
    public override void OnNetworkDespawn()
    {
        isDead.OnValueChanged -= OnDeathStateChange;
        isFreezed.OnValueChanged -= OnFreezeStateChange;

    }

    public bool IsDead() => isDead.Value;
    public float GetHealth() => entityHealth.Value;
    public void AddHealth(float _health)
    {

        entityHealth.Value += _health;
        if (entityHealth.Value <= 0 && !isDead.Value)
        {
            isDead.Value = true;
            Debug.Log($"---Enemy {name} was killed!");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TurnIntoSphereServerRpc()
    {
        if (isDead.Value || GetHealth() >= 1) return;
        var glass = Instantiate(glassCage, transform.position, Quaternion.identity);
        glass.Spawn();
        isDead.Value = true;
    }

    [ServerRpc(RequireOwnership = false)]
    public void FreezeServerRpc(float seconds)
    {
        StartCoroutine(FreezeTimer(seconds));
    }

    private IEnumerator FreezeTimer(float freezeTime)
    {
        isFreezed.Value = true;
        yield return new WaitForSeconds(freezeTime);
        isFreezed.Value = false;
    }

    protected void OnDeathStateChange(bool oldValue, bool _isDead)
    {
        if (isDead.Value) KillEntity();
        else ReviveEntity();
    }
    virtual protected void OnFreezeStateChange(bool oldV, bool newV) { }

    virtual protected void KillEntity() { }
    virtual protected void ReviveEntity() { }
}

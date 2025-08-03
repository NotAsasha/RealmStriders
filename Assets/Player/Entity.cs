using Unity.Netcode;
using UnityEngine;

public abstract class Entity : NetworkBehaviour
{
    public float dangerLevel = 1f;
    [SerializeField] NetworkObject glassCage; 

    public NetworkVariable<bool> isDead = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> entityHealth = new(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        isDead.OnValueChanged += OnDeathStateChange;
    }
    public override void OnNetworkDespawn()
    {
        isDead.OnValueChanged -= OnDeathStateChange;
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

    [ServerRpc]
    public void TurnIntoSphereServerRpc()
    {
        if (isDead.Value || GetHealth() >= 1) return;
        var glass = Instantiate(glassCage, transform.position, Quaternion.identity);
        glass.Spawn();
        isDead.Value = true;
    }

    private void OnDeathStateChange(bool oldValue, bool _isDead)
    {
        if (_isDead) KillEntity();
        else ReviveEntity();
    }

    virtual protected void KillEntity() { }
    virtual protected void ReviveEntity() { }
}

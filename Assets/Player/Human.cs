using UnityEngine;
using Unity.Netcode;
using Player;
using System.Collections;
public class Human : NetworkBehaviour, IEntity
{
    public const float defaultHealth = 100f;
    public NetworkVariable<bool> isDead = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> playerHealth = new(defaultHealth, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        isDead.OnValueChanged += OnDeathStateChange;
    }
    public override void OnNetworkDespawn()
    {
        isDead.OnValueChanged -= OnDeathStateChange;
    }
    public bool IsDead() => isDead.Value;
    public float GetHealth() => playerHealth.Value;
    public void AddHealth(float _health)
    {

        playerHealth.Value += _health;
        if (playerHealth.Value <= 0 && !isDead.Value)
        {
            isDead.Value = true;
            Debug.Log($"---Crew member {OwnerClientId} was killed!");
        }
    }
    private void OnDeathStateChange(bool oldValue, bool _isDead)
    {
        if (_isDead) KillPlayer();
        else RevivePlayer();
    }
    private void KillPlayer()
    {
        GetComponent<Movement>().enabled = false;
        GameManager.instance.OnPlayerDeathServerRpc();
    }
    private void RevivePlayer()
    {
        GetComponent<Movement>().enabled = true;
    }
}
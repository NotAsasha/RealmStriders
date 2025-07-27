using NUnit.Framework;
using Player;
using System.Globalization;
using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;

public class Enemy : NetworkBehaviour, IEntity
{
    public const float defaultHealth = 200f;
    public NetworkVariable<bool> isDead = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> entityHealth = new(defaultHealth, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Animator animator;
    private ChasePlayer vision;
    private NavMeshAgent agent;
    private RandomMove randMove;
    private Rigidbody rigidbody;
    private void Awake()
    {
        animator = GetComponent<Animator>();
        vision = GetComponent<ChasePlayer>();
        agent = GetComponent<NavMeshAgent>();
        randMove = GetComponent<RandomMove>();
        rigidbody = GetComponent<Rigidbody>();
        ToggleRagdoll(false);
    }

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
            Debug.Log($"---Enemy {OwnerClientId} was killed!");
        }
    }
    private void OnDeathStateChange(bool oldValue, bool _isDead)
    {
        if (_isDead) KillPlayer();
    }
    private void KillPlayer()
    {
        if (!IsOwner) return;
        ToggleRagdoll(true);
    }

    private void ToggleRagdoll(bool isActive)
    {
        animator.enabled = !isActive;
        vision.enabled = !isActive;
        agent.enabled = !isActive;
        randMove.enabled = !isActive;
        rigidbody.isKinematic = !isActive;
    }

    private void FixedUpdate()
    {
        if (!IsServer || isDead.Value) return;

        vision.DrawViewState();

        var player = vision.PlayerInSight();
        if (player != null)
        {
            agent.SetDestination(player.transform.position);
            Debug.Log("Found!");
        }
        else
        {
            if (agent.remainingDistance <= agent.stoppingDistance) //done with path
            {
                Vector3 point;
                if (randMove.RandomPoint(transform.position, randMove.range, out point)) //pass in our centre point and radius of area
                {
                    Debug.DrawRay(point, Vector3.up, Color.blue, 1.0f); //so you can see with gizmos
                    agent.SetDestination(point);
                }
            }
        }
    }
}

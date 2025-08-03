using NUnit.Framework;
using Player;
using System.Globalization;
using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;

public class Enemy : Entity, ICollidable
{
    public float damage = 1f;

    private Animator animator;
    private ChasePlayer vision;
    private NavMeshAgent agent;
    private RandomMove randMove;
    private Rigidbody playerRigidbody;

    #region Initialization

    private void Awake()
    {
        animator = GetComponent<Animator>();
        vision = GetComponent<ChasePlayer>();
        agent = GetComponent<NavMeshAgent>();
        randMove = GetComponent<RandomMove>();
        playerRigidbody = GetComponent<Rigidbody>();
        ToggleRagdoll(false);
    }

    #endregion

    #region Entity

    override protected void KillEntity()
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
        playerRigidbody.isKinematic = !isActive;
    }

    #endregion

    #region ICollidable

    public void OnColliderEnter(GameObject collider)
    {
        if (!IsServer || isDead.Value) return;
        var player = collider.GetComponent<Human>();
        if (player == null || player.isDead.Value) return;

        PlayerBiteClientRpc(collider);
        player.AddHealth(-damage);
    }

    [ClientRpc]
    virtual protected void PlayerBiteClientRpc(NetworkObjectReference obj)
    {
        obj.TryGet(out NetworkObject player);
        Debug.Log($"---Enemy: Eaten {player.name}");
    }

    #endregion



    private void FixedUpdate()
    {
        if (!IsServer || isDead.Value) return;

        vision.DrawViewState(); //draw vision boundaries

        var player = vision.PlayerInSight();
        if (player != null)
        {
            agent.SetDestination(player.transform.position);
            Debug.Log("Found!");
        }
        else
        {
            if (agent.remainingDistance > agent.stoppingDistance) return; //not done with path

            Vector3 point;
            if (randMove.RandomPoint(transform.position, randMove.range, out point)) //choose where to go
            {
                Debug.DrawRay(point, Vector3.up, Color.blue, 1.0f); //so you can see with gizmos
                agent.SetDestination(point);
            }
        }
    }
}

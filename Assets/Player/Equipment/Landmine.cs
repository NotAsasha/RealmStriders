using InventorySystem;
using Unity.Netcode;
using UnityEngine;

public class Landmine : Item, ICollidable
{
    [SerializeField] float explosionRadius = 10;
    [SerializeField] float damage = 500;
    [SerializeField] ParticleSystem emit;
    [SerializeField] LayerMask entityLayer;
    [SerializeField] LayerMask wallLayer;

    bool isTriggered = false;
    protected override void ExecuteItemAction(GameObject player)
    {
        ExplodeServerRpc();
        Debug.Log("---Landmine: Used!");
    }

    public void OnColliderEnter(GameObject collider)
    {
        if (isCurrentlyHeld) return;
        isTriggered = true;
        Debug.Log("---Landmine: Collided with something!");
    }

    public void OnColliderExit(GameObject collider)
    {
        if (!IsServer || isCurrentlyHeld || !isTriggered) return;
        ExplodeServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void ExplodeServerRpc()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, entityLayer);
        foreach (Collider collider in hits)
        {
            ApplyDamage(collider);
        }

        ExplodeClientRpc();
        NetworkObject.Despawn(true);
    }

    void ApplyDamage(Collider _collider)
    {
        //Calculate all vectors
        Vector3 toTarget = _collider.transform.position - transform.position;
        Vector3 direction = toTarget.normalized;
        float distanceToTarget = toTarget.magnitude;

        //Check for walls
        if (Physics.Raycast(transform.position, direction, out RaycastHit hit, distanceToTarget, wallLayer)) return;

        //Apply damage
        var entity = _collider.GetComponent<IEntity>();
        float damageToApply = damage / Mathf.Max(distanceToTarget, 1f);
        entity.AddHealth(-damageToApply);



        if (entity.IsDead())
        {
            var rigidbody = _collider.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                _collider.GetComponent<Rigidbody>()?.AddForce(direction * 10f);
            }
            
            Debug.Log("---Landmine: Killed some entity.");
        }
    }


    [ClientRpc]
    private void ExplodeClientRpc()
    {
        Debug.Log("---Landmine: Boom!");
        PlayParticles();
    }
    private void PlayParticles()
    {
        emit.transform.parent = null;
        emit.Play();
        Destroy(emit.gameObject, 2f);
    }
}

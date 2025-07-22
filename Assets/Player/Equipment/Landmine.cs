using Unity.Netcode;
using UnityEngine;

public class Landmine : NetworkBehaviour, ITakable, ICollidable
{
    //
    [SerializeField] float explosionRadius = 10;
    [SerializeField] float damage = 200;
    [SerializeField] LayerMask entityLayer;
    [SerializeField] LayerMask wallLayer;

    public bool IsSingleUse() => true;
    public void Interact(GameObject player)
    {
        Take(player);
    }
    public void StopInteraction(GameObject player)
    {
        Drop(player);
    }

    public void Take(GameObject player)
    {
        var inventory = player.GetComponentInParent<Inventory>();
        inventory.AddItem(gameObject, inventory.activeSlotIndex);
        GetComponent<Collider>().enabled = false;
        GetComponent<Rigidbody>().isKinematic = true;
    }
    public void Drop(GameObject player)
    {
        GetComponent<Collider>().enabled = true;
        GetComponent<Rigidbody>().isKinematic = false;
    }
    public GameObject GetGameObject() => gameObject;


    public void Use(GameObject player)
    {
        ExplodeServerRpc();
        Debug.Log("---Landmine: Used!");
    }

    public void OnColliderEnter(GameObject collider)
    {
        Debug.Log("---Landmine: Collided with something!");
    }
    public void OnColliderExit(GameObject collider)
    {
        if (!IsServer) return;
        ExplodeServerRpc();
    }

    [ServerRpc]
    public void ExplodeServerRpc()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, entityLayer);
        foreach (Collider collider in hits)
        {
            ApplyDamage(collider);
        }
        ExplodeClientRpc();
        Destroy(gameObject);
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
            Debug.Log("---Landmine: Killed some entity.");
    }
    [ClientRpc]
    private void ExplodeClientRpc()
    {
        Debug.Log("---Landmine: Boom!");
    }
}

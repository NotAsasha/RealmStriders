using Unity.VisualScripting;
using UnityEngine;

public class LeafBlower : MonoBehaviour, ITakable
{
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
    public void Use(GameObject player)
    {
        Debug.Log("Using Leaf-Blower");
    }

    public GameObject GetGameObject() => gameObject;
}

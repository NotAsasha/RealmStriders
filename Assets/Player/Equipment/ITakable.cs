using UnityEngine;

public interface ITakable : IInteractable
{
    void Take(GameObject player);
    void Drop(GameObject player);
    void Use(GameObject player);

    GameObject GetGameObject();
}

using UnityEngine;
using Player.Movement;

public class Crosshair : MonoBehaviour
{
    private void Start()
    {
        PlayerMovement.Instance.onInteractionStateChanged += OnInteractionStateChanged;
    }

    private void OnDestroy()
    {
        if (PlayerMovement.Instance != null)
        {
            PlayerMovement.Instance.onInteractionStateChanged -= OnInteractionStateChanged;
        }
    }

    private void OnInteractionStateChanged(bool isInInteraction)
    {
        gameObject.SetActive(!isInInteraction);
    }
}

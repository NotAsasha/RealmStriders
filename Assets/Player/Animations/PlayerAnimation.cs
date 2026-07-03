using Player;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging; // Переконайся, що namespace на місці

public class PlayerAnimation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rig handWeight;
    [SerializeField] private Inventory inventory;

    [Header("Settings")]
    [Tooltip("Швидкість підняття/опускання рук")]
    [SerializeField] private float handAnimationSpeed = 10f;

    private Coroutine weightLerpCoroutine;

    // Використовуємо OnEnable замість Start для безпечної роботи в мережі та респавнах
    private void OnEnable()
    {
        if (inventory != null)
        {
            inventory.itemChanged += OnItemChanged;
        }
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.itemChanged -= OnItemChanged;
        }
    }

    private void OnItemChanged(bool isHoldingSmth)
    {
        float targetWeight = isHoldingSmth ? 1f : 0f;

        if (weightLerpCoroutine != null)
        {
            StopCoroutine(weightLerpCoroutine);
        }
        weightLerpCoroutine = StartCoroutine(AnimateRigWeightCoroutine(targetWeight));
    }

    private IEnumerator AnimateRigWeightCoroutine(float targetWeight)
    {
        while (!Mathf.Approximately(handWeight.weight, targetWeight))
        {
            handWeight.weight = Mathf.Lerp(handWeight.weight, targetWeight, Time.deltaTime * handAnimationSpeed);

            yield return null;
        }

        handWeight.weight = targetWeight;

        weightLerpCoroutine = null;
    }
}
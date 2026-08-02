using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

[RequireComponent(typeof(Volume))]
public class OutOfBoundsEffect : MonoBehaviour
{
    private Volume volume;

    private void Awake()
    {
        if (!TryGetComponent(out volume))
        {
            Debug.LogError("---OutOfBoundsEffect: No Volume component found");
        }
    }
    private void OnEnable()
    {
        GameManager.Instance.hasStartedMission.OnValueChanged += StartEffect;
    }
    private void OnDisable()
    {
        GameManager.Instance.hasStartedMission.OnValueChanged -= StartEffect;
    }

    private void StartEffect(bool _, bool val)
    {
        StartCoroutine(FadeRoutine(val ? 0f : 1f));

    }

    private IEnumerator FadeRoutine(float target)
    {
        Debug.Log("FadeRoutine");
        float duration = 5f;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            volume.weight = Mathf.Lerp(0f, target, elapsed / duration);
            yield return null;
        }
        volume.weight = target;
    }
}

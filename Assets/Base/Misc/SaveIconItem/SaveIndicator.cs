using System.Collections;
using UnityEngine;

public class SaveIndicator : MonoBehaviour
{
    [SerializeField] private Renderer indicatorRenderer;
    [SerializeField] private AudioSource sound;
    [SerializeField] private AudioClip successSound;
    [SerializeField] private AudioClip failSound;
    [SerializeField] private float blinkDuration = 1.5f;
    [SerializeField] private float blinkInterval = 0.12f;

    private Coroutine blinkCoroutine;
    private Material indicatorMaterial;
    private Color initialColor;

    private void OnEnable()
    {
        SaveGameButton.SaveCompleted += ShowResult;
    }

    private void OnDisable()
    {
        SaveGameButton.SaveCompleted -= ShowResult;
    }

    private void Awake()
    {
        if (indicatorRenderer == null) return;

        indicatorMaterial = indicatorRenderer.material;
        initialColor = indicatorMaterial.color;
    }

    public void ShowResult(bool succeeded)
    {
        if (indicatorMaterial == null || sound == null) return;

        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
        }

        blinkCoroutine = StartCoroutine(Blink(succeeded ? Color.green : Color.red));
        sound.PlayOneShot(succeeded ? successSound : failSound);
    }

    private IEnumerator Blink(Color color)
    {
        float elapsed = 0f;
        bool isColorShown = false;
        while (elapsed < blinkDuration)
        {
            isColorShown = !isColorShown;
            indicatorMaterial.color = isColorShown ? color : initialColor;
            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        indicatorMaterial.color = initialColor;
        blinkCoroutine = null;
    }
}

using UnityEngine;
using UnityEngine.Rendering.UI;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class CategoryBlocker : MonoBehaviour
{
    [SerializeField] private int requiredRating;
    private Image image;

    private void Awake()
    {
        if (!TryGetComponent(out image))
        {
            Debug.LogError("---CategoryBlocker: No Image component found");
        }
    }


    private void OnEnable()
    {
        GameManager.Instance.teamRating.OnValueChanged += UpdateUI;
        UpdateUI(0, GameManager.Instance.teamRating.Value);
    }
    private void OnDisable()
    {
        GameManager.Instance.teamRating.OnValueChanged -= UpdateUI;
    }

    private void UpdateUI(int _, int val)
    {
        image.enabled = !(val >= requiredRating);
    }
}

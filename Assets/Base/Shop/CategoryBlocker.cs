using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class CategoryBlocker : MonoBehaviour
{
    [SerializeField] private int requiredRating;
    private Button button;

    private void Awake()
    {
        if (!TryGetComponent(out button))
        {
            Debug.LogError("---CategoryBlocker: No Button component found");
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
        button.interactable = val >= requiredRating;
    }
}

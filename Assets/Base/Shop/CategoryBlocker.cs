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
        if (GameManager.Instance != null)
        {
            GameManager.Instance.teamRating.OnValueChanged += UpdateUI;
            UpdateUI(0, GameManager.Instance.teamRating.Value);
        }
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.teamRating.OnValueChanged -= UpdateUI;
        }
    }

    private void UpdateUI(int _, int val)
    {
        button.interactable = val >= requiredRating;
    }

    /// <summary>
    /// Called by CategorySwitch to force-refresh interactable state,
    /// bypassing OnValueChanged callback ordering issues.
    /// </summary>
    public void Refresh(int currentRating)
    {
        if (button == null) button = GetComponent<Button>();
        button.interactable = currentRating >= requiredRating;
    }
}

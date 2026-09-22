using UnityEngine;
using UnityEngine.UI;

public class CategorySwitch : MonoBehaviour
{
    public GameObject[] categories;
    public Renderer[] buttons;
    public Button[] buttonComponents;

    private CategoryBlocker[] blockers;

    private void Awake()
    {
        // Cache CategoryBlocker references so we can force-refresh them
        blockers = new CategoryBlocker[buttonComponents.Length];
        for (int i = 0; i < buttonComponents.Length; i++)
        {
            blockers[i] = buttonComponents[i].GetComponent<CategoryBlocker>();
        }
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.teamRating.OnValueChanged += OnRatingChanged;
    }

    private void Start()
    {
        // Safety net: handles late-join where NetworkVariable already has
        // the server value but OnValueChanged may not fire for the initial sync
        SwitchToItem(0);
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.teamRating.OnValueChanged -= OnRatingChanged;
    }

    private void OnRatingChanged(int previousValue, int newValue)
    {
        SwitchToItem(0);
    }

    public void SwitchToItem(int index)
    {
        // Force-refresh all CategoryBlockers before reading interactable,
        // eliminating any dependency on callback execution order
        RefreshBlockers();

        for (int i = 0; i < categories.Length; i++)
        {
            categories[i].SetActive(i == index);
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttonComponents[i].interactable == false)
            {
                buttons[i].material.color = Color.gray;
                continue;
            }
            buttons[i].material.color = i == index ? Color.red : Color.white;
        }
    }

    private void RefreshBlockers()
    {
        if (GameManager.Instance == null) return;

        int rating = GameManager.Instance.teamRating.Value;
        for (int i = 0; i < blockers.Length; i++)
        {
            if (blockers[i] != null)
                blockers[i].Refresh(rating);
        }
    }
}

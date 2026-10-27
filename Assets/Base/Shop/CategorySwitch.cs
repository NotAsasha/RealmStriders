using UnityEngine;
using UnityEngine.UI;

public class CategorySwitch : MonoBehaviour
{
    public GameObject[] categories;
    public Button[] buttonComponents;
    [SerializeField] private CategoryBlocker[] blockers;
    [SerializeField] private Image[] indicators;
    [SerializeField] private GameObject catalog;
    [SerializeField] private Base.Shop.ItemCard[] itemCards;
    [SerializeField] private Color normalColor = new(.09f, .09f, .11f);
    [SerializeField] private Color activeColor = new(.24f, .25f, .28f);
    private int selectedIndex;

    private void Awake()
    {
        RefreshBlockers();
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.teamRating.OnValueChanged += OnRatingChanged;
        SwitchToItem(0);
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
        RefreshBlockers();
        if (!buttonComponents[selectedIndex].interactable) SwitchToItem(0);
        else RefreshColors();
    }

    public void SwitchToItem(int index)
    {
        // Force-refresh all CategoryBlockers before reading interactable,
        // eliminating any dependency on callback execution order
        if (index < 0 || index >= categories.Length) return;
        RefreshBlockers();
        if (!buttonComponents[index].interactable) return;
        selectedIndex = index;
        foreach (var card in itemCards) card.gameObject.SetActive(false);
        catalog.SetActive(true);

        for (int i = 0; i < categories.Length; i++)
        {
            categories[i].SetActive(i == index);
        }

        RefreshColors();
    }

    private void RefreshColors()
    {
        for (int i = 0; i < buttonComponents.Length; i++)
        {
            var colors = buttonComponents[i].colors;
            colors.normalColor = i == selectedIndex ? activeColor : normalColor;
            buttonComponents[i].colors = colors;
            indicators[i].enabled = i == selectedIndex;
        }
    }

    private void RefreshBlockers()
    {
        int rating = GameManager.Instance != null ? GameManager.Instance.teamRating.Value : 0;
        for (int i = 0; i < blockers.Length; i++)
        {
            if (blockers[i] != null)
                blockers[i].Refresh(rating);
        }
    }
}

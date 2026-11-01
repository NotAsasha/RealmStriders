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
    [SerializeField] private Base.Shop.ShopCategoryButton[] physicalButtons;
    [SerializeField] private Base.Shop.ShopPanelMotion panelMotion;
    [SerializeField] private TMPro.TMP_Text ratingStatus;
    [SerializeField] private Color normalColor = new(.09f, .09f, .11f);
    [SerializeField] private Color activeColor = new(.24f, .25f, .28f);
    private int selectedIndex;
    private GameManager gameManager;

    private void Awake()
    {
        SwitchToItem(0);
    }

    public void Bind(GameManager manager)
    {
        if (gameManager != null) gameManager.teamRating.OnValueChanged -= OnRatingChanged;
        gameManager = manager;
        if (gameManager != null) gameManager.teamRating.OnValueChanged += OnRatingChanged;
        OnRatingChanged(0, 0);
    }

    private void OnDisable()
    {
        if (gameManager != null) gameManager.teamRating.OnValueChanged -= OnRatingChanged;
    }

    private void OnRatingChanged(int previousValue, int newValue)
    {
        RefreshBlockers();
        if (!buttonComponents[selectedIndex].interactable) SwitchToItem(0);
        else RefreshColors();
    }

    private void OnEnable()
    {
        if (gameManager != null) Bind(gameManager);
    }

    public void SwitchToItem(int index)
    {
        // Force-refresh all CategoryBlockers before reading interactable,
        // eliminating any dependency on callback execution order
        if (index < 0 || index >= categories.Length) return;
        RefreshBlockers();
        if (!buttonComponents[index].interactable) return;
        bool changed = selectedIndex != index || !catalog.activeSelf || (panelMotion != null && panelMotion.IsTransitioning);
        selectedIndex = index;
        if (changed && panelMotion != null) panelMotion.Show(catalog);
        else if (panelMotion == null)
        {
            foreach (var card in itemCards) card.gameObject.SetActive(false);
            catalog.SetActive(true);
        }

        for (int i = 0; i < categories.Length; i++)
        {
            categories[i].SetActive(i == index);
        }

        RefreshColors();
    }

    private void RefreshColors()
    {
        if (ratingStatus != null)
        {
            int rating = GameManager.Instance != null ? GameManager.Instance.teamRating.Value : 0;
            ratingStatus.text = "Team rating: " + rating + "     |     Category " + (selectedIndex + 1) + " / 5";
        }
        for (int i = 0; i < buttonComponents.Length; i++)
        {
            var colors = buttonComponents[i].colors;
            colors.normalColor = i == selectedIndex ? activeColor : normalColor;
            buttonComponents[i].colors = colors;
            indicators[i].enabled = i == selectedIndex;
            if (physicalButtons != null && i < physicalButtons.Length)
                physicalButtons[i].Refresh(i == selectedIndex, buttonComponents[i].interactable);
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

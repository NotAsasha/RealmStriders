using UnityEngine;
using UnityEngine.UI;

public class CategorySwitch : MonoBehaviour
{
    public GameObject[] categories;
    public Renderer[] buttons;
    public Button[] buttonComponents;

    public void OnEnable()
    {
        GameManager.Instance.teamRating.OnValueChanged += (int _, int _) => SwitchToItem(0);
        SwitchToItem(0);
    }

    public void SwitchToItem(int index)
    {
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
}

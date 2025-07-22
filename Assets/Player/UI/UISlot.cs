using UnityEngine;
using UnityEngine.UI;

public class UISlot : MonoBehaviour
{
    public void UpdateUI(bool isTaken, bool isSelected)
    {
        GetComponent<Image>().color =
            isSelected ? Color.red :
            isTaken ? Color.darkGray : Color.white;
    }
}

using TMPro;
using UnityEngine;

namespace Base.Shop
{
    public class BalanceUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text textOutput;

        private void Start()
        {
            //causes error, fix --- TODO
            GameManager.Instance.teamMoney.OnValueChanged += UpdateUI;
            textOutput.text = GameManager.Instance.teamMoney.Value.ToString();
        }

        private void UpdateUI(int _, int money)
        {
            if (!textOutput)
            {
                Debug.LogError("---BalanceUI: textOutput not set.");
                return;
            }
            textOutput.text = money.ToString();
        }
    }
}
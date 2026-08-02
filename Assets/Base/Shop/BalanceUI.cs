using TMPro;
using UnityEngine;

namespace Base.Shop
{
    public class BalanceUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text textOutput;

        private void OnEnable()
        {
            GameManager.Instance.teamMoney.OnValueChanged += UpdateUI;
            UpdateUI(0, GameManager.Instance.teamMoney.Value);
        }
        private void OnDisable()
        {
            GameManager.Instance.teamMoney.OnValueChanged -= UpdateUI;
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
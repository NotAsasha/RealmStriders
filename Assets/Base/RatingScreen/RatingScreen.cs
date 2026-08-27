using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Base.RatingScreen
{
    public class RatingScreen : MonoBehaviour
    {
        private const int MaxPointsPerStar = 3;

        [SerializeField] private Sprite[] starTextures; // 0 = пуста, 1 = 1/3, 2 = 2/3, 3 = повна
        [SerializeField] private Image[] stars;

        private void OnEnable()
        {
            GameManager.Instance.teamRating.OnValueChanged += UpdateCounter;
            UpdateCounter(0, GameManager.Instance.teamRating.Value);
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.teamRating.OnValueChanged -= UpdateCounter;
            }
        }

        private void UpdateCounter(int _, int rating)
        {
            int currentRating = rating;

            for (int i = 0; i < stars.Length; i++)
            {
                int starState = Mathf.Clamp(currentRating, 0, MaxPointsPerStar);
                
                stars[i].sprite = starTextures[starState];
                currentRating -= MaxPointsPerStar;
            }
        }
    }
}
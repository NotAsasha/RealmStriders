using UnityEngine;
using TMPro;

public class RatingScreen : MonoBehaviour
{
    [SerializeField] TMP_Text screen;

    private void Start()
    {
        GameManager.instance.teamRating.OnValueChanged += OnRatingChange;
        OnRatingChange(0, GameManager.instance.teamRating.Value);
    }

    private void OnDestroy()
    {
        GameManager.instance.teamRating.OnValueChanged -= OnRatingChange;
    }

    private void OnRatingChange(int oldV, int _rating)
    {
        screen.text = _rating.ToString();
    }
}

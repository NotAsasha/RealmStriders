using UnityEngine;
using TMPro;
using Unity.Netcode;
using UnityEngine.UI;

public class RatingScreen : NetworkBehaviour
{
    [SerializeField] TMP_Text screen;
    [SerializeField] Slider teamRatingSlider;
    [SerializeField] Slider minRatingSlider;

    public override void OnNetworkSpawn()
    {
        var teamRating = GameManager.instance.teamRating;
        var looseRating = GameManager.instance.looseRating;

        teamRating.OnValueChanged += (oldV, newV) =>
        {
            screen.text = newV.ToString();
            teamRatingSlider.value = newV;
        };

        looseRating.OnValueChanged += (oldV, newV) =>
        {
            minRatingSlider.value = newV;
        };

        // Update at the beginning
        screen.text = teamRating.Value.ToString();
        teamRatingSlider.value = teamRating.Value;
        minRatingSlider.value = looseRating.Value;
    }
}

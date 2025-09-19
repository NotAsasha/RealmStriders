using UnityEngine;
using TMPro;
using Unity.Netcode;
using UnityEngine.UI;

public class RatingScreen : NetworkBehaviour
{
    [SerializeField] Slider teamRatingSlider;
    [SerializeField] Slider minRatingSlider;

    public override void OnNetworkSpawn()
    {
        var teamRating = GameManager.instance.teamRating;
        var looseRating = GameManager.instance.looseRating;

        teamRating.OnValueChanged += (oldV, newV) =>
        {
            teamRatingSlider.value = newV;
        };

        looseRating.OnValueChanged += (oldV, newV) =>
        {
            minRatingSlider.value = newV;
        };

        // Update at the beginning
        teamRatingSlider.value = teamRating.Value;
        minRatingSlider.value = looseRating.Value;
    }
}

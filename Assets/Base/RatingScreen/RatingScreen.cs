using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Base.RatingScreen
{
    // idk why it is NetworkBehaviour, TODO
    public class RatingScreen : NetworkBehaviour
    {
        [SerializeField] Slider teamRatingSlider;
        [SerializeField] Slider minRatingSlider;

        public override void OnNetworkSpawn()
        {
            var teamRating = GameManager.Instance.teamRating;
            var looseRating = GameManager.Instance.looseRating;

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
}

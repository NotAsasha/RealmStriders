using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Base.RatingScreen
{
    public sealed class CampaignRatingDisplay : NetworkBehaviour
    {
        [SerializeField] private GameObject overRating;
        [SerializeField] private TMP_Text ratingText;

        private GameManager manager;

        public override void OnNetworkSpawn()
        {
            manager = GameManager.Instance;
            manager.campaignCompleted.OnValueChanged += OnCompletionChanged;
            manager.teamRating.OnValueChanged += OnRatingChanged;
            RefreshDisplay();
        }

        public override void OnNetworkDespawn()
        {
            if (manager == null) return;
            manager.campaignCompleted.OnValueChanged -= OnCompletionChanged;
            manager.teamRating.OnValueChanged -= OnRatingChanged;
            overRating.SetActive(false);
            manager = null;
        }

        private void OnCompletionChanged(bool previous, bool current) => RefreshDisplay();
        private void OnRatingChanged(int previous, int current) => RefreshDisplay();

        private void RefreshDisplay()
        {
            overRating.SetActive(manager.campaignCompleted.Value);
            ratingText.SetText("{0:1} STARS", Mathf.Max(0, manager.teamRating.Value) / 3f);
        }
    }
}

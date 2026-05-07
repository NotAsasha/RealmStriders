using Player.Movement;
using Player.Network;
using Steamworks;
using Steamworks.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player.UI
{
    public class PauseUIManager : MonoBehaviour
    {
        public GameObject pauseMenu;
        public GameObject settingsMenu;
        public Transform cardsParent;
        public GameObject playerCard;
        public ScreenShatterEffect shatterEffect;

        private void Start()
        {
            SteamMatchmaking.OnLobbyDataChanged += UpdatePauseUI;
            PlayerMovement.Instance.controls.System.Pause.performed += UpdateMenuState;
        }

        private void OnDisable()
        {
            SteamMatchmaking.OnLobbyDataChanged -= UpdatePauseUI;
            if (PlayerMovement.Instance != null && PlayerMovement.Instance.controls != null)
            {
                PlayerMovement.Instance.controls.System.Pause.performed -= UpdateMenuState;
            }
        }

        private void UpdateMenuState(InputAction.CallbackContext obj)
        {
            if (PlayerMovement.Instance.isInInteraction)
            {
                CameraMovement.Instance.StopInteraction();
                return;
            }

            if (settingsMenu != null && settingsMenu.activeSelf)
            {
                ToggleSettings(false);
                return;
            }

            bool opening = !pauseMenu.activeSelf;
            
            if (opening && shatterEffect != null)
            {
                shatterEffect.TriggerEffect(() => {
                    pauseMenu.SetActive(true);
                });
            }
            else
            {
                pauseMenu.SetActive(!pauseMenu.activeSelf);
                if (!pauseMenu.activeSelf && shatterEffect != null)
                {
                    shatterEffect.ResetEffect();
                }
            }
            }

            public void ToggleSettings(bool active)
            {
            settingsMenu.SetActive(active);
            pauseMenu.SetActive(!active);
            
            if (!active && !pauseMenu.activeSelf && shatterEffect != null)
            {
                shatterEffect.ResetEffect();
            }
            }

        private List<PlayerCard> playerCards = new();

        private async void UpdatePauseUI(Lobby lobby)
        {
            List<SteamPlayer> playerList = await GetLobbyMembersAsync(lobby);

            //set each card as inactive
            foreach (var card in playerCards)
            {
                card.isFoundInLobby = false;
            }


            foreach (SteamPlayer player in playerList)
            {
                PlayerCard existingCard = playerCards.Find(c => c.linkedPlayer.playerSteamId == player.playerSteamId);

                if (existingCard != null)
                {
                    existingCard.isFoundInLobby = true;
                    existingCard.playerName.text = player.playerName;
                }
                else
                {
                    var newCard = Instantiate(playerCard, cardsParent).GetComponent<PlayerCard>();
                    newCard.linkedPlayer = player;
                    newCard.playerName.text = player.playerName;
                    newCard.isFoundInLobby = true;

                    if (player.playerImage.HasValue)
                    {
                        var img = player.playerImage.Value;
                        newCard.playerImage.texture = PlayerCard.ImageFromBytes((int)img.Width, (int)img.Height, img.Data);
                    }

                    playerCards.Add(newCard);
                }
            }

            //delete all inactive
            for (int i = playerCards.Count - 1; i >= 0; i--)
            {
                if (!playerCards[i].isFoundInLobby)
                {
                    PlayerCard cardToDestroy = playerCards[i];
                    playerCards.RemoveAt(i);
                    Destroy(cardToDestroy.gameObject);
                }
            }
        }




        public async Task<List<SteamPlayer>> GetLobbyMembersAsync(Lobby lobby)
        {
            List<SteamPlayer> playerList = new();

            foreach (var member in lobby.Members)
            {
                var imageTask = await member.GetMediumAvatarAsync();

                SteamPlayer player = new(member.Name, member.Id, imageTask, member);
                playerList.Add(player);
            }
            return playerList;
        }

        void DestroyChildren(Transform parent)
        {
            foreach (Transform child in parent) Destroy(child.gameObject);
        }
    }
}

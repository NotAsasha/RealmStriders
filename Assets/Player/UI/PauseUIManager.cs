using System.Collections.Generic;
using Player.Movement;
using Player.Network;
using Steam;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player.UI
{
    public class PauseUIManager : MonoBehaviour
    {
        public GameObject pauseMenu;
        public Transform cardsParent;
        public GameObject playerCard;

        private Controls controls;

        private void Start()
        {
            SteamMatchmaking.OnLobbyDataChanged += UpdatePauseUI;
            PlayerMovement.Instance.controls.System.Pause.performed += UpdateMenuState;
        }

        private void OnDisable()
        {
            SteamMatchmaking.OnLobbyDataChanged -= UpdatePauseUI;
            PlayerMovement.Instance.controls.System.Pause.performed -= UpdateMenuState;
        }

        private void UpdateMenuState(InputAction.CallbackContext obj)
        {
            if (PlayerMovement.Instance.isInInteraction)
            {
                CameraMovement.Instance.StopInteraction();
                return;
            }
            pauseMenu.SetActive(!pauseMenu.activeSelf);
        }

        private void UpdatePauseUI(Lobby lobby)
        {
            // may be inefficient to destroy everything, revise TODO
            List<SteamPlayer> playerList = GetLobbyMembers(lobby);
            Debug.Log("Players count -- " + playerList.Count);

            DestroyChildren(cardsParent);
            foreach (SteamPlayer player in playerList)
            {
                var currentCard = Instantiate(playerCard, cardsParent).GetComponent<PlayerCard>();
                currentCard.linkedPlayer = player;
                currentCard.playerName.text = player.playerName;
            }
        }

        public List<SteamPlayer> GetLobbyMembers(Lobby lobby)
        {
            List<SteamPlayer> playerList = new();

            foreach (Friend member in lobby.Members)
            {
                // IT HAS AN IMAGEEEEEE, add to the screen! TODO
                Image? playerImage = member.GetSmallAvatarAsync().Result;
                SteamPlayer player = new(member.Name, member.Id, playerImage, member);
                playerList.Add(player);
                Debug.Log(player.playerName);
            }
            return playerList;
        }

        void DestroyChildren(Transform parent)
        {
            foreach (Transform child in parent) Destroy(child.gameObject);
        }
    }
}

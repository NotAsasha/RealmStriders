using Player.Network;
using Steam;
using Steamworks;
using Steamworks.Data;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Button = Base.Interactables.Button;

namespace Player.UI
{
    public class PlayerCard : MonoBehaviour
    {
        public TMP_Text playerName;
        public Button playerKick;
        public SteamPlayer linkedPlayer;
        public Lobby currentLobby;

        public void KickButton()
        {
            if (linkedPlayer == null) return; 
            KickPlayerServerRpc(currentLobby, linkedPlayer);

            if (currentLobby.IsOwnedBy(SteamClient.SteamId))
            {
                Debug.Log("TO DO!!!!!!!!!!!!!!!!!!!!");
            }
        }

        [ServerRpc]
        public void KickPlayerServerRpc(Lobby lobby, SteamPlayer player)
        {
            LeaveLobbyClientRpc(lobby, player);
            if (player.playerSteamId != SteamClient.SteamId) return;

            Debug.Log("Kicked yourselfff --- " + player.playerName);
            SteamManager.Instance.Disconnect();

            SceneManager.LoadScene("SteamBoot", LoadSceneMode.Single);
        }

        [ClientRpc]
        public void LeaveLobbyClientRpc(Lobby lobby, SteamPlayer player)
        {
            Debug.Log("Kicked Player --- " + player.playerName);
        }
    }
}

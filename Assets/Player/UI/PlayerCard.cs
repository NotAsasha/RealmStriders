using Steam;
using Steamworks;
using Steamworks.Data;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        if (player.PlayerSteamId != SteamClient.SteamId) return;

        Debug.Log("Kicked yourselfff --- " + player.PlayerName);
        SteamManager.Instance.Disconnect();

        SceneManager.LoadScene("SteamBoot", LoadSceneMode.Single);
    }

    [ClientRpc]
    public void LeaveLobbyClientRpc(Lobby lobby, SteamPlayer player)
    {
        Debug.Log("Kicked Player --- " + player.PlayerName);
    }
}

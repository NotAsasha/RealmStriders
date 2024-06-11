using Player;
using Steam;
using Steamworks;
using Steamworks.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PauseUIManager : MonoBehaviour
{
    public GameObject pauseMenu;
    public Transform cardsParent;
    public GameObject playerCard;
    void Start()
    {
        SteamMatchmaking.OnLobbyDataChanged += UpdatePauseUI;
        Debug.Log(Movement._controls);
        Movement._controls.System.Pause.performed += UpdateMenuState;
    }
    private void Update()
    {
       if (Input.GetKeyDown(KeyCode.U)) { UpdatePauseUI(SteamManager.Instance.CurrentLobby.Value); }
    }
    void UpdateMenuState(InputAction.CallbackContext obj)
    {
        pauseMenu.SetActive(!pauseMenu.activeSelf); 
    }
    void UpdatePauseUI(Lobby lobby)
    {
        List<SteamPlayer> playerList = GetLobbyMembers(lobby);
        Debug.Log("Players count -- " + playerList.Count);
        DestroyChildren(cardsParent);
        foreach (SteamPlayer player in playerList)
        {
            var currentCard = Instantiate(playerCard, cardsParent).GetComponent<PlayerCard>();
            currentCard.linkedPlayer = player;
            currentCard.playerName.text = player.PlayerName;
            
        }
    }
    public List<SteamPlayer> GetLobbyMembers(Lobby lobby)
    {
        List<SteamPlayer> playerList = new();
        foreach (Friend member in lobby.Members)
        {
            Image? playerImage = member.GetSmallAvatarAsync().Result;
            SteamPlayer player = new(member.Name, member.Id, playerImage, member);
            playerList.Add(player);
            Debug.Log(player.PlayerName);
        }
        return playerList;
    }
    void DestroyChildren(Transform parent)
    {
        foreach (Transform child in parent) Destroy(child.gameObject);
    }
}

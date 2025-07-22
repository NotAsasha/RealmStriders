using UnityEngine;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
public class ToggleOnLobbyJoin : MonoBehaviour
{
    public bool ToggleState = false;
    void Start()
    {
        NetworkManager.Singleton.OnClientStarted += Toggle;

    }

    void Toggle()
    {
        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        NetworkManager.Singleton.OnClientStarted -= Toggle;
    }
}

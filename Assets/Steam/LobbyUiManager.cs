using Steam;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;
namespace Menu
{
    public class LobbyUiManager : MonoBehaviour
    {
        [Header("Steam")]
        [SerializeField] private SteamManager _steamManager;
        [SerializeField] private Toggle _friendsOnly;

        public void CreateLobbyButton()
        {
            _steamManager.StartHost(6, _friendsOnly.isOn);
        }
    }
}

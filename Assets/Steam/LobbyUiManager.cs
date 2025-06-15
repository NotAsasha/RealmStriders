using FileSystem;
using Steam;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;
namespace Menu
{
    public class LobbyUiManager : MonoBehaviour
    {
        [Header("SetUp")]
        [SerializeField] private Toggle _friendsOnly;
        private SteamManager _steamManager;

        public void CreateLobbyButton()
        {
            _steamManager = SteamManager.Instance;
            _steamManager.StartHost(6, _friendsOnly.isOn);
        }

        public void DeleteFiles()
        {
            GameFileHandler.Instance.DeleteAvaible();
        }
    }
} 

using FileSystem.Scripts;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Steam
{
    public class LobbyUiManager : MonoBehaviour
    {
        [FormerlySerializedAs("_friendsOnly")]
        [Header("SetUp")]
        [SerializeField] private Toggle friendsOnly;
        private SteamManager steamManager;

        public void CreateLobbyButton()
        {
            steamManager = SteamManager.Instance;
            steamManager.StartHost(6, friendsOnly.isOn);
            NetworkManager.Singleton.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
        }

        public void DeleteFiles()
        {
            GameFileHandler.Instance.DeleteAvaible();
        }
    }
} 

using System;
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

            if (steamManager != null)
            {
                try
                {
                    steamManager.StartHost(6, friendsOnly.isOn);
                }
                catch
                {
                    throw new Exception("Cannot start the steam lobby");
                }
            }
            
            NetworkManager.Singleton.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
        }

       

        public void DeleteFiles()
        {
            GameFileHandler.Instance.DeleteAvaible();
        }
    }
} 

using System;
using FileSystem.Scripts;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Steam
{
    public class LobbyUiManager : MonoBehaviour
    {
        [Header("UI Toggle")]
        [SerializeField] private Toggle friendsOnly;

        private SteamManager steamManager;

        public void Start()
        {
            steamManager = SteamManager.Instance;
        }

        public void CreateLobbyButton()
        {
            if (steamManager == null) return;
            try
            {
                steamManager.StartHost(6, friendsOnly.isOn);
            }
            catch
            {
                throw new Exception("Cannot start the steam lobby");
            }
        }

        public void DeleteFiles()
        {
            GameFileHandler.Instance.DeleteAvaible();
        }
    }
} 

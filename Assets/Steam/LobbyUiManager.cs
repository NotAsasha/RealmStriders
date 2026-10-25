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

        private void Awake()
        {
            UnlockCursor();
        }

        private void OnEnable()
        {
            UnlockCursor();
        }

        public void Start()
        {
            UnlockCursor();
            steamManager = SteamManager.Instance;
        }

        private static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void CreateLobbyButton()
        {
            if (steamManager == null)
            {
                steamManager = SteamManager.Instance;
            }
            if (steamManager == null)
            {
                Debug.LogError("[LobbyUiManager] SteamManager instance is not available.");
                return;
            }

            try
            {
                bool isFriendsOnly = friendsOnly != null && friendsOnly.isOn;
                steamManager.StartHost(6, isFriendsOnly);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LobbyUiManager] Error starting host: {ex}");
                throw;
            }
        }

        public void DeleteFiles()
        {
            GameFileHandler.Instance.DeleteAvaible();
        }
    }
} 

using Player.Network;
using Steam;
using Steamworks;
using Steamworks.Data;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Player.UI
{
    public class PlayerCard : MonoBehaviour
    {
        public TMP_Text playerName;
        public RawImage playerImage;
        public SteamPlayer linkedPlayer;
        public Lobby currentLobby;

        [SerializeField] private Slider volumeSlider;
        [SerializeField] private GameObject volumeSliderContainer;

        [HideInInspector] public bool isFoundInLobby;

        private void OnEnable()
        {
            VoiceVolumeManager.OnVolumeChanged += OnVolumeChangedExternally;
            InitializeSlider();
        }

        private void OnDisable()
        {
            VoiceVolumeManager.OnVolumeChanged -= OnVolumeChangedExternally;
        }

        private void InitializeSlider()
        {
            if (volumeSlider == null || linkedPlayer == null) return;

            bool isLocalPlayer = SteamClient.IsValid && linkedPlayer.playerSteamId == SteamClient.SteamId;

            // Слайдер власного гравця прихований — немає сенсу регулювати власний голос
            if (volumeSliderContainer != null)
                volumeSliderContainer.SetActive(!isLocalPlayer);

            if (isLocalPlayer) return;

            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = VoiceVolumeManager.MAX_VOLUME;
            volumeSlider.value = VoiceVolumeManager.GetVolumeBySteamId(linkedPlayer.playerSteamId);
        }

        /// <summary>
        /// Викликається з UnityEvent слайдера при зміні значення користувачем.
        /// </summary>
        public void OnVolumeSliderChanged(float value)
        {
            if (linkedPlayer == null) return;
            VoiceVolumeManager.SetVolumeBySteamId(linkedPlayer.playerSteamId, value);
        }

        /// <summary>
        /// Оновлює слайдер, якщо гучність змінилась ззовні (наприклад, після реєстрації VoiceChat).
        /// </summary>
        private void OnVolumeChangedExternally(ulong clientId, float volume)
        {
            // Якщо нас не стосується ця подія — ігноруємо
            if (linkedPlayer == null || volumeSlider == null) return;
            if (!VoiceVolumeManager.IsRegistered(linkedPlayer.playerSteamId)) return;

            // Оновлюємо слайдер тільки якщо значення відрізняється (щоб не зациклитись)
            float current = VoiceVolumeManager.GetVolumeBySteamId(linkedPlayer.playerSteamId);
            if (!Mathf.Approximately(volumeSlider.value, current))
                volumeSlider.SetValueWithoutNotify(current);
        }

        public void KickButton()
        {
            if (linkedPlayer == null) return; 
            KickPlayerServerRpc(currentLobby, linkedPlayer);

            if (SteamClient.IsValid && currentLobby.IsOwnedBy(SteamClient.SteamId))
            {
                Debug.Log("TO DO!!!!!!!!!!!!!!!!!!!!");
            }
        }

        [ServerRpc]
        public void KickPlayerServerRpc(Lobby lobby, SteamPlayer player)
        {
            LeaveLobbyClientRpc(lobby, player);
            if (SteamClient.IsValid && player.playerSteamId != SteamClient.SteamId) return;

            Debug.Log("Kicked yourselfff --- " + player.playerName);
            SteamManager.Instance.Disconnect();

            SceneManager.LoadScene("SteamBoot", LoadSceneMode.Single);
        }

        [ClientRpc]
        public void LeaveLobbyClientRpc(Lobby lobby, SteamPlayer player)
        {
            Debug.Log("Kicked Player --- " + player.playerName);
        }

        public static Texture2D ImageFromBytes(int width, int height, byte[] data)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.LoadRawTextureData(data);
            texture.Apply();
            return texture;
        }
    }
}

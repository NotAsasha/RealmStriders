using Steam;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;
public class LobbyUiManager : MonoBehaviour
{
    [Header("Steam")]
    [SerializeField] private SteamManager _steamManager;
    [SerializeField] private Toggle _friendsOnly;
    [SerializeField] private Slider _sensSlider;

    private float _sensValue;
    private void Start()
    {
        // SteamMatchmaking.OnLobbyDataChanged += UpdateLobbyMembers;
        _sensValue = PlayerPrefs.GetFloat("MouseSensativity");
        if (_sensValue == 0)
        {
            UpdateMouseSensativity();
        }
        _sensSlider.value = _sensValue;
    }
    public void CreateLobbyButton()
    {
        _steamManager.StartHost(6, _friendsOnly.isOn);
    }
    public void UpdateMouseSensativity()
    {
        _sensValue = _sensSlider.value;
        PlayerPrefs.SetFloat("MouseSensativity", _sensValue);
    }
}

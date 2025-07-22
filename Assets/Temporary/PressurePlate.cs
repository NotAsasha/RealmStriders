using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FileSystem;
using Unity.Netcode;
using TMPro;
public class PressurePlate : NetworkBehaviour, ICollidable
{
    [SerializeField] private string _saveFileName;
    [SerializeField] private GameObject _pressurePlate;
    [SerializeField] private TMP_Text _pressesCounter;
    [SerializeField] private Color _normalColor;
    [SerializeField] private Color _pressesColor;
    private GameFileHandler _fileHandler;
    private TestGameFile testGameFile;

    private void Awake()
    {
        _fileHandler = GameFileHandler.Instance;
        testGameFile = (TestGameFile)_fileHandler.SearchForFileByName(_saveFileName);
        _pressesCounter.text = "Button press count: " + testGameFile.buttonPresses;
    }

    public void OnColliderEnter(GameObject collider)
    {
        CallButtonPressServerRpc();
    }

    [ClientRpc]
    private void UiUpdateClientRPC(int pressesAmount)
    {
        _pressesCounter.text = "Button press count: " + pressesAmount;
    }
    [ServerRpc]
    public void CallButtonPressServerRpc()
    {
        testGameFile.AddButtonClick();
        testGameFile.Save(false);
        UiUpdateClientRPC(testGameFile.buttonPresses);
      //  _pressesCounter.text = "Button press count: " + testGameFile.buttonPresses;
    }
}

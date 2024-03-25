using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FileSystem;
using Unity.VisualScripting;
using Unity.Netcode;
using TMPro;
using Unity.Burst.CompilerServices;
public class PressurePlate : NetworkBehaviour
{
    [SerializeField] private string _saveFileName;
    [SerializeField] private GameObject _pressurePlate;
    [SerializeField] private TMP_Text _pressesCounter;
    [SerializeField] private Color _normalColor;
    [SerializeField] private Color _pressesColor;
    private GameFileHandler _fileHandler;
    private TestGameFile testGameFile;

    private void Start()
    {
        _fileHandler = GameFileHandler.Instance;
        testGameFile = (TestGameFile)_fileHandler.SearchForFileByName(_saveFileName);
        _pressesCounter.text = "Button press count: " + testGameFile.buttonPresses;
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
    }
}

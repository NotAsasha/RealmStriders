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
        foreach (var file in _fileHandler.availableFiles)
        {
            if (file.FileName == _saveFileName)
            {
                testGameFile = (TestGameFile)file;
                break;
            }
        };
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
    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q)) { testGameFile.Save(true); }
        if (Input.GetKeyDown(KeyCode.E)) { testGameFile.Load(true); }
    }
}

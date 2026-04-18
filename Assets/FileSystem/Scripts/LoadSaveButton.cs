using System;
using FileSystem.Scripts;
using TMPro;
using UnityEngine;
using FileSystem;

public class LoadSaveButton : MonoBehaviour
{
    public SaveFile gameSave;

    public TMP_Text saveText;


    private void Start()
    {
        saveText.text = $"Rating: {gameSave.save.teamRating}\n Money: {gameSave.save.teamMoney}";
    }

    public void LoadSave()
    {
        GameManager.Instance.currentSave = gameSave;
    }
}

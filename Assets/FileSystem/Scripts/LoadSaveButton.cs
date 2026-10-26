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
            RefreshText();
        }

        public void RefreshText()
        {
            if (saveText == null) return;
            saveText.text = gameSave != null && gameSave.save != null
                ? $"Rating: {gameSave.save.teamRating}\nMoney: {gameSave.save.teamMoney}"
                : "Empty save";
        }

    public void LoadSave()
    {
        GameManager.Instance.currentSave = gameSave;
    }
}

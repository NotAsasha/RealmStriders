using System;
using FileSystem.Scripts;
using TMPro;
using UnityEngine;
using FileSystem;

public class LoadSaveButton : MonoBehaviour
{
    public SaveFile gameSave;

    public TMP_Text saveText;
    [SerializeField] private GameObject completedStar;

    private void Start() => RefreshText();
    private void OnEnable() => RefreshText();

    public void RefreshText()
    {
        bool available = gameSave != null && !gameSave.IsLoadBlocked && gameSave.save != null;
        if (completedStar != null)
            completedStar.SetActive(available && gameSave.save.campaignCompleted);
        if (saveText == null) return;
        saveText.text = gameSave != null && gameSave.IsLoadBlocked ? "Save unavailable" : available
            ? $"Rating: {gameSave.save.teamRating}\nMoney: {gameSave.save.teamMoney}"
            : "Empty save";
    }

    public void LoadSave()
    {
        if (gameSave == null || gameSave.IsLoadBlocked) return;
        GameManager.Instance.currentSave = gameSave;
    }
}

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
        if (completedStar != null)
            completedStar.SetActive(gameSave != null && gameSave.save != null && gameSave.save.campaignCompleted);
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

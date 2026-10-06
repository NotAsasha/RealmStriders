using System;
using UnityEngine;
using UnityEngine.Events;

public class SaveGameButton : MonoBehaviour
{
    public static event Action<bool> SaveCompleted;

    [SerializeField] private UnityEvent<bool> onSaveResult;

    public void SaveGame()
    {
        if (GameManager.Instance.hasStartedMission.Value)
        {
            PublishResult(false);
            return;
        }

        bool succeeded = GameManager.Instance.currentSave != null
            && GameManager.Instance.currentSave.Save();
        PublishResult(succeeded);
    }

    private void PublishResult(bool succeeded)
    {
        onSaveResult?.Invoke(succeeded);
        SaveCompleted?.Invoke(succeeded);
    }
}
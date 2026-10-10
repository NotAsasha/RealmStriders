using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class SaveGameButton : NetworkBehaviour
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
        if (IsSpawned && IsServer)
        {
            SaveResultClientRpc(succeeded);
            return;
        }

        NotifyResult(succeeded);
    }

    [Rpc(SendTo.Everyone)]
    private void SaveResultClientRpc(bool succeeded)
    {
        NotifyResult(succeeded);
    }

    private void NotifyResult(bool succeeded)
    {
        onSaveResult?.Invoke(succeeded);
        SaveCompleted?.Invoke(succeeded);
    }
}
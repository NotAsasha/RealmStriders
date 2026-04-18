using UnityEngine;

public class SaveGameButton : MonoBehaviour
{
    public void SaveGame() => GameManager.Instance.currentSave.Save(false);
}

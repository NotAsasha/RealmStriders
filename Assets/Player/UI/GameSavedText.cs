using UnityEngine;
using TMPro;

public class GameSavedText : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] Animator animator;

    private void Awake()
    {
        if (text == null || animator == null)
        {
            throw new System.NullReferenceException();
        }
    }

    void Start()
    {
        SaveGameButton.SaveCompleted += GameSavedAnimation;
    }

    private void GameSavedAnimation(bool isSaved)
    {
        text.text = isSaved ? "saved" : "Saving is unavailable.";
        animator.SetTrigger("SaveTriggered");
    }
}

using UnityEngine;
using TMPro;

public class GameSavedText : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private Animator animator;

    private void Awake()
    {
        if (text == null || animator == null)
        {
            throw new System.NullReferenceException();
        }
    }

    private void OnEnable()
    {
        SaveGameButton.SaveCompleted += GameSavedAnimation;
    }

    private void OnDisable()
    {
        SaveGameButton.SaveCompleted -= GameSavedAnimation;
    }

    private void GameSavedAnimation(bool isSaved)
    {
        text.text = isSaved ? "Game saved." : "Game could not be saved.";
        animator.SetTrigger("SaveTriggered");
    }
}

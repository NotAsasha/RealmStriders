using UnityEngine;
using Enemy;
using UnityEngine.AI;

public class EnemyAnimation : MonoBehaviour
{
    public Enemy.Enemy enemy;

    private void Awake()
    {
        if (TryGetComponent<Animator>(out Animator animator))
        {
            enemy.onStateChanged += (state) => UpdateAnimation(animator, state);
        }
        else
        {
            Debug.LogWarning("No Animator component found on GrabarAnimation.");
        }
    }

    private void UpdateAnimation(Animator animator, EnemyState state)
    {
        switch (state)
        {
            case EnemyState.IsMoving:
                animator.SetBool("IsMoving", true);
                animator.SetBool("IsChasingPlayer", false);
                break;
            case EnemyState.IsChasingPlayer:
                animator.SetBool("IsMoving", false);
                animator.SetBool("IsChasingPlayer", true);
                break;
            default:
                animator.SetBool("IsMoving", false);
                animator.SetBool("IsChasingPlayer", false);
                break;
        }
    }
    private void OnDestroy()
    {
        if (enemy != null)
        {
            enemy.onStateChanged -= (state) => UpdateAnimation(GetComponent<Animator>(), state);
        }
    }
}

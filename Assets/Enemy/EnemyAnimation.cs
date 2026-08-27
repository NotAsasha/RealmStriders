using UnityEngine;
using Enemy;

public class EnemyAnimation : MonoBehaviour
{
    [SerializeField] private Enemy.Enemy enemy;
    [SerializeField] private Animator animator;
    [SerializeField] private float speedModifier = 0.25f;
    [SerializeField] private float stopThreshold = 0.05f;

    private Vector3 lastPosition;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (enemy == null) enemy = GetComponentInParent<Enemy.Enemy>();
        lastPosition = transform.position;
    }

    private void Update()
    {
        if (animator == null) return;

        float distance = Vector3.Distance(transform.position, lastPosition);
        float speed = distance / Time.deltaTime;
        lastPosition = transform.position;

        if (speed < stopThreshold)
        {
            // Зупиняємо анімацію повністю, якщо ворог вперся в стіну чи зупинився
            animator.speed = 0f;
            animator.SetBool("IsMoving", false);
        }
        else
        {
            // Швидкість програвання анімації синхронна зі швидкістю переміщення
            animator.speed = Mathf.Lerp(animator.speed, speed * speedModifier, Time.deltaTime * 10f);
            animator.SetBool("IsMoving", true);
        }
    }
}
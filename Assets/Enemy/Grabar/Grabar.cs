using UnityEngine;
using Enemy;
using Unity.Netcode;

public class Grabar : Enemy.Enemy
{
    [Header("Eye Settings")]
    [SerializeField] private Transform eye;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float minLookInterval = 2f;
    [SerializeField] private float maxLookInterval = 5f;
    [SerializeField] private float forwardLookDistance = 5f;


    [SerializeField] protected EntityDetector eyeDetector;

    [Header("Misc")]
    public NetworkVariable<Vector3> eyeTarget = new NetworkVariable<Vector3>(
        Vector3.forward, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private float lookTimer;

    protected override void Awake()
    {
        base.Awake();
        if (eyeDetector != null) vision = eyeDetector;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            ResetLookTimer();
            // Початковий погляд вперед
            eyeTarget.Value = eye.position + transform.forward * forwardLookDistance;
        }
    }

    protected override void Update()
    {
        base.Update();

        // Поворот ока працює на всіх клієнтах і хості плавно
        if (eye != null)
        {
            Vector3 direction = eyeTarget.Value - eye.position;

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                eye.rotation = Quaternion.Slerp(eye.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }

    protected override bool ChasePlayer(bool countEnemies = false)
    {
        var player = vision.EntityInSight(countEnemies);

        if (player != null)
        {
            Vector3 target = player.transform.position;
            if ((agent.destination - target).sqrMagnitude > 0.5f * 0.5f)
            {
                agent.SetDestination(target);
            }
            enemyState = EnemyState.IsChasingPlayer;

            // Якщо бачить гравця — фокусується суворо на ньому
            eyeTarget.Value = target;
        }
        else
        {
            // Якщо ворог не бачить гравця — оглядається довкола
            if (IsServer)
            {
                HandleRandomEyeLook();
            }
        }

        return (player != null);
    }

    private void HandleRandomEyeLook()
    {
        lookTimer -= thinkCooldown;

        if (lookTimer <= 0f)
        {
            ResetLookTimer();

            // Випадковий кут відхилення вліво/вправо
            float randomAngle = Random.Range(-360f, 360f);

            // Повертаємо вектор forward на цей кут
            Vector3 lookDirection = Quaternion.Euler(0f, randomAngle, 0f) * transform.forward;

            // Отримуємо точку на відстані forwardLookDistance
            eyeTarget.Value = eye.position + lookDirection * forwardLookDistance;
        }
    }

    private void ResetLookTimer()
    {
        lookTimer = Random.Range(minLookInterval, maxLookInterval);
    }
}
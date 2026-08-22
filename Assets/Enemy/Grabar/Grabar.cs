using UnityEngine;
using Enemy;
using Unity.Netcode;
public class Grabar : Enemy.Enemy
{
    [Header("Eye Settings")]
    [SerializeField] private Transform eye;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Misc")]
    public NetworkVariable<Vector3> eyeTarget = new NetworkVariable<Vector3>(
        Vector3.forward, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    protected override void Update()
    {
        base.Update();

        if (eyeTarget.Value != null)
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
            eyeTarget.Value = target;
        }
        else
        {
            eyeTarget.Value = Vector3.forward;
        }
        return (player != null);
    }
}

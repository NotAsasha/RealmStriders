using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;
using System.Collections;
using System.Drawing;
using UnityEngine.Audio;

public class Enemy : Entity, ICollidable
{
    public float damage = 1f;
    public bool overAggresive = false;
    public float moveRange = 10f;
    public float defaultSpeed = 5f;

    public float stepSoundCooldown = 0.3f;
    public AudioSource audioSource;
    public AudioResource[] stepSounds;

    protected EnemyState enemyState = EnemyState.isMoving;

    protected Animator animator;
    protected EntityDetector vision;
    protected NavMeshAgent agent;
    protected Rigidbody playerRigidbody;
    protected Collider collider1;

    private bool isLocalFreezed = false;

    #region Initialization

    private void Start()
    {
        animator = GetComponent<Animator>();
        vision = GetComponent<EntityDetector>();
        agent = GetComponent<NavMeshAgent>();
        playerRigidbody = GetComponent<Rigidbody>();
        collider1 = GetComponent<Collider>();
        agent.speed = defaultSpeed;
        ToggleRagdoll(true);
    }

    #endregion

    #region Entity

    override protected void KillEntity()
    {
        if (!IsOwner) return;
        ToggleRagdoll(false);
    }

    override protected void OnFreezeStateChange(bool oldV, bool isFreezed)
    {
        animator.speed = isFreezed ? 0 : 1;
        agent.speed = isFreezed ? 0 : defaultSpeed;
        isLocalFreezed = isFreezed;
    }

    private void ToggleRagdoll(bool isActive)
    {
        Debug.Log($"ToggleRagdoll, is entity alive - {isActive}");
        animator.enabled = isActive;
        vision.enabled = isActive;
        agent.enabled = isActive;
        //playerRigidbody.isKinematic = isActive;
        collider1.isTrigger = isActive;
        Debug.Log($"playerRigidbody.isKinematic {playerRigidbody.isKinematic}");
    }

    #endregion

    #region ICollidable

    public void OnColliderEnter(GameObject collider)
    {
        if (!IsServer || isDead.Value || isLocalFreezed || !GameManager.instance.hasStartedMission.Value) return;
        var player = collider.GetComponent<Entity>();
        if (player == null || player.isDead.Value) return;
        if (!overAggresive && collider.GetComponent<Enemy>() != null) return;

        BiteClientRpc(collider);
        player.AddHealth(-damage);
    }

    [ClientRpc]
    virtual protected void BiteClientRpc(NetworkObjectReference obj)
    {
        obj.TryGet(out NetworkObject player);
        Debug.Log($"---Enemy: Eaten {player.name}");
    }

    #endregion

    private float nextUpdate;
    void Update()
    {
        if (!IsServer || isDead.Value || isLocalFreezed) return;

        vision.DrawViewState(); //draw vision boundaries
        if (Time.time >= nextUpdate)
        {
            nextUpdate = Time.time + 0.3f + Random.Range(0f, 0.1f);
            Think();
        }
        if (isSoundReady && agent.velocity.magnitude > 0.1) PlayStepsSound(stepSoundCooldown);
    }

    protected virtual void Think()
    {

        //first priority, run
        if (enemyState == EnemyState.isRunning)
        {
            if (agent.remainingDistance > agent.stoppingDistance) return;
            enemyState = EnemyState.isMoving;
        }

        //second priority, search for player
        if (ChasePlayer(overAggresive)) return;

        //third priority, go to the target location
        if (agent.remainingDistance > agent.stoppingDistance & agent.pathStatus == NavMeshPathStatus.PathComplete) return; //not done with path

        //forth priority, go somewhere
        Move();
    }

    public void Run()
    {
        Move();
        enemyState = EnemyState.isRunning;
    }

    protected virtual bool ChasePlayer(bool _countEnemies = false)
    {
        var player = vision.EntityInSight(_countEnemies);
        if (player != null)
        {
            Vector3 target = player.transform.position;
            if ((agent.destination - target).sqrMagnitude > 0.5f * 0.5f)
            {
                agent.SetDestination(target);
            }
            enemyState = EnemyState.isChasingPlayer;
        }
        return (player != null);
    }

    public void Lure(Vector3 _coords)
    {
        if (!IsServer || enemyState < EnemyState.isChasingSound) return;
        if (agent.SetDestination(_coords))
        {
            enemyState = EnemyState.isChasingSound;
        }
    }

    protected virtual void Move()
    {
        Vector3 point;
        if (RandomMove.RandomPoint(transform.position, moveRange, out point)) //choose where to go
        {
            Debug.DrawRay(point, Vector3.up, UnityEngine.Color.blue, 1.0f); //so you can see with gizmos
            agent.SetDestination(point);
            enemyState = EnemyState.isMoving;
        }
    }

    bool isSoundReady = true;
    private void PlayStepsSound(float cooldown)
    {
        isSoundReady = false;
        int soundsCount = stepSounds.Length;
        audioSource.resource = stepSounds[Random.Range(0, soundsCount)];
        audioSource.Play();
        StartCoroutine(SoundTimer(cooldown));
    }

    private IEnumerator SoundTimer(float cooldown)
    {
        yield return new WaitForSeconds(cooldown);
        isSoundReady = true;
        audioSource.Stop();
    }
}
public enum EnemyState
{
    isRunning = 0,
    isChasingPlayer = 1,
    isChasingSound = 2,
    isMoving = 3,
}

using NUnit.Framework.Internal.Filters;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

public class LureMan : Enemy
{



    protected override void Think()
    {
        //first priority, run
        if (enemyState == EnemyState.isRunning)
        {
            if (agent.remainingDistance > agent.stoppingDistance) return;
            enemyState = EnemyState.isMoving;
        }

        //second priority, search for player
        if (ChasePlayer(overAggresive)) return;
    }

}

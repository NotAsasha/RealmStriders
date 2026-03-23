namespace Enemy.LureMan
{
    public class LureMan : Enemy
    {



        protected override void Think()
        {
            //first priority, run
            if (enemyState == EnemyState.IsRunning)
            {
                if (agent.remainingDistance > agent.stoppingDistance) return;
                enemyState = EnemyState.IsMoving;
            }

            //second priority, search for player
            if (ChasePlayer(overAggresive)) return;
        }

    }
}

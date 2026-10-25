using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Enemy
{
    public class EnemySpawner : NetworkBehaviour
    {
        [SerializeField] private EnemyPool[] enemiesByDanger;

        [SerializeField] Vector3 worldCenter = new(0f,0f,0f);
        [SerializeField] float portalArea = 30f;

        [SerializeField] float spawnRadius = 100f;

        [SerializeField] Vector3 portalPos = new(3f, 0f, 0f);

        //void Start()
        //{
        //    TestDistribution();
        //}

        // Spawns enemies from a pre-rolled danger list generated at mission-selection time.
        public void SpawnEnemies(FixedList32Bytes<int> dangerLevels)
        {
            if (!IsServer) return;

            for (int i = 0; i < dangerLevels.Length; i++)
            {
                int danger = dangerLevels[i];
                if (danger < 1 || danger > enemiesByDanger.Length) continue;

                var pool = enemiesByDanger[danger - 1].enemies;
                if (pool == null || pool.Length == 0) continue;

                if (!RandomMapPoint(out var position)) continue;
                var prefab = pool[Random.Range(0, pool.Length)];
                var enemyObj = Instantiate(prefab, position, Quaternion.identity);
                SceneManager.MoveGameObjectToScene(enemyObj, GameManager.Instance.missionScene);
                var enemy = enemyObj.GetComponent<NetworkObject>();
                enemy.Spawn(true);
                GameManager.Instance.activeEnemies.Add(enemy.GetComponent<Enemy>());
            }
        }

        public static int RandomEnemiesNumber(int teamRating)
        {
            // You always start with a single enemy.
            // With every star(3 rating) you acquire, max enemies count increases by one
            int clampedRating = Mathf.Max(1, teamRating);
            int maxCount = (int)Mathf.Ceil(clampedRating / 3.0f);
            int minCount = (int)Mathf.Ceil(clampedRating / 5.0f);

            return Random.Range(minCount, maxCount + 1);
        }

        public static int GetMaxDanger(int teamRating)
        {
            // Star 1 (Rating 1-3): Danger 1-2 only (tiers 3, 4, 5 are strictly prohibited)
            // Star 2 (Rating 4-6): up to Danger 3
            // Star 3 (Rating 7-9): up to Danger 4
            // Star 4-5 (Rating 10+): up to Danger 5
            if (teamRating <= 3) return 2;
            if (teamRating <= 6) return 3;
            if (teamRating <= 9) return 4;
            return 5;
        }

        public static List<int> CalculateDangers(int teamRating, int enemiesNumber)
        {
            int maxDanger = GetMaxDanger(teamRating);
            float[] weights = new float[maxDanger];

            int clampedRating = Mathf.Clamp(teamRating, 1, 15);
            // Continuous target danger scales smoothly from 0.5 at rating 1 to 5.0 at rating 15
            float targetDanger = Mathf.Min(5.0f, 0.5f + (clampedRating - 1) * (4.5f / 14.0f));
            const float sigma = 0.85f;
            const float twoSigmaSq = 2f * sigma * sigma;

            for (int i = 0; i < maxDanger; ++i)
            {
                float diff = (i + 1) - targetDanger;
                weights[i] = Mathf.Exp(-(diff * diff) / twoSigmaSq);
            }

            // Randomly choose danger levels for each enemy
            List<int> dangers = new(enemiesNumber);
            for (int i = 0; i < enemiesNumber; ++i)
            {
                dangers.Add(ChooseWeight(weights) + 1);
            }
            return dangers;
        }

        private static int ChooseWeight(float[] weights)
        {
            if (weights == null || weights.Length == 0) return 0;

            float sum = 0f;
            for (int i = 0; i < weights.Length; ++i)
            {
                sum += weights[i];
            }

            if (sum <= 0f) return 0;

            float rand = Random.value * sum;
            float accumulated = 0f;

            for (int i = 0; i < weights.Length; ++i)
            {
                accumulated += weights[i];
                if (rand <= accumulated) return i;
            }

            return weights.Length - 1;
        }

        // CRITICAL: enemies can possibly choose to spawn inside player base... TODO
        public bool RandomMapPoint(out Vector3 position)
        {
            Vector3 searchPos;
            for (int i = 0; i < 1000; ++i)
            {
                if (!RandomMove.RandomPoint(worldCenter, spawnRadius, out searchPos)) continue;
                Debug.Log($"---EnemySpawner: Tried {searchPos}");

                if (searchPos.y < -30f) continue;

                if (Vector3.Distance(searchPos, portalPos) > portalArea)
                {
                    position = searchPos;
                    return true;
                }
            }
            position = worldCenter;
            Debug.LogError($"---EnemySpawner: Nooo location found...");
            return false;
        }

        #region Debug

        private void TestDistribution()
        {
            for (int i = 1; i < 16; ++i)
            {
                int[] star = new int[5];

                float avgAmount = 0;

                for (int j = 0; j < 500; ++j)
                {
                    int enemyNum = RandomEnemiesNumber(i);
                    avgAmount = (avgAmount * j + enemyNum) / (j + 1);

                    var dangers = CalculateDangers(i, enemyNum);

                    foreach (var danger in dangers)
                    {
                        star[danger - 1] += 1;
                    }
                }
                Debug.Log($"Witin a rating: {i}, avarage enemies number was: {avgAmount}, and danger stats are: {star[0]}, {star[1]}, {star[2]}, {star[3]}, {star[4]}");
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(worldCenter, portalArea);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(worldCenter, spawnRadius);
        }

        #endregion
    }

    [System.Serializable]
    public class EnemyPool
    {
        public GameObject[] enemies;
    }
}

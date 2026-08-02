using System.Collections.Generic;
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

        public void SpawnEnemies(int teamRating, int enemiesNumber)
        {

            if (!IsServer) return;

            List<int> dangers = CalculateDangers(teamRating, enemiesNumber);

            foreach (int danger in dangers)
            {
                var pool = enemiesByDanger[danger - 1].enemies;
                if (pool.Length == 0) continue;
            
                RandomMapPoint(out var position);
                var prefab = pool[Random.Range(0, pool.Length)];
                var enemyObj = Instantiate(prefab,
                    position + Vector3.up * 1.5f,
                    Quaternion.identity
                );
                SceneManager.MoveGameObjectToScene(enemyObj, GameManager.Instance.missionScene);
                var enemy = enemyObj.GetComponent<NetworkObject>();
                enemy.Spawn(true); 
                GameManager.Instance.activeEnemies.Add(enemy.GetComponent<Enemy>());
            }
        }

        public static int RandomEnemiesNumber(int teamRating)
        {
            //You always start with a single enemy.
            //With every star(3 rating) you are starting to aquire, max enemies count increases by one
            int maxCount = (int)Mathf.Ceil(teamRating / 3.0f);
            int minCount = (int)Mathf.Ceil(teamRating / 5.0f);

            return Random.Range(minCount, maxCount + 1);
        }

        public List<int> CalculateDangers(int teamRating, int enemiesNumber)
        {
            int maxDanger = teamRating > 8 ? 5 : teamRating > 5 ? 4 : 3;
            float[] weights = new float[maxDanger];

            //Gives spawn probability for every enemy, depending on rating
            for (int i = 0; i < maxDanger; ++i)
            {
                weights[i] = Mathf.Pow(teamRating / 15f * (i + 1) / 5f, (i + 1));
            }

            //Randomly choose few dangers
            List<int> dangers = new(enemiesNumber);

            for (int i = 0; i < enemiesNumber; ++i)
            {
                dangers.Add(ChooseWeight(weights) + 1);
            }
            return dangers;
        }

        private static int ChooseWeight(float[] weights)
        {
            float sum = 0;
            foreach (float weight in weights)
            {
                sum += weight;
            }

            //Random float within sum range
            float rand = Random.value * sum;

            //Search where on range it landed by subtracting every weight 
            for (int i = 0; i < weights.Length; ++i)
            {
                if (rand < weights[i]) return i;
                rand -= weights[i];
            }

            //If nothing worked, choose last (spawn the strongest enemy ;)
            return weights.Length - 1;
        }

        // CRITICAL: enemies can possibly choose to spawn inside player base... TODO
        public bool RandomMapPoint(out Vector3 position)
        {
            Vector3 searchPos;
            for (int i = 0; i < 100; ++i)
            {
                if (!RandomMove.RandomPoint(worldCenter, spawnRadius, out searchPos)) continue;
                Debug.Log($"---EnemySpawner: Tried {searchPos}");

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
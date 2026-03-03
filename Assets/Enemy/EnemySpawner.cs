using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class EnemySpawner : NetworkBehaviour
{
    [SerializeField] private EnemyPool[] enemiesByDanger;

    [SerializeField] Vector3 worldCenter = new(0f,50f,0f);
    [SerializeField] float portalArea = 30f;

    [SerializeField] float spawnRadius = 10f;

    private Vector3 portalPos = new(3f, 50f, 0f);

    //void Start()
    //{
    //    TestDistribution();
    //}

    public void SpawnEnemies(int _teamRating, int _enemiesNumber)
    {

        if (!IsServer) return;

        List<int> dangers = CalculateDangers(_teamRating, _enemiesNumber);

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
            SceneManager.MoveGameObjectToScene(enemyObj, GameManager.instance.missionScene);
            var enemy = enemyObj.GetComponent<NetworkObject>();
            enemy.Spawn(true); 
            GameManager.instance.activeEnemies.Add(enemy.GetComponent<Enemy>());
        }
    }

    public static int RandomEnemiesNumber(int _teamRating)
    {
        //You always start with a single enemy.
        //With every star(3 rating) you are starting to aquire, max enemies count increases by one
        int maxCount = (int)Mathf.Ceil(_teamRating / 3.0f);
        int minCount = (int)Mathf.Ceil(_teamRating / 5.0f);

        return Random.Range(minCount, maxCount + 1);
    }

    public List<int> CalculateDangers(int _teamRating, int _enemiesNumber)
    {
        int maxDanger = _teamRating > 8 ? 5 : _teamRating > 5 ? 4 : 3;
        float[] weights = new float[maxDanger];

        //Gives spawn probability for every enemy, depending on rating
        for (int i = 0; i < maxDanger; ++i)
        {
            weights[i] = Mathf.Pow(_teamRating / 15f * (i + 1) / 5f, (i + 1));
        }

        //Randomly choose few dangers
        List<int> dangers = new(_enemiesNumber);

        for (int i = 0; i < _enemiesNumber; ++i)
        {
            dangers.Add(ChooseWeight(weights) + 1);
        }
        return dangers;
    }

    private static int ChooseWeight(float[] _weights)
    {
        float sum = 0;
        foreach (float weight in _weights)
        {
            sum += weight;
        }

        //Random float within sum range
        float rand = Random.value * sum;

        //Search where on range it landed by subtracting every weight 
        for (int i = 0; i < _weights.Length; ++i)
        {
            if (rand < _weights[i]) return i;
            rand -= _weights[i];
        }

        //If nothing worked, choose last (spawn the strongest enemy ;)
        return _weights.Length - 1;
    }

    public bool RandomMapPoint(out Vector3 position)
    {
        Vector3 searchPos;
        for (int i = 0; i < 100; ++i)
        {
            if (!RandomMove.RandomPoint(worldCenter, spawnRadius, out searchPos)) continue;
            Debug.Log($"Tried {searchPos}");

            if (Vector3.Distance(searchPos, portalPos) > portalArea)
            {
                position = searchPos;
                return true;
            }
        }
        position = worldCenter;
        Debug.Log($"Nooo {position}");
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
        Gizmos.DrawWireSphere(worldCenter, spawnRadius);
    }

    #endregion
}

[System.Serializable]
public class EnemyPool
{
    public GameObject[] enemies;
}

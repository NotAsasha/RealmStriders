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
        [SerializeField, Min(2)] private int maximumThreatBudget = 20;
        [SerializeField] private Enemy[] finalMissionEnemies;
        private readonly System.Random encounterRandom = new();

        [SerializeField] private Vector3 worldCenter = new(0f, 0f, 0f);
        [SerializeField] private float portalArea = 30f;
        [SerializeField] private float spawnRadius = 100f;
        [SerializeField] private Vector3 portalPos = new(3f, 0f, 0f);

        public FixedList32Bytes<int> CreateMissionDangers(int teamRating, bool finalMission)
        {
            var dangers = new FixedList32Bytes<int>();
            if (finalMission)
            {
                if (!ValidateFinalRoster(dangers.Capacity)) return dangers;
                foreach (Enemy enemy in finalMissionEnemies)
                    dangers.Add(Mathf.RoundToInt(enemy.dangerLevel));
                return dangers;
            }

            int limit = Mathf.Max(2, maximumThreatBudget);
            int budget = Mathf.Min(limit, Mathf.Clamp(teamRating, 0, limit) + 2);
            foreach (int danger in EnemyEncounterBudget.Generate(budget, GetMaxDanger(teamRating),
                dangers.Capacity, encounterRandom)) dangers.Add(danger);
            return dangers;
        }

        private bool ValidateFinalRoster(int capacity)
        {
            if (finalMissionEnemies == null || finalMissionEnemies.Length == 0 || finalMissionEnemies.Length > capacity)
            {
                Debug.LogError("End requires an explicit enemy roster that fits the mission payload.", this);
                return false;
            }
            var unique = new HashSet<Enemy>();
            foreach (Enemy enemy in finalMissionEnemies)
            {
                if (enemy == null || !enemy.TryGetComponent<NetworkObject>(out _) || !unique.Add(enemy) ||
                    enemy.dangerLevel < 1 || enemy.dangerLevel > 5)
                {
                    Debug.LogError("End roster contains a missing, duplicate or invalid enemy prefab.", this);
                    return false;
                }
            }
            return true;
        }

        public bool SpawnEnemies(FixedList32Bytes<int> dangerLevels, bool finalMission = false)
        {
            if (!IsServer || dangerLevels.Length == 0) return false;
            if (finalMission && (!ValidateFinalRoster(dangerLevels.Capacity) ||
                finalMissionEnemies.Length != dangerLevels.Length)) return false;

            var prefabs = new GameObject[dangerLevels.Length];
            var positions = new Vector3[dangerLevels.Length];
            for (int i = 0; i < dangerLevels.Length; i++)
            {
                int danger = dangerLevels[i];
                if (finalMission)
                {
                    if (danger != Mathf.RoundToInt(finalMissionEnemies[i].dangerLevel)) return false;
                    prefabs[i] = finalMissionEnemies[i].gameObject;
                }
                else
                {
                    if (enemiesByDanger == null || danger < 1 || danger > enemiesByDanger.Length) return false;
                    var pool = enemiesByDanger[danger - 1].enemies;
                    if (pool == null || pool.Length == 0) return false;
                    prefabs[i] = pool[Random.Range(0, pool.Length)];
                }
                if (prefabs[i] == null || !prefabs[i].TryGetComponent<Enemy>(out _) ||
                    !prefabs[i].TryGetComponent<NetworkObject>(out _) || !RandomMapPoint(out positions[i]))
                {
                    Debug.LogError("Could not prepare every mission target; no enemies were spawned.", this);
                    return false;
                }
            }

            for (int i = 0; i < prefabs.Length; i++)
            {
                var enemyObj = Instantiate(prefabs[i], positions[i], Quaternion.identity);
                SceneManager.MoveGameObjectToScene(enemyObj, GameManager.Instance.missionScene);
                var enemy = enemyObj.GetComponent<NetworkObject>();
                enemy.Spawn(true);
                GameManager.Instance.RegisterMissionEnemy(enemy.GetComponent<Enemy>());
            }
            return true;
        }

        public static int GetMaxDanger(int teamRating)
        {
            if (teamRating <= 3) return 2;
            if (teamRating <= 6) return 3;
            if (teamRating <= 9) return 4;
            return 5;
        }

        public bool RandomMapPoint(out Vector3 position)
        {
            for (int i = 0; i < 1000; i++)
            {
                if (!RandomMove.RandomPoint(worldCenter, spawnRadius, out var searchPos)) continue;
                if (searchPos.y < -30f) continue;
                if (Vector3.Distance(searchPos, portalPos) <= portalArea) continue;
                position = searchPos;
                return true;
            }
            position = worldCenter;
            Debug.LogError("No valid enemy spawn location found.", this);
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(worldCenter, portalArea);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(worldCenter, spawnRadius);
        }
    }

    [System.Serializable]
    public class EnemyPool
    {
        public GameObject[] enemies;
    }
}

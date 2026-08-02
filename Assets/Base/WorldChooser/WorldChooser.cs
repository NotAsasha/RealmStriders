using Enemy;
using Unity.Netcode;
using UnityEngine;
using NetString = Unity.Collections.FixedString64Bytes;

namespace Base.WorldChooser
{
    public struct MissionData : INetworkSerializable, System.IEquatable<MissionData>
    {
        public NetString missionName;
        public int enemiesCount;
        public float averageDanger;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref missionName);
            serializer.SerializeValue(ref enemiesCount);
            serializer.SerializeValue(ref averageDanger);
        }

        public bool Equals(MissionData other)
        {
            return missionName == other.missionName &&
                   enemiesCount == other.enemiesCount &&
                   averageDanger == other.averageDanger;
        }
    }

    public class WorldChooser : NetworkBehaviour
    {
        public int missionNumber = 3;

        [SerializeField] private Transform parent;
        [SerializeField] private GameObject prefab;

        [SerializeField] private string[] availableWorlds;

        private NetworkList<MissionData> availableMissions;



        private void Awake()
        {
            availableMissions = new NetworkList<MissionData>();
        }

        public override void OnNetworkSpawn()
        {
            availableMissions.OnListChanged += OnMissionsListChanged;

            GameManager.Instance.hasStartedMission.OnValueChanged += ReactToMissionState;

            ReactToMissionState(false, GameManager.Instance.hasStartedMission.Value);

            if (IsServer)
            {
                if (availableMissions.Count == 0)
                {
                    GenerateMissions(missionNumber);
                }
                else
                {
                    UpdateUI();
                }
            }
            else
            {
                UpdateUI();
            }
        }

        public override void OnNetworkDespawn()
        {
            availableMissions.OnListChanged -= OnMissionsListChanged;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.hasStartedMission.OnValueChanged -= ReactToMissionState;
            }
        }

        private void OnMissionsListChanged(NetworkListEvent<MissionData> changeEvent)
        {
            UpdateUI();
        }

        private void ReactToMissionState(bool oldV, bool isStarted)
        {
            parent.gameObject.SetActive(!isStarted);
            if (!isStarted) UpdateUI();
        }

        private void UpdateUI()
        {
            ClearUI();

            foreach (var mission in availableMissions)
            {
                CreateCardLocal(mission.missionName, mission.enemiesCount, mission.averageDanger);
            }
        }

        private void GenerateMissions(int capacity)
        {
            availableMissions.Clear();

            for (int i = 0; i < capacity; i++)
            {
                var missionName = (NetString)availableWorlds[Random.Range(0, availableWorlds.Length)];
                var enemiesCount = EnemySpawner.RandomEnemiesNumber(GameManager.Instance.teamRating.Value + i - 1);
                var averageDanger = Random.Range(1, 5);

                availableMissions.Add(new MissionData
                {
                    missionName = missionName,
                    enemiesCount = enemiesCount,
                    averageDanger = averageDanger
                });
            }
        }

        private void CreateCardLocal(NetString missionName, int enemiesCount, float averageDanger)
        {
            var temp = Instantiate(prefab, parent);
            WorldCard card = temp.GetComponent<WorldCard>();
            card.Setup(this, missionName, enemiesCount, averageDanger);
        }

        private void ClearUI()
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetMissionServerRpc(NetString missionName, int enemiesCount, float averageDanger)
        {
            if (GameManager.Instance.hasStartedMission.Value) return;

            // Тепер цей лог з'явиться ТІЛЬКИ на Сервері (Хості)
            Debug.Log($"[SERVER] Клієнт вибрав місію: {missionName}");

            GameManager.Instance.missionName = missionName.ToString();
            GameManager.Instance.enemiesCount = enemiesCount;
            GameManager.Instance.averageDanger = averageDanger;

            // Якщо потрібно сповістити інші клієнти про те, яка місія зараз обрана, 
            // GameManager має синхронізувати ці змінні (наприклад, через NetworkVariable або ClientRpc)
        }
    }
}
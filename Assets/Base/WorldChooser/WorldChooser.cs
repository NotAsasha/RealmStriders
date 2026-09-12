using Enemy;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Base.WorldChooser
{
    public struct MissionData : INetworkSerializable, System.IEquatable<MissionData>
    {
        public FixedString64Bytes missionName;
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
            return missionName.Equals(other.missionName) &&
                   enemiesCount == other.enemiesCount &&
                   Mathf.Approximately(averageDanger, other.averageDanger);
        }
    }

    public class WorldChooser : NetworkBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int missionNumber = 3;
        [SerializeField] private string[] availableWorlds;

        [Header("UI References")]
        [SerializeField] private Transform cardParent;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private TMP_Text currentMissionText;
        [SerializeField] private AudioSource audioSource;

        private NetworkList<MissionData> availableMissions;

        private readonly NetworkVariable<FixedString64Bytes> selectedMissionName = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private void Awake()
        {
            availableMissions = new NetworkList<MissionData>();
            if (audioSource == null) TryGetComponent(out audioSource);
        }

        public override void OnNetworkSpawn()
        {
            availableMissions.OnListChanged += OnMissionsListChanged;
            selectedMissionName.OnValueChanged += OnSelectedMissionChanged;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.hasStartedMission.OnValueChanged += ReactToMissionState;
                ReactToMissionState(false, GameManager.Instance.hasStartedMission.Value);
            }

            if (IsServer)
            {
                if (IsServer)
                {
                    // a bit scary.. TODO
                    GameManager.Instance.teamRating.OnValueChanged += (int _, int __) => GenerateMissions(missionNumber);
                }
            }

            UpdateSelectedMissionUI(selectedMissionName.Value);
            RebuildCardsUI();
        }

        public override void OnNetworkDespawn()
        {
            availableMissions.OnListChanged -= OnMissionsListChanged;
            selectedMissionName.OnValueChanged -= OnSelectedMissionChanged;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.hasStartedMission.OnValueChanged -= ReactToMissionState;
            }
        }

        private void OnMissionsListChanged(NetworkListEvent<MissionData> changeEvent)
        {
            RebuildCardsUI();
        }

        private void OnSelectedMissionChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
        {
            UpdateSelectedMissionUI(newValue);
            if (audioSource != null && !newValue.IsEmpty)
            {
                audioSource.Play();
            }
        }

        private void ReactToMissionState(bool oldState, bool isStarted)
        {
            cardParent.gameObject.SetActive(!isStarted);

            if (IsServer && oldState && !isStarted)
            {
                selectedMissionName.Value = default;
                GenerateMissions(missionNumber);
            }

            if (!isStarted)
            {
                RebuildCardsUI();
            }
        }

        private void UpdateSelectedMissionUI(FixedString64Bytes missionName)
        {
            if (missionName.IsEmpty)
            {
                currentMissionText.text = "NONE";
                currentMissionText.color = Color.red;
            }
            else
            {
                currentMissionText.text = missionName.ToString();
                currentMissionText.color = Color.green;
            }
        }

        private void RebuildCardsUI()
        {
            ClearUI();

            for (int i = 0; i < availableMissions.Count; i++)
            {
                var mission = availableMissions[i];
                var cardObj = Instantiate(cardPrefab, cardParent);
                if (cardObj.TryGetComponent<WorldCard>(out var card))
                {
                    card.Setup(this, mission.missionName, mission.enemiesCount, mission.averageDanger);
                }
            }
        }

        private void ClearUI()
        {
            for (int i = cardParent.childCount - 1; i >= 0; i--)
            {
                Destroy(cardParent.GetChild(i).gameObject);
            }
        }

        private void GenerateMissions(int capacity)
        {
            if (availableWorlds == null || availableWorlds.Length == 0)
            {
                Debug.LogError("[WorldChooser] availableWorlds is empty!");
                return;
            }

            availableMissions.Clear();

            int currentRating = GameManager.Instance != null ? GameManager.Instance.teamRating.Value : 1;

            for (int i = 0; i < capacity; i++)
            {
                var missionName = new FixedString64Bytes(availableWorlds[Random.Range(0, availableWorlds.Length)]);

                int difficultyStep = Mathf.Max(0, currentRating + i - 1);
                int enemiesCount = EnemySpawner.RandomEnemiesNumber(difficultyStep);
                float averageDanger = Random.Range(1f, 5f);

                availableMissions.Add(new MissionData
                {
                    missionName = missionName,
                    enemiesCount = enemiesCount,
                    averageDanger = averageDanger
                });
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetMissionServerRpc(FixedString64Bytes missionName, int enemiesCount, float averageDanger)
        {
            if (GameManager.Instance == null || GameManager.Instance.hasStartedMission.Value) return;

            GameManager.Instance.missionName = missionName.ToString();
            GameManager.Instance.enemiesCount = enemiesCount;
            GameManager.Instance.averageDanger = averageDanger;

            selectedMissionName.Value = missionName;
        }
    }
}
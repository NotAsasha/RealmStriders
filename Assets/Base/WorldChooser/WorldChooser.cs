using Enemy;
using Unity.Netcode;
using UnityEngine;
using NetString = Unity.Collections.FixedString64Bytes;
namespace Base.WorldChooser
{
    public class WorldChooser : NetworkBehaviour
    {
        public int missionNumber = 3;

        [SerializeField] private Transform parent;
        [SerializeField] private GameObject prefab;

        private void Start()
        {
            NetworkManager.OnClientStarted += UpdateUI;
            GameManager.Instance.hasStartedMission.OnValueChanged += ReactToMissionState;
            ReactToMissionState(false, GameManager.Instance.hasStartedMission.Value);
        }

        private void OnDisable()
        {

            GameManager.Instance.hasStartedMission.OnValueChanged -= ReactToMissionState;
        }

        private void ReactToMissionState(bool oldV, bool isStarted)
        {
            parent.gameObject.SetActive(!isStarted);

            if (!isStarted) UpdateUI();
        }

        private void UpdateUI()
        {
            ClearUI();
            if (IsServer) GenerateMissions(missionNumber);
        }

        private void GenerateMissions(int capacity)
        {
            for (int i = 0; i < missionNumber; i++)
            {
                //TODO: Make better calculation for missions.
                //maybe based on current team rating(+-one star)
                //or create three missions with different difficulties(easy, normal, hard)
                var missionName = (NetString)$"World{i + 1}";
                var enemiesCount = EnemySpawner.RandomEnemiesNumber(GameManager.Instance.teamRating.Value + i - 1);
                var averageDanger = Random.Range(1, 5);

                CreateCardClientRpc(missionName, enemiesCount, averageDanger);
            }
        }

        [ClientRpc]
        private void CreateCardClientRpc(NetString missionName, int enemiesCount, float averageDanger)
        {
            var temp = Instantiate(prefab, parent);
            WorldCard card = temp.GetComponent<WorldCard>();
            card.missionName = missionName;
            card.enemiesCount = enemiesCount;
            card.averageDanger = averageDanger;
        }

        private void ClearUI()
        {
            Debug.Log("ClearUI");
            for (int i = 0; i < parent.childCount; i++)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }
    }
}
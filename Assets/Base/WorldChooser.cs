using NUnit.Framework;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

using NetString = Unity.Collections.FixedString64Bytes;
public class WorldChooser : NetworkBehaviour
{
    public int missionNumber = 3;

    [SerializeField] private Transform parent;
    [SerializeField] private GameObject prefab;

    private void Start()
    {
        NetworkManager.OnClientStarted += UpdateUI;
        GameManager.instance.hasStartedMission.OnValueChanged += ReactToMissionState;
    }

    private void ReactToMissionState(bool oldV, bool isStarted)
    {
        parent.gameObject.SetActive(!isStarted);

        if (!isStarted) UpdateUI();
    }

    private void UpdateUI()
    {
        Debug.Log("UpdateUI()");
        if (parent.childCount != missionNumber) RecreateUI();
        if (IsServer) GenerateMissions(missionNumber);
    }

    private void GenerateMissions(int _capacity)
    {
        for (int i = 0; i < missionNumber; i++)
        {
            //TODO: Make better calculation for missions.
            //maybe based on current team rating(+-one star)
            //or create three missions with different difficulties(easy, normal, hard)
            var child = parent.GetChild(i).GetComponent<WorldCard>();
            child.parent = this;
            child.missionName.Value = (NetString)$"World{i + 1}";
            child.enemiesCount.Value = EnemySpawner.RandomEnemiesNumber(GameManager.instance.teamRating.Value + i - 1);
            child.avarageDanger.Value = Random.Range(1, 5);
        }
    }

    private void RecreateUI()
    {
        Debug.Log("RecreateUI()");

        for (int i = 0; i < parent.childCount; i++)
        {
            Destroy(parent.GetChild(i).gameObject);
        }

        for (int i = 0; i < missionNumber; i++)
        {
            Debug.Log("Instantiate "+ i);


            var temp = Instantiate(prefab, parent);
            temp.GetComponent<WorldCard>().parent = this;
            temp.GetComponent<NetworkObject>().Spawn();
        }
    }
}
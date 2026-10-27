using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Collections;
using Base.WorldChooser;

public static class WorldChooserChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static async Task<string> Run()
    {
        Require(Application.isPlaying && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path=="Assets/Temporary/PauseFadeChecks/Check.unity","Use isolated check scene");
        var network=new GameObject("Chooser test host");var transport=network.AddComponent<UnityTransport>();transport.SetConnectionData("127.0.0.1",17846);
        var manager=network.AddComponent<NetworkManager>();manager.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false,ForceSamePrefabs=false};manager.SetSingleton();Require(manager.StartHost(),"Host failed");
        var gameRoot=new GameObject("Chooser test game",typeof(NetworkObject));var game=gameRoot.AddComponent<GameManager>();gameRoot.GetComponent<NetworkObject>().Spawn();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Base/WorldChooser/WorldChooser.prefab");manager.AddNetworkPrefab(prefab);
        var root=UnityEngine.Object.Instantiate(prefab);var chooser=root.GetComponent<WorldChooser>();root.GetComponent<NetworkObject>().Spawn();
        var missions=(NetworkList<MissionData>)typeof(WorldChooser).GetField("availableMissions",Private).GetValue(chooser);
        var parent=(Transform)typeof(WorldChooser).GetField("cardParent",Private).GetValue(chooser);
        var index=(NetworkVariable<int>)typeof(WorldChooser).GetField("selectedMissionIndex",Private).GetValue(chooser);
        missions.Clear();
        var danger=new FixedList32Bytes<int>();danger.Add(1);
        missions.Add(new MissionData{missionName=new FixedString64Bytes("Duplicate"),dangerLevels=danger});danger.Add(3);
        missions.Add(new MissionData{missionName=new FixedString64Bytes("Duplicate"),dangerLevels=danger});
        await Task.Delay(150);
        var cards=parent.GetComponentsInChildren<WorldCard>();Require(cards.Length==2,"Rebuild failed");
        Require(cards.All(c=>!c.transform.Find("SelectedIndicator").gameObject.activeSelf),"Initial selection not empty");
        cards[0].SetMission();Require(index.Value==0 && cards[0].transform.Find("SelectedIndicator").gameObject.activeSelf && !cards[1].transform.Find("SelectedIndicator").gameObject.activeSelf,"First selection failed");
        cards[1].SetMission();Require(index.Value==1 && !cards[0].transform.Find("SelectedIndicator").gameObject.activeSelf && cards[1].transform.Find("SelectedIndicator").gameObject.activeSelf,"Duplicate name highlighted wrong card");
        Require(game.dangerLevels.Length==2,"Wrong selected mission payload");
        typeof(WorldChooser).GetMethod("RebuildCardsUI",Private).Invoke(chooser,null);await Task.Delay(100);
        cards=parent.GetComponentsInChildren<WorldCard>();Require(cards[1].transform.Find("SelectedIndicator").gameObject.activeSelf,"Selection lost on rebuild");
        foreach(var card in cards){var button=card.GetComponentInChildren<Button>();Require(button.colors.normalColor.maxColorComponent<=1 && button.colors.normalColor.r<.3f,"Button not dark");}
        game.teamRating.Value+=1;await Task.Delay(100);
        Require(index.Value==-1 && parent.GetComponentsInChildren<WorldCard>().All(c=>!c.transform.Find("SelectedIndicator").gameObject.activeSelf),"Generated missions retained stale selection");
        root.GetComponent<NetworkObject>().Despawn(true);await Task.Delay(100);game.teamRating.Value+=1;
        return "PASS: exact card selection for duplicate names, server mission payload, rebuild persistence, regeneration reset, dark buttons, and rating subscription cleanup.";
    }
}

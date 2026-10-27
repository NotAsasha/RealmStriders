using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public static class FixShopCategories
{
    public static string Apply()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode first");
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/Lobby/Lobby.unity");
        if(!scene.isLoaded || scene.isDirty)throw new Exception("Lobby must be open with saved edits");
        int count=0;
        foreach(var chooser in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CategorySwitch>(true)))
        {
            for(int i=0;i<chooser.buttonComponents.Length;i++)
            {
                var button=chooser.buttonComponents[i];var data=new SerializedObject(button);
                PrefabUtility.RevertPropertyOverride(data.FindProperty("m_OnClick"),InteractionMode.AutomatedAction);
                data.Update();var calls=data.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                if(calls.arraySize!=2 || button.onClick.GetPersistentMethodName(0)!="PlayClick" || button.onClick.GetPersistentTarget(1)!=chooser ||
                    button.onClick.GetPersistentMethodName(1)!="SwitchToItem" || calls.GetArrayElementAtIndex(1).FindPropertyRelative("m_Arguments.m_IntArgument").intValue!=i)
                    throw new Exception("Invalid category callback: "+i);
                count++;
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "Restored inherited category callbacks for "+count+" Lobby buttons; removed stale local audio overrides.";
    }
    public static string CaptureFixture()
    {
        var chooser=UnityEngine.Object.FindFirstObjectByType<CategorySwitch>(FindObjectsInactive.Include);
        var shop=chooser.GetComponentInParent<Base.Shop.Shop>();var copy=UnityEngine.Object.Instantiate(shop.gameObject);
        try{PrefabUtility.SaveAsPrefabAsset(copy,"Assets/Temporary/PauseFadeChecks/SceneShop.prefab");}
        finally{UnityEngine.Object.DestroyImmediate(copy);}
        return "Captured actual Lobby Shop instance for isolated regression check.";
    }
    public static string Check()
    {
        if(!Application.isPlaying || SceneManager.GetActiveScene().path!="Assets/Temporary/PauseFadeChecks/Check.unity")throw new Exception("Use isolated fixture");
        var host=new GameObject("Category check host");var transport=host.AddComponent<UnityTransport>();transport.SetConnectionData("127.0.0.1",17847);
        var network=host.AddComponent<NetworkManager>();network.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false,ForceSamePrefabs=false};network.SetSingleton();
        if(!network.StartHost())throw new Exception("Host failed");
        var gameRoot=new GameObject("Category check game",typeof(NetworkObject));var game=gameRoot.AddComponent<GameManager>();gameRoot.GetComponent<NetworkObject>().Spawn();
        var shop=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Temporary/PauseFadeChecks/SceneShop.prefab"));
        var chooser=shop.GetComponentInChildren<CategorySwitch>(true);
        var events=new GameObject("Category check events",typeof(EventSystem)).GetComponent<EventSystem>();var click=new PointerEventData(events){button=PointerEventData.InputButton.Left};
        int[] thresholds={0,4,7,10,13};
        for(int i=0;i<5;i++)
        {
            if(i>0)
            {
                game.teamRating.Value=thresholds[i]-1;
                if(chooser.buttonComponents[i].interactable)throw new Exception("Category unlocked early: "+i);
            }
            game.teamRating.Value=thresholds[i];
            ExecuteEvents.Execute(chooser.buttonComponents[i].gameObject,click,ExecuteEvents.pointerClickHandler);
            for(int j=0;j<5;j++)if(chooser.categories[j].activeSelf!=(i==j))throw new Exception("Category click did not switch to "+i);
        }
        for(int i=4;i>=0;i--)
        {
            ExecuteEvents.Execute(chooser.buttonComponents[i].gameObject,click,ExecuteEvents.pointerClickHandler);
            if(!chooser.categories[i].activeSelf)throw new Exception("Switch back failed: "+i);
        }
        return "PASS: actual Lobby instance switches all five categories through button clicks, in both directions, at rating thresholds 0/4/7/10/13.";
    }
}

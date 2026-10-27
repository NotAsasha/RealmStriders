using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Base.Shop;
using TMPro;

public static class ShopQuickBuyChecks
{
    private const string Folder="Assets/Temporary/ShopQuickBuyChecks";
    private const string ScenePath=Folder+"/Check.unity";
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    public static string Prepare()
    {
        Require(!Application.isPlaying && !AssetDatabase.IsValidFolder(Folder),"Fixture already active");
        SessionState.SetString("ShopQuickBuy.PreviousStart",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        AssetDatabase.CreateFolder("Assets/Temporary","ShopQuickBuyChecks");
        var previous=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        var token=new GameObject("PurchaseToken",typeof(NetworkObject));
        PrefabUtility.SaveAsPrefabAsset(token,Folder+"/PurchaseToken.prefab");UnityEngine.Object.DestroyImmediate(token);
        EditorSceneManager.SaveScene(scene,ScenePath);EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        return "Prepared isolated Shop test scene.";
    }
    public static async Task<string> Run()
    {
        Require(Application.isPlaying && SceneManager.GetActiveScene().path==ScenePath,"Wrong scene");
        var network=new GameObject("Shop test host");var transport=network.AddComponent<UnityTransport>();transport.SetConnectionData("127.0.0.1",17845);
        var manager=network.AddComponent<NetworkManager>();manager.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false,ForceSamePrefabs=false};
        manager.SetSingleton();Require(manager.StartHost(),"Host failed");
        var gameRoot=new GameObject("Shop test game",typeof(NetworkObject));var game=gameRoot.AddComponent<GameManager>();gameRoot.GetComponent<NetworkObject>().Spawn();
        game.teamMoney.Value=5000;game.teamRating.Value=13;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Base/Shop/Shop.prefab");manager.AddNetworkPrefab(prefab);
        var token=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/PurchaseToken.prefab");manager.AddNetworkPrefab(token);
        var root=UnityEngine.Object.Instantiate(prefab);root.name="Shop quick-buy check";
        var items=root.GetComponentsInChildren<ItemCard>(true);foreach(var item in items)item.itemPrefab=token.GetComponent<NetworkObject>();
        root.GetComponent<NetworkObject>().Spawn();root.GetComponent<Shop>().StopAllCoroutines();
        var events=new GameObject("Test events",typeof(EventSystem)).GetComponent<EventSystem>();
        var pointer=new PointerEventData(events){button=PointerEventData.InputButton.Left};
        var catalog=root.transform.Find("Canvas/Mask/ShopArea");
        var tiles=catalog.GetComponentsInChildren<Button>(true).Where(b=>b.transform.parent.name=="Items").ToArray();
        Require(tiles.Length==11,"Missing tiles");
        int expected=5000;
        foreach(var tile in tiles)
        {
            var item=Enumerable.Range(0,tile.onClick.GetPersistentEventCount()).Select(i=>tile.onClick.GetPersistentTarget(i)).OfType<GameObject>().Select(g=>g.GetComponent<ItemCard>()).First(c=>c!=null);
            var quick=tile.transform.Find("QuickBuy").GetComponent<Button>();
            Require(quick.interactable,"Affordable quick buy disabled");
            Require(tile.transform.Find("Price").GetComponent<TMP_Text>().text==item.Price.ToString(),"Catalog price suffix");
            Require(item.transform.Find("Price").GetComponent<TMP_Text>().text==item.Price.ToString(),"Detail price suffix");
            Require(((RectTransform)item.transform.Find("Back")).anchoredPosition.x<((RectTransform)item.transform.Find("Buy")).anchoredPosition.x,"Back/Buy order");
            tile.transform.parent.parent.gameObject.SetActive(true);
            var handled=ExecuteEvents.ExecuteHierarchy(quick.GetComponentInChildren<TMP_Text>().gameObject,pointer,ExecuteEvents.pointerClickHandler);
            await Task.Delay(30);expected-=item.Price;
            Require(handled==quick.gameObject && !item.gameObject.activeSelf,"Quick buy opened details");
            Require(game.teamMoney.Value==expected,"Quick buy charged incorrectly: "+item.name);
            tile.transform.parent.parent.gameObject.SetActive(false);
        }
        Require(manager.SpawnManager.SpawnedObjectsList.Count(o=>o.name.StartsWith("PurchaseToken"))==11,"Incorrect purchase spawn count");
        var first=tiles[0];first.transform.parent.parent.gameObject.SetActive(true);
        var firstQuick=first.transform.Find("QuickBuy").GetComponent<Button>();
        game.teamMoney.Value=0;Require(tiles.All(t=>!t.transform.Find("QuickBuy").GetComponent<Button>().interactable),"Insufficient money buttons active");
        ExecuteEvents.ExecuteHierarchy(firstQuick.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        Require(items.All(c=>!c.gameObject.activeSelf),"Disabled button opened details");
        game.teamMoney.Value=5000;game.teamRating.Value=3;
        var tierTwo=items.First(c=>c.RequiredRating==4);
        var tierTwoQuick=tiles.Select(t=>t.transform.Find("QuickBuy").GetComponent<Button>()).First(b=>b.onClick.GetPersistentTarget(0)==tierTwo);
        Require(!tierTwoQuick.interactable,"Tier two unlocked at rating 3");game.teamRating.Value=4;Require(tierTwoQuick.interactable,"Tier two locked at rating 4");
        ExecuteEvents.ExecuteHierarchy(first.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        var detail=items.Single(c=>c.gameObject.activeSelf);Require(!catalog.gameObject.activeSelf,"Tile did not open details");
        detail.transform.Find("Buy").GetComponent<Button>().onClick.Invoke();Require(game.teamMoney.Value==5000-detail.Price,"Detail buy failed");
        detail.transform.Find("Back").GetComponent<Button>().onClick.Invoke();Require(catalog.gameObject.activeSelf && !detail.gameObject.activeSelf,"Back failed");
        var camera=new GameObject("Shop check camera",typeof(Camera)).GetComponent<Camera>();camera.tag="MainCamera";
        var point=root.transform.Find("CameraPoint");camera.transform.SetPositionAndRotation(point.position,point.rotation);
        camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.fieldOfView=60;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.025f,.035f);
        root.transform.Find("Canvas").GetComponent<Canvas>().worldCamera=camera;
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();await Task.Delay(300);Canvas.ForceUpdateCanvases();
        foreach(var label in root.GetComponentsInChildren<TMP_Text>()){label.ForceMeshUpdate();Require(!label.isTextOverflowing,"Overflow: "+label.name+" "+label.text);}
        return "PASS: 11 quick purchases and spawns, click isolation, disabled click, money/rating refresh, detail navigation/purchase, prices and button order.";
    }
    public static string Restore()
    {
        Require(!Application.isPlaying,"Stop Play Mode first");
        var previous=SessionState.GetString("ShopQuickBuy.PreviousStart","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
        AssetDatabase.DeleteAsset(Folder);SessionState.EraseString("ShopQuickBuy.PreviousStart");return "Restored Editor and removed fixture.";
    }
}

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

public static class AnalogShopChecks
{
    private const string Folder="Assets/Temporary/AnalogShopChecks";
    private const string ScenePath=Folder+"/Check.unity";
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    public static string Prepare()
    {
        Require(!Application.isPlaying && !AssetDatabase.IsValidFolder(Folder),"Fixture already active");
        SessionState.SetString("AnalogShop.PreviousStart",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        AssetDatabase.CreateFolder("Assets/Temporary","AnalogShopChecks");
        var source=GameObject.Find("/Base/Furniture/Shop");
        var copy=UnityEngine.Object.Instantiate(source);PrefabUtility.SaveAsPrefabAsset(copy,Folder+"/SceneShop.prefab");UnityEngine.Object.DestroyImmediate(copy);
        var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        var token=new GameObject("PurchaseToken",typeof(NetworkObject));PrefabUtility.SaveAsPrefabAsset(token,Folder+"/PurchaseToken.prefab");UnityEngine.Object.DestroyImmediate(token);
        EditorSceneManager.SaveScene(scene,ScenePath);EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        return "Prepared isolated runtime fixture from actual Lobby Shop; original scene retained.";
    }
    public static async Task<string> Run()
    {
        Require(Application.isPlaying && SceneManager.GetActiveScene().path==ScenePath,"Wrong scene");
        Application.runInBackground=true;
        var host=new GameObject("Shop check host");var transport=host.AddComponent<UnityTransport>();transport.SetConnectionData("127.0.0.1",17848);
        var manager=host.AddComponent<NetworkManager>();manager.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false,ForceSamePrefabs=false};
        manager.SetSingleton();Require(manager.StartHost(),"Host failed");
        var gameRoot=new GameObject("Shop check game",typeof(NetworkObject));var game=gameRoot.AddComponent<GameManager>();gameRoot.GetComponent<NetworkObject>().Spawn();
        game.teamMoney.Value=5000;game.teamRating.Value=0;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/SceneShop.prefab");manager.AddNetworkPrefab(prefab);
        var token=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/PurchaseToken.prefab");manager.AddNetworkPrefab(token);
        var root=UnityEngine.Object.Instantiate(prefab);root.name="Analog Shop check";root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.transform.localScale=Vector3.one;
        var items=root.GetComponentsInChildren<ItemCard>(true);foreach(var item in items)item.itemPrefab=token.GetComponent<NetworkObject>();
        root.GetComponent<NetworkObject>().Spawn();root.GetComponent<Shop>().StopAllCoroutines();
        var chooser=root.GetComponentInChildren<CategorySwitch>();var panel=root.transform.Find("shop/MainPanel");var home=panel.localPosition;
        var canvas=root.transform.Find("Canvas").GetComponent<Canvas>();var group=canvas.GetComponent<CanvasGroup>();
        var events=new GameObject("Test events",typeof(EventSystem)).GetComponent<EventSystem>();var pointer=new PointerEventData(events){button=PointerEventData.InputButton.Left};
        var camera=new GameObject("Shop check camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.tag="MainCamera";
        var point=root.transform.Find("CameraPoint");camera.transform.SetPositionAndRotation(point.position,point.rotation);
        camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.fieldOfView=60;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.1f,.12f,.15f);
        canvas.worldCamera=camera;
        var light=new GameObject("Preview light",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(35,-65,0);
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();
        await Task.Delay(150);
        int[] ratings={0,4,7,10,13};
        for(int i=0;i<5;i++)
        {
            if(i>0){game.teamRating.Value=ratings[i]-1;Require(!chooser.buttonComponents[i].interactable,"Unlocked early "+i);}
            game.teamRating.Value=ratings[i];var b=chooser.buttonComponents[i];
            Require(b.interactable,"Locked at threshold "+i);
            var mesh=root.transform.Find("shop/Cylinder."+(i+1).ToString("000"));var rest=mesh.localPosition;
            ExecuteEvents.Execute(b.gameObject,pointer,ExecuteEvents.pointerDownHandler);await Task.Delay(90);
            Require(mesh.localPosition.y<rest.y-.005f,"No physical press "+i);
            ExecuteEvents.Execute(b.gameObject,pointer,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(b.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            if(i>0){Require(!group.interactable,"Input active during travel");await Task.Delay(80);Require(panel.localPosition.y<home.y-.1f,"Panel did not lower");}
            await Task.Delay(650);
            Require((panel.localPosition-home).sqrMagnitude<.000001f && group.interactable,"Panel did not settle");
            Require(chooser.categories[i].activeSelf,"Category missing "+i);
            Require(b.transform.Find("Requirement").GetComponent<TMP_Text>().text=="OPEN","Selected status missing");
        }
        var catalog=root.transform.Find("Canvas/Mask/ShopArea");
        var tiles=catalog.GetComponentsInChildren<Button>(true).Where(b=>b.transform.parent.name=="Items").ToArray();
        Require(tiles.Length==11,"Missing catalog items");int expected=5000;
        foreach(var tile in tiles)
        {
            var item=Enumerable.Range(0,tile.onClick.GetPersistentEventCount()).Select(i=>tile.onClick.GetPersistentTarget(i)).OfType<ItemCard>().Single();
            chooser.SwitchToItem(Array.IndexOf(chooser.categories,tile.transform.parent.parent.gameObject));await Task.Delay(550);
            var quick=tile.transform.Find("QuickBuy").GetComponent<Button>();Require(quick.interactable,"Affordable item unavailable");
            ExecuteEvents.Execute(quick.gameObject,pointer,ExecuteEvents.pointerClickHandler);await Task.Delay(30);expected-=item.Price;
            Require(game.teamMoney.Value==expected && items.All(c=>!c.gameObject.activeSelf),"Quick purchase failed "+item.name);
        }
        var first=items[0];first.Open();await Task.Delay(600);Require(first.gameObject.activeSelf && !catalog.gameObject.activeSelf,"Detail navigation failed");
        first.Close();await Task.Delay(600);Require(!first.gameObject.activeSelf && catalog.gameObject.activeSelf,"Back failed");
        chooser.SwitchToItem(4);await Task.Delay(40);chooser.SwitchToItem(2);await Task.Delay(40);chooser.SwitchToItem(1);await Task.Delay(600);
        Require(chooser.categories[1].activeSelf && group.interactable && panel.localPosition==home,"Rapid switching failed");
        game.teamRating.Value=0;await Task.Delay(600);Require(chooser.categories[0].activeSelf,"Rating downgrade did not return to available category");
        Require(root.transform.Find("HardwareControls/BalanceDisplay/MoneyCounter").GetComponent<TMP_Text>().text==expected.ToString(),"Balance mismatch");
        game.teamMoney.Value=0;Require(tiles.All(t=>!t.transform.Find("QuickBuy").GetComponent<Button>().interactable),"No-money purchase enabled");
        game.teamMoney.Value=320;game.teamRating.Value=4;chooser.SwitchToItem(0);await Task.Delay(600);Canvas.ForceUpdateCanvases();
        first.Open();await Task.Delay(40);chooser.SwitchToItem(0);await Task.Delay(600);
        Require(catalog.gameObject.activeSelf && items.All(c=>!c.gameObject.activeSelf),"Pending detail was not cancelled by category key");
        for(int i=0;i<5;i++)
        {
            pointer.position=camera.WorldToScreenPoint(root.transform.Find("shop/Cylinder."+(i+1).ToString("000")).GetComponent<Renderer>().bounds.center);
            var hits=new System.Collections.Generic.List<RaycastResult>();events.RaycastAll(pointer,hits);
            Require(hits.Count>0 && hits[0].gameObject==chooser.buttonComponents[i].gameObject,"Hardware trigger is misaligned "+i);
        }
        return "PASS: actual Lobby instance, five unlock thresholds, physical presses, MainPanel travel, rapid switches, 11 purchases, detail/back, rating downgrade and balance updates.";
    }
    public static string Restore()
    {
        Require(!Application.isPlaying,"Stop Play Mode first");var previous=SessionState.GetString("AnalogShop.PreviousStart","");
        EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
        AssetDatabase.DeleteAsset(Folder);SessionState.EraseString("AnalogShop.PreviousStart");return "Restored Editor and removed isolated fixture.";
    }

    public static async Task<string> Detail()
    {
        var root=GameObject.Find("Analog Shop check");root.GetComponentsInChildren<ItemCard>(true).First(c=>c.itemName=="LeafBlower").Open();
        await Task.Delay(650);
        foreach(var label in root.GetComponentsInChildren<TMP_Text>())
        {label.ForceMeshUpdate();Require(!label.isTextOverflowing,"Text overflow: "+label.name);}
        return "Detail preview ready; active text fits.";
    }
}

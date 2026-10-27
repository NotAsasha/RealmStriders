using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Base.BaseUpgrader;
using TMPro;

public static class BaseUpgraderChecks
{
    private const string Folder="Assets/Temporary/BaseUpgraderChecks";
    private const string ScenePath=Folder+"/Check.unity";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static string Prepare()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode first.");
        if(AssetDatabase.IsValidFolder(Folder))throw new Exception("Previous fixture exists.");
        SessionState.SetString("BaseUpgrade.PreviousStart",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        AssetDatabase.CreateFolder("Assets/Temporary","BaseUpgraderChecks");
        var previous=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        EditorSceneManager.SaveScene(scene,ScenePath);EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        return "Prepared isolated loopback check; no saves or Steam lobby involved.";
    }
    private static void Field(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
    private static void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
    private static NetworkObject NetworkRoot(string name)=>new GameObject(name,typeof(NetworkObject)).GetComponent<NetworkObject>();
    public static async Task<string> Run()
    {
        if(!Application.isPlaying || SceneManager.GetActiveScene().path!=ScenePath)throw new Exception("Enter prepared scene.");
        Application.runInBackground=true;
        var managerRoot=new GameObject("Upgrade test host");
        var transport=managerRoot.AddComponent<UnityTransport>();transport.SetConnectionData("127.0.0.1",17844);
        var manager=managerRoot.AddComponent<NetworkManager>();
        manager.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false,ForceSamePrefabs=false};
        manager.SetSingleton();Require(manager.StartHost(),"Host failed");
        var gameRoot=NetworkRoot("Upgrade test game");var game=gameRoot.gameObject.AddComponent<GameManager>();gameRoot.Spawn();
        var baseRoot=NetworkRoot("Upgrade test base");var state=baseRoot.gameObject.AddComponent<BaseManager>();
        var radarRoot=NetworkRoot("Upgrade test radar");var radar=radarRoot.gameObject.AddComponent<Base.Radar.Radar>();
        radar.enabled=false;
        var dummyCamera=new GameObject("Dummy radar camera",typeof(Camera)).GetComponent<Camera>();dummyCamera.enabled=false;radar.radarCamera=dummyCamera;
        Field(state,"radarTerminal",radarRoot);baseRoot.Spawn();
        game.teamMoney.Value=5000;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Base/BaseUpgrader/BaseUpgrader.prefab");
        manager.AddNetworkPrefab(prefab);
        var root=UnityEngine.Object.Instantiate(prefab);root.name="Base upgrade UI check";
        root.GetComponent<NetworkObject>().Spawn();
        var upgrader=root.GetComponent<BaseUpgrader>();
        var area=root.transform.Find("Canvas/Mask/ShopArea");
        string[] names={"Drone","Detection","BeamWeapon","Casino","Shield","Charger"};
        var buttons=names.Select(n=>area.Find(n+"/Buy").GetComponent<Button>()).ToArray();
        Require(!buttons[1].interactable && !buttons[2].interactable,"Missing prerequisites enabled");
        var radarHover=area.Find("Detection").GetComponent<UpgradeOption>();
        var radarStatus=area.Find("Detection/Status").GetComponent<TMP_Text>();
        var normal=radarStatus.color;
        radarHover.OnPointerEnter(null);
        Require(radarStatus.color.r>radarStatus.color.g+.2f,"Prerequisite error not highlighted on hover");
        radarHover.OnPointerExit(null);
        Require(radarStatus.color==normal,"Hover exit did not restore status color");
        radarHover.OnPointerEnter(null);
        upgrader.BuyBeamServerRpc();upgrader.BuyDetectionServerRpc();
        Require(state.baseUpgrades.Value==0 && game.teamMoney.Value==5000,"Prerequisites bypassed on server");
        int[] prices={200,300,400,300,750,250};
        int expected=5000;
        for(int i=0;i<6;i++)
        {
            Require(buttons[i].interactable,"Available upgrade disabled: "+names[i]);
            buttons[i].onClick.Invoke();await Task.Delay(50);expected-=prices[i];
            Require(game.teamMoney.Value==expected,"Incorrect charge: "+names[i]);
            if(i==0)Require(radarStatus.text=="AVAILABLE" && radarStatus.color==normal,"Resolved prerequisite remained highlighted");
            Require((state.baseUpgrades.Value&(1<<i))!=0,"Purchase flag missing");
            Require(!buttons[i].interactable && area.Find(names[i]+"/PurchasedIndicator").GetComponent<Image>().enabled,"Purchased highlight missing");
            Require(area.Find(names[i]+"/Status").GetComponent<TMP_Text>().text=="PURCHASED","Purchased label missing");
            buttons[i].onClick.Invoke();await Task.Delay(20);
            Require(game.teamMoney.Value==expected,"Duplicate purchase charged money");
        }
        var reopened=UnityEngine.Object.Instantiate(prefab);reopened.GetComponent<NetworkObject>().Spawn();
        Require(reopened.GetComponentsInChildren<Button>().All(b=>!b.interactable),"Existing upgrades not reflected on spawn");
        reopened.GetComponent<NetworkObject>().Despawn(true);await Task.Delay(50);
        state.baseUpgrades.Value&=~(int)BaseUpgrades.IsCasinoBought;
        Require(buttons[3].interactable && !area.Find("Casino/PurchasedIndicator").GetComponent<Image>().enabled,"Casino reset still purchased");
        game.teamMoney.Value=0;Require(!buttons[3].interactable,"Unaffordable purchase enabled");
        var casinoHover=area.Find("Casino").GetComponent<UpgradeOption>();
        var casinoStatus=area.Find("Casino/Status").GetComponent<TMP_Text>();
        casinoHover.OnPointerEnter(null);
        Require(casinoStatus.text=="NOT ENOUGH MONEY" && casinoStatus.color.r>casinoStatus.color.g+.2f,"Money error hover/text failed");
        casinoHover.enabled=false;casinoHover.enabled=true;
        Require(casinoStatus.color==normal,"Disabled hover did not clear its highlight");
        upgrader.BuyCasinoServerRpc();Require((state.baseUpgrades.Value&8)==0,"Unaffordable purchase accepted");
        state.baseUpgrades.Value=1|8|32;game.teamMoney.Value=1200;
        var camera=new GameObject("Upgrade check camera",typeof(Camera)).GetComponent<Camera>();camera.tag="MainCamera";
        var point=root.transform.Find("CameraPoint");camera.transform.SetPositionAndRotation(point.position,point.rotation);
        camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.fieldOfView=60;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.025f,.035f);
        root.transform.Find("Canvas").GetComponent<Canvas>().worldCamera=camera;
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();
        await Task.Delay(300);Canvas.ForceUpdateCanvases();
        foreach(var label in root.GetComponentsInChildren<TMP_Text>())
        {label.ForceMeshUpdate();Require(!label.isTextOverflowing,"Overflow: "+label.name+" "+label.text);}
        return "PASS: prerequisite/money hover, hover exit, state changes under cursor, disable reset, six host purchases, duplicate protection and label fit.";
    }
    public static string Restore()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode first.");
        var path=SessionState.GetString("BaseUpgrade.PreviousStart","");
        EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(path)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        AssetDatabase.DeleteAsset(Folder);SessionState.EraseString("BaseUpgrade.PreviousStart");
        return "Restored original Play Mode scene and removed temporary fixture.";
    }
}

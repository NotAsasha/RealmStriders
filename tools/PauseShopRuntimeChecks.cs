using System;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Base.Shop;

public static class PauseShopRuntimeChecks
{
    public static string FocusGame()
    {
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();
        return "Game view opened.";
    }
    public static async Task<string> WorldShop()
    {
        GameObject.Find("Pause UI check")?.SetActive(false);
        var root=Resources.FindObjectsOfTypeAll<Shop>().First(s=>s.name=="Shop UI check").gameObject;
        root.SetActive(true);
        var canvas=root.transform.Find("Canvas").GetComponent<Canvas>();
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Base/Shop/Shop.prefab").transform.Find("Canvas");
        canvas.renderMode=RenderMode.WorldSpace;
        canvas.GetComponent<RectTransform>().sizeDelta=new Vector2(900,700);
        canvas.transform.localScale=source.localScale;canvas.transform.localPosition=source.localPosition;canvas.transform.localRotation=source.localRotation;
        var camera=GameObject.Find("Shop check camera").GetComponent<Camera>();
        camera.tag="MainCamera";
        foreach(var c in Camera.allCameras)if(c.targetTexture==null)c.enabled=false;
        camera.transform.SetPositionAndRotation(root.transform.Find("CameraPoint").position,root.transform.Find("CameraPoint").rotation);
        camera.nearClipPlane=.01f;camera.farClipPlane=30;camera.fieldOfView=60;camera.enabled=true;
        canvas.worldCamera=camera;
        await Task.Delay(500);return "World-space Shop view ready.";
    }
    public static async Task<string> ShowShop()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        Application.runInBackground=true;
        for(int i=0;i<100 && GameObject.Find("MenuCamera")==null;i++)await Task.Delay(100);
        var menu=GameObject.Find("MenuCamera/LobbyScreen");if(menu!=null)menu.SetActive(false);
        var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Base/Shop/Shop.prefab"));
        root.name="Shop UI check";
        var canvas=root.transform.Find("Canvas").GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=200;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1100,700);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        // Keep the terminal's authored 900x700 area centered for a readable UI preview.
        canvas.GetComponent<RectTransform>().localScale=Vector3.one;
        var camera=new GameObject("Shop check camera",typeof(Camera)).GetComponent<Camera>();
        camera.enabled=false;canvas.worldCamera=camera;
        foreach(var card in root.GetComponentsInChildren<ItemCard>(true))card.gameObject.SetActive(false);
        await Task.Delay(600);
        root.GetComponentsInChildren<ItemCard>(true).First(c=>c.itemName=="LeafBlower").gameObject.SetActive(true);
        await Task.Delay(750);
        return "Shop card open.";
    }

    public static async Task<string> Verify()
    {
        var root=GameObject.Find("Shop UI check");
        var categories=root.GetComponentInChildren<CategorySwitch>();
        var cards=root.GetComponentsInChildren<ItemCard>(true);
        var preview=root.GetComponentInChildren<ShopItemPreview>();
        var fields=BindingFlags.Instance|BindingFlags.NonPublic;
        int[] ratings={0,4,7,10,13};
        if(categories.categories.Length!=5)throw new Exception("Expected five categories");
        for(int i=0;i<5;i++)
        {
            var blocker=categories.buttonComponents[i].GetComponent<CategoryBlocker>();
            blocker.Refresh(ratings[i]-1);
            if(categories.buttonComponents[i].interactable)throw new Exception("Unlocked too early: "+i);
            blocker.Refresh(ratings[i]);
            if(!categories.buttonComponents[i].interactable)throw new Exception("Did not unlock at boundary: "+i);
        }
        foreach(var card in cards)
        {
            foreach(var c in cards)c.gameObject.SetActive(false);
            card.gameObject.SetActive(true);await Task.Delay(150);
            var model=(GameObject)typeof(ShopItemPreview).GetField("model",fields).GetValue(preview);
            if(model==null || model.GetComponentsInChildren<MeshRenderer>().Length==0)throw new Exception("Missing mesh "+card.itemName);
            if(model.GetComponentsInChildren<MonoBehaviour>().Length!=0 || model.GetComponentsInChildren<Collider>().Length!=0)
                throw new Exception("Gameplay component in preview");
            var pivot=(Transform)typeof(ShopItemPreview).GetField("pivot",fields).GetValue(preview);
            var rotation=pivot.rotation;await Task.Delay(100);
            if(Quaternion.Angle(rotation,pivot.rotation)<.1f)throw new Exception("Preview is not rotating");
            foreach(var label in card.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();if(label.isTextOverflowing)throw new Exception("Overflow "+card.itemName+" / "+label.name);
            }
        }
        foreach(var card in cards)card.gameObject.SetActive(false);
        await Task.Delay(100);
        if(((Camera)typeof(ShopItemPreview).GetField("previewCamera",fields).GetValue(preview)).enabled)
            throw new Exception("Preview camera still rendering after close");
        cards.First(c=>c.itemName=="LeafBlower").gameObject.SetActive(true);
        return "PASS: five unlock boundaries, all eleven mesh-only previews rotate, card labels fit, camera stops when cards close.";
    }

    public static async Task<string> ShowPause()
    {
        GameObject.Find("Shop UI check")?.SetActive(false);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player.prefab").GetComponentInChildren<Player.UI.PauseUIManager>(true);
        var root=UnityEngine.Object.Instantiate(source.gameObject);
        root.name="Pause UI check";root.SetActive(true);root.GetComponent<Canvas>().sortingOrder=300;
        root.GetComponent<Player.UI.PauseUIManager>().pauseMenu.SetActive(true);
        await Task.Delay(350);return "Pause open.";
    }

    public static async Task<string> CheckSettings()
    {
        var manager=GameObject.Find("Pause UI check").GetComponent<Player.UI.PauseUIManager>();
        manager.OpenSettings();await Task.Delay(400);
        foreach(var dropdown in manager.settingsMenu.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            dropdown.Show();await Task.Delay(150);
            var popup=dropdown.transform.Find("Dropdown List");if(popup==null)throw new Exception("Dropdown did not open");
            foreach(var t in popup.GetComponentsInChildren<TMP_Text>())
            {
                t.ForceMeshUpdate();if(t.isTextOverflowing || t.rectTransform.rect.width<300)throw new Exception("Invalid dropdown label");
            }
            dropdown.Hide();await Task.Delay(200);
        }
        var settings=manager.settingsMenu.GetComponent<Assets.Player.UI.SettingsMenu>();
        for(int i=0;i<3;i++){settings.SelectCategory(i);await Task.Delay(100);}
        settings.Close();
        if(manager.settingsMenu.activeSelf || !manager.pauseMenu.activeSelf)throw new Exception("Pause settings back failed");
        manager.OpenSettings();await Task.Delay(250);
        manager.settingsMenu.GetComponentsInChildren<TMP_Dropdown>(true).First().Show();
        return "PASS: Pause settings dropdowns, three categories and return to Pause.";
    }
}

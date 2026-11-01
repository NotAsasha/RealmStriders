using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class ClassicMenuPreview
{
    public static string Upgrader() => Render("Assets/Base/BaseUpgrader/BaseUpgrader.prefab", "tools/base-upgrader-classic.png", false);
    public static string Chooser() => Render("Assets/Base/WorldChooser/WorldChooser.prefab", "tools/world-chooser-classic.png", false);
    public static string ShopDetail() => Render("Assets/Base/Shop/Shop.prefab", "tools/shop-readable-detail.png", true);
    private static string Render(string path, string output, bool detail)
    {
        if (Application.isPlaying) throw new Exception("Edit Mode required");
        var root=PrefabUtility.LoadPrefabContents(path);GameObject rig=null;RenderTexture target=null;Texture2D pixels=null;
        try
        {
            root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.transform.localScale=Vector3.one;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            foreach(var c in root.GetComponentsInChildren<Canvas>(true))c.enabled=false;
            var canvas=root.transform.Find("Canvas").GetComponent<Canvas>();canvas.enabled=true;
            canvas.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);canvas.transform.localScale=Vector3.one;
            var chooser=root.GetComponent<Base.WorldChooser.WorldChooser>();
            if(chooser!=null)
            {
                var parent=root.transform.Find("Canvas/ShopArea");for(int i=parent.childCount-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
                var data=new SerializedObject(chooser);var worlds=data.FindProperty("availableWorlds");
                for(int i=0;i<3;i++)
                {
                    var card=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Base/WorldChooser/WorldCard.prefab"),parent,false).GetComponent<Base.WorldChooser.WorldCard>();
                    card.Setup(chooser,i,worlds.GetArrayElementAtIndex(i%worlds.arraySize).stringValue,2+i,1.5f+i);card.RefreshSelection(1);
                }
                canvas.transform.Find("Current Realm").GetComponent<TMP_Text>().text=worlds.GetArrayElementAtIndex(1).stringValue;
                canvas.transform.Find("Current Realm").GetComponent<TMP_Text>().color=new Color(.03f,.28f,.18f);
            }
            if(detail)
            {
                var cards=root.GetComponentsInChildren<Base.Shop.ItemCard>(true);
                foreach(var card in cards)card.gameObject.SetActive(card.itemName=="LeafBlower");
                canvas.transform.Find("Mask/ShopArea").gameObject.SetActive(false);
            }
            var up=root.GetComponent<BaseUpgrader>();
            if(up!=null)
            {
                var area=canvas.transform.Find("Mask/ShopArea");var s=new SerializedObject(up);var entries=s.FindProperty("upgradeCards");
                for(int i=0;i<entries.arraySize;i++)
                {
                    var e=entries.GetArrayElementAtIndex(i);var button=(Button)e.FindPropertyRelative("buyButton").objectReferenceValue;button.interactable=i!=0&&i!=2;
                    if(i==0)
                    {
                        ((Image)e.FindPropertyRelative("background").objectReferenceValue).color=s.FindProperty("purchasedColor").colorValue;
                        ((Image)e.FindPropertyRelative("purchasedIndicator").objectReferenceValue).enabled=true;
                        ((TMP_Text)e.FindPropertyRelative("buttonLabel").objectReferenceValue).text="OWNED";
                        ((TMP_Text)e.FindPropertyRelative("statusLabel").objectReferenceValue).text="PURCHASED";
                    }
                }
                area.Find("Detection/Status").GetComponent<TMP_Text>().text="AVAILABLE";
            }
            var rect=(RectTransform)canvas.transform;int width=(int)rect.rect.width+40,height=(int)rect.rect.height+40;
            rig=new GameObject("UI preview camera",typeof(Camera));SceneManager.MoveGameObjectToScene(rig,root.scene);
            var camera=rig.GetComponent<Camera>();camera.scene=root.scene;camera.transform.position=new Vector3(0,0,-1000);camera.orthographic=true;camera.orthographicSize=height/2f;
            camera.aspect=(float)width/height;camera.nearClipPlane=.01f;camera.farClipPlane=2000;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.10f,.12f);canvas.worldCamera=camera;
            target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;
            Canvas.ForceUpdateCanvases();var overflow=new List<string>();
            foreach(var text in root.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();if(text.isTextOverflowing)overflow.Add(text.name+": "+text.text);}
            camera.Render();camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
            pixels=new Texture2D(width,height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();RenderTexture.active=previous;File.WriteAllBytes(output,pixels.EncodeToPNG());
            return output+"; overflow: "+string.Join(" | ",overflow);
        }
        finally
        {
            if(rig!=null)UnityEngine.Object.DestroyImmediate(rig);if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Base.BaseUpgrader;

public static class StyleBaseUpgrader
{
    private static TMP_FontAsset title, button, body, detail;
    private static readonly Color Text = new(.922f,.914f,.878f);
    private static readonly Color Muted = new(.60f,.61f,.63f);
    private static readonly Color Surface = new(.12f,.12f,.14f);
    private static RectTransform Box(Transform parent,string name)
    {
        var t=parent.Find(name);if(t!=null)return (RectTransform)t;
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent,false);return rect;
    }
    private static void Place(Transform transform,float x,float y,float w,float h)
    {
        var r=(RectTransform)transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
        r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);
        r.localScale=Vector3.one;r.localRotation=Quaternion.identity;
        r.localPosition=new Vector3(r.localPosition.x,r.localPosition.y,0);
    }
    private static Image Fill(Transform t,Color color,bool raycast=false)
    {
        var image=t.GetComponent<Image>()??t.gameObject.AddComponent<Image>();
        image.sprite=null;image.material=null;image.type=Image.Type.Simple;
        image.color=color;image.raycastTarget=raycast;return image;
    }
    private static TMP_Text Label(Transform parent,string name,string value,float x,float y,float w,float h,TMP_FontAsset font,float size,Color color)
    {
        var rect=Box(parent,name);Place(rect,x,y,w,h);
        var t=rect.GetComponent<TMP_Text>()??rect.gameObject.AddComponent<TextMeshProUGUI>();
        t.text=value;t.font=font;t.fontSize=size;t.fontSizeMax=size;t.fontSizeMin=size*.8f;
        t.enableAutoSizing=true;t.fontStyle=FontStyles.Normal;t.alignment=TextAlignmentOptions.MidlineLeft;
        t.margin=Vector4.zero;t.color=color;t.raycastTarget=false;return t;
    }
    private static void Backup(string path)
    {
        Directory.CreateDirectory("tools/base-upgrader-backup");
        var copy="tools/base-upgrader-backup/"+path.Replace('/','_')+".backup";
        if(!File.Exists(copy))File.Copy(path,copy);
    }
    public static string Apply()
    {
        if(Application.isPlaying)throw new Exception("Exit Play Mode first.");
        title=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/CinzelDecorative-Bold SDF.asset");
        button=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Syncopate-Bold SDF.asset");
        body=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/SmoochSans-ExtraBold SDF.asset");
        detail=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/UnicaOne-Regular SDF.asset");
        const string path="Assets/Base/BaseUpgrader/BaseUpgrader.prefab";Backup(path);
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var canvas=root.transform.Find("Canvas");
            var backdrop=Box(canvas,"Backdrop");Place(backdrop,0,0,1000,700);
            Fill(backdrop,new Color(.043f,.043f,.055f,1),true);backdrop.SetAsFirstSibling();
            Label(canvas,"Title","Base Upgrades",38,24,700,72,title,48,Text);
            Label(canvas,"Subtitle","Improve the base. Prepare for what comes next.",38,91,700,40,body,28,Muted);
            Label(canvas,"BalanceLabel","TEAM CREDITS",800,26,164,28,detail,21,Muted);
            var money=canvas.Find("MoneyCounter").GetComponent<TMP_Text>();
            Place(money.transform,800,54,164,48);money.font=detail;money.fontSize=38;money.color=Text;
            money.alignment=TextAlignmentOptions.MidlineLeft;
            Label(canvas,"Hint","ESC  /  LEAVE TERMINAL",38,666,920,25,detail,20,Muted);
            var mask=canvas.Find("Mask");Place(mask,38,152,924,500);
            var scroll=mask.GetComponent<ScrollRect>();scroll.enabled=false;
            mask.Find("Scrollbar").gameObject.SetActive(false);
            var area=mask.Find("ShopArea");Place(area,0,0,924,500);
            Fill(area,Color.clear);area.GetComponent<GridLayoutGroup>().enabled=false;
            string[] names={"Drone","Detection","BeamWeapon","Casino","Shield","Charger"};
            string[] descriptions={"View the entire map from the base.","Pinpoint entities on the drone map.","Add a weapon to the radar.","Try your luck at the casino.","Prevent entities from passing through the portal.","Recharge compatible equipment."};
            string[] priceFields={"terminalPrice","detectionPrice","beamPrice","casinoPrice","shieldPrice","chargerPrice"};
            var upgrader=root.GetComponent<BaseUpgrader>();var serialized=new SerializedObject(upgrader);
            var entries=serialized.FindProperty("upgradeCards");entries.arraySize=6;
            for(int i=0;i<6;i++)
            {
                var card=area.Find(names[i]);Place(card,i%2*474,i/2*172,450,156);
                card.GetComponent<VerticalLayoutGroup>().enabled=false;
                var background=Fill(card,Surface,true);
                var titleText=card.Find("Name").GetComponent<TMP_Text>();
                Label(card,"Name",titleText.text,18,10,282,36,button,24,Text);
                int price=serialized.FindProperty(priceFields[i]).intValue;
                var priceText=Label(card,"Price",price+" CR",310,10,122,36,detail,26,Muted);
                priceText.alignment=TextAlignmentOptions.MidlineRight;
                Label(card,"Description",descriptions[i],18,52,414,46,body,26,Muted);
                var status=Label(card,"Status",i==1?"REQUIRES DRONE":i==2?"REQUIRES RADAR":"AVAILABLE",18,111,275,32,detail,22,Muted);
                var hover=card.GetComponent<UpgradeOption>()??card.gameObject.AddComponent<UpgradeOption>();
                var hoverFields=new SerializedObject(hover);
                hoverFields.FindProperty("statusLabel").objectReferenceValue=status;
                hoverFields.ApplyModifiedPropertiesWithoutUndo();
                var rule=Box(card,"PurchasedIndicator");Place(rule,0,0,4,156);
                var indicator=Fill(rule,new Color(.65f,.67f,.70f));indicator.enabled=false;
                var buy=card.Find("Buy").GetComponent<Button>();Place(buy.transform,308,108,124,36);
                buy.targetGraphic=Fill(buy.transform,Color.white,true);buy.transition=Selectable.Transition.ColorTint;
                var colors=buy.colors;colors.normalColor=new Color(.20f,.21f,.24f);
                colors.highlightedColor=new Color(.32f,.33f,.36f);colors.selectedColor=colors.highlightedColor;
                colors.pressedColor=Surface;colors.disabledColor=new Color(.075f,.075f,.09f);
                colors.fadeDuration=.12f;colors.colorMultiplier=1;buy.colors=colors;
                var label=Label(buy.transform,"Text (TMP)","BUY",8,0,108,36,button,18,Text);
                label.alignment=TextAlignmentOptions.Center;
                var entry=entries.GetArrayElementAtIndex(i);entry.FindPropertyRelative("upgrade").intValue=1<<i;
                entry.FindPropertyRelative("background").objectReferenceValue=background;
                entry.FindPropertyRelative("purchasedIndicator").objectReferenceValue=indicator;
                entry.FindPropertyRelative("buyButton").objectReferenceValue=buy;
                entry.FindPropertyRelative("buttonLabel").objectReferenceValue=label;
                entry.FindPropertyRelative("statusLabel").objectReferenceValue=status;
                entry.FindPropertyRelative("priceLabel").objectReferenceValue=priceText;
                entry.FindPropertyRelative("hoverFeedback").objectReferenceValue=hover;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // Keep the interface on one plane in front of the terminal housing.
            foreach(Transform child in canvas)child.localPosition=new Vector3(child.localPosition.x,child.localPosition.y,-80f);
            var camera=root.transform.Find("CameraPoint");
            camera.position=canvas.TransformPoint(new Vector3(0,0,-860));camera.rotation=canvas.rotation;
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
        const string scenePath="Assets/Scenes/Lobby/Lobby.unity";Backup(scenePath);
        var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
        bool opened=!scene.IsValid()||!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
        try
        {
            foreach(var upgrader in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BaseUpgrader>(true)))
            {
                var source=PrefabUtility.GetCorrespondingObjectFromSource(upgrader).transform;
                var canvas=source.Find("Canvas");var camera=source.Find("CameraPoint");
                var mods=PrefabUtility.GetPropertyModifications(upgrader.gameObject);
                PrefabUtility.SetPropertyModifications(upgrader.gameObject,mods.Where(m=>
                    !(m.target is RectTransform r && r!=canvas && r.IsChildOf(canvas)) && m.target!=camera).ToArray());
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        return "Styled six upgrade cards, wired purchased indicators and status labels, updated terminal framing and Lobby overrides.";
    }
}

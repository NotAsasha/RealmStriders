using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Base.Shop;

public static class AnalogShopSetup
{
    private static readonly Color Paper = new(.925f,.914f,.847f);
    private static readonly Color Ink = new(.09f,.12f,.18f);
    private static TMP_FontAsset font;
    private static T Ensure<T>(GameObject go) where T : Component { var c=go.GetComponent<T>(); return c != null ? c : go.AddComponent<T>(); }
    private static void Ref(UnityEngine.Object o,string field,UnityEngine.Object value)
    {
        var s=new SerializedObject(o);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Refs(UnityEngine.Object o,string field,UnityEngine.Object[] values)
    {
        var s=new SerializedObject(o);var p=s.FindProperty(field);p.arraySize=values.Length;
        for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];s.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Rect(Transform t,float x,float y,float w,float h)
    {
        var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);
        r.sizeDelta=new Vector2(w,h);r.anchoredPosition3D=new Vector3(x,y,0);r.localScale=Vector3.one;r.localRotation=Quaternion.identity;
    }
    private static RectTransform Box(Transform parent,string name,float x,float y,float w,float h)
    {
        var r=parent.Find(name) as RectTransform;
        if(r==null){r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);}
        Rect(r,x,y,w,h);return r;
    }
    private static Image Fill(Transform t,Color color,bool hit=false)
    {
        var i=Ensure<Image>(t.gameObject);i.sprite=null;i.color=color;i.raycastTarget=hit;return i;
    }
    private static TMP_Text Label(Transform p,string name,string text,float x,float y,float w,float h,float size,Color color)
    {
        var r=Box(p,name,x,y,w,h);var t=Ensure<TextMeshProUGUI>(r.gameObject);
        t.font=font;t.text=text;t.fontSize=size;t.fontSizeMax=size;t.fontSizeMin=size*.8f;t.enableAutoSizing=true;
        t.alignment=TextAlignmentOptions.Center;t.color=color;t.raycastTarget=false;t.fontStyle=FontStyles.Normal;return t;
    }
    private static void Bevel(Transform t)
    {
        var r=(RectTransform)t;float w=r.rect.width,h=r.rect.height;
        Fill(Box(t,"BevelTop",0,h/2-1,w,2),Color.white);
        Fill(Box(t,"BevelLeft",-w/2+1,0,2,h),Color.white);
        Fill(Box(t,"BevelBottom",0,-h/2+1,w,2),new Color(.34f,.35f,.38f));
        Fill(Box(t,"BevelRight",w/2-1,0,2,h),new Color(.34f,.35f,.38f));
    }
    public static string Apply()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode first");
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        const string path="Assets/Base/Shop/Shop.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var canvas=root.transform.Find("Canvas");var chooser=root.GetComponentInChildren<CategorySwitch>(true);
            var catalog=canvas.Find("Mask/ShopArea").gameObject;var cards=root.GetComponentsInChildren<ItemCard>(true);
            var panel=root.transform.Find("shop/Cube.002")??root.transform.Find("shop/MainPanel");panel.name="MainPanel";
            var motion=Ensure<ShopPanelMotion>(root);
            var group=Ensure<CanvasGroup>(canvas.gameObject);
            ((RectTransform)canvas).sizeDelta=new Vector2(900,680);
            canvas.localPosition=new Vector3(.005f,1.39f,0);canvas.localRotation=Quaternion.Euler(7.38f,90,0);canvas.localScale=Vector3.one*.00068f;
            var controls=Box(root.transform,"HardwareControls",0,0,1200,250);
            controls.localPosition=new Vector3(.126f,1.745f,0);controls.localRotation=Quaternion.Euler(0,90,0);controls.localScale=Vector3.one*.0007f;
            var controlCanvas=Ensure<Canvas>(controls.gameObject);controlCanvas.renderMode=RenderMode.WorldSpace;
            if(controls.GetComponent<GraphicRaycaster>()==null)controls.gameObject.AddComponent<GraphicRaycaster>();
            var buttons=chooser.transform;buttons.SetParent(controls,false);Rect(buttons,0,0,1200,250);
            var layout=buttons.GetComponent<HorizontalLayoutGroup>();if(layout!=null)UnityEngine.Object.DestroyImmediate(layout);
            var physical=new ShopCategoryButton[5];var indicators=new Image[5];int[] ratings={0,4,7,10,13};
            for(int i=0;i<5;i++)
            {
                var b=chooser.buttonComponents[i];float x=(-.29022f+.081f*i)/.0007f;
                Rect(b.transform,x,16,92,88);Fill(b.transform,Color.clear,true);b.transition=Selectable.Transition.None;
                foreach(Transform child in b.transform)child.gameObject.SetActive(false);
                Label(b.transform,"Label",(i+1).ToString(),0,0,72,45,26,Ink).gameObject.SetActive(true);
                var status=Label(b.transform,"Requirement",i==0?"OPEN":"LOCKED\nSTAR "+Mathf.CeilToInt(ratings[i]/3f),0,-64,108,45,17,Ink);status.gameObject.SetActive(true);
                var indicator=Fill(Box(b.transform,"Selected",0,-37,72,5),new Color(.16f,.65f,.19f));indicator.gameObject.SetActive(true);indicator.enabled=i==0;indicators[i]=indicator;
                physical[i]=Ensure<ShopCategoryButton>(b.gameObject);
                var mesh=root.transform.Find("shop/Cylinder."+(i+1).ToString("000"));
                Ref(physical[i],"control",b);Ref(physical[i],"physicalButton",mesh);Ref(physical[i],"buttonRenderer",mesh.GetComponent<Renderer>());Ref(physical[i],"status",status);
                var data=new SerializedObject(physical[i]);data.FindProperty("requiredRating").intValue=ratings[i];data.ApplyModifiedPropertiesWithoutUndo();
                b.onClick=new Button.ButtonClickedEvent();
                var sound=b.GetComponent<Game.UI.UIControlSound>();if(sound!=null)UnityEventTools.AddPersistentListener(b.onClick,sound.PlayClick);
                UnityEventTools.AddIntPersistentListener(b.onClick,chooser.SwitchToItem,i);
            }
            Refs(chooser,"physicalButtons",physical);Refs(chooser,"indicators",indicators);Ref(chooser,"panelMotion",motion);
            Ref(root.GetComponent<Shop>(),"categorySwitch",chooser);
            var balancePanel=Box(controls,"BalanceDisplay",330,0,360,156);Fill(balancePanel,new Color(.79f,.83f,.64f));Bevel(balancePanel);
            var money=root.GetComponentInChildren<BalanceUI>(true).transform;money.SetParent(balancePanel,false);Rect(money,0,-18,324,80);
            var moneyText=money.GetComponent<TMP_Text>();moneyText.font=font;moneyText.fontSize=58;moneyText.fontSizeMax=58;moneyText.color=new Color(.1f,.2f,.1f);moneyText.alignment=TextAlignmentOptions.Center;
            var balanceLabel=canvas.Find("BalanceLabel")??balancePanel.Find("BalanceLabel");balanceLabel.SetParent(balancePanel,false);Rect(balanceLabel,0,48,324,30);
            var bt=balanceLabel.GetComponent<TMP_Text>();bt.font=font;bt.text="TEAM BALANCE";bt.fontSize=21;bt.fontSizeMax=21;bt.fontStyle=FontStyles.Normal;bt.color=Ink;bt.alignment=TextAlignmentOptions.Center;
            canvas.Find("MenuBackdrop").gameObject.SetActive(true);Fill(canvas.Find("MenuBackdrop"),Paper);Bevel(canvas.Find("MenuBackdrop"));
            root.transform.Find("RatingScreen/Canvas").gameObject.SetActive(false);
            var title=canvas.Find("Title");Rect(title,0,300,884,62);
            var titleBar=Box(canvas,"TitleBar",0,300,884,62);Fill(titleBar,new Color(.04f,.24f,.77f));titleBar.SetSiblingIndex(1);
            var titleText=title.GetComponent<TMP_Text>();titleText.text="  Equipment Shop";titleText.font=font;titleText.fontSize=34;titleText.fontStyle=FontStyles.Bold;titleText.color=Color.white;
            titleText.alignment=TextAlignmentOptions.MidlineLeft;
            var rating=Label(canvas,"Location","Team rating: 0     |     Category 1 / 5",0,244,824,34,24,Ink);rating.alignment=TextAlignmentOptions.MidlineLeft;Ref(chooser,"ratingStatus",rating);
            var mask=canvas.Find("Mask");Rect(mask,0,0,824,432);
            var statusBar=canvas.Find("StatusBar");if(statusBar!=null)statusBar.gameObject.SetActive(false);
            foreach(var text in catalog.GetComponentsInChildren<TMP_Text>(true).Concat(cards.SelectMany(c=>c.GetComponentsInChildren<TMP_Text>(true))))
            {text.font=font;text.color=Ink;text.fontStyle=FontStyles.Normal;}
            foreach(var image in catalog.GetComponentsInChildren<Image>(true).Concat(cards.SelectMany(c=>c.GetComponentsInChildren<Image>(true))))
            {if(image.name=="Accent" || image.name=="Rule")image.color=new Color(.1f,.3f,.75f);else image.color=Paper;}
            foreach(var b in catalog.GetComponentsInChildren<Button>(true).Concat(cards.SelectMany(c=>c.GetComponentsInChildren<Button>(true))))
            {
                var colors=b.colors;colors.normalColor=Paper;colors.highlightedColor=new Color(1f,.97f,.85f);colors.pressedColor=new Color(.73f,.78f,.86f);
                colors.selectedColor=Paper;colors.disabledColor=new Color(.71f,.71f,.68f);colors.fadeDuration=.06f;b.colors=colors;
                b.GetComponent<Image>().color=Color.white;Bevel(b.transform);
            }
            foreach(var card in cards)
            {
                Ref(card,"panelMotion",motion);card.gameObject.SetActive(false);
                var back=card.transform.Find("Back").GetComponent<Button>();back.onClick=new Button.ButtonClickedEvent();
                UnityEventTools.AddPersistentListener(back.onClick,back.GetComponent<Game.UI.UIControlSound>().PlayClick);UnityEventTools.AddPersistentListener(back.onClick,card.Close);
            }
            foreach(var tile in catalog.GetComponentsInChildren<Button>(true).Where(b=>b.transform.parent.name=="Items"))
            {
                var card=Enumerable.Range(0,tile.onClick.GetPersistentEventCount()).Select(i=>tile.onClick.GetPersistentTarget(i)).Select(o=>o is ItemCard c ? c : o is GameObject g ? g.GetComponent<ItemCard>() : null).First(c=>c!=null);
                tile.onClick=new Button.ButtonClickedEvent();UnityEventTools.AddPersistentListener(tile.onClick,tile.GetComponent<Game.UI.UIControlSound>().PlayClick);UnityEventTools.AddPersistentListener(tile.onClick,card.Open);
            }
            Ref(motion,"mainPanel",panel);Ref(motion,"screen",canvas);Ref(motion,"screenInput",group);Ref(motion,"shopCanvas",canvas.GetComponent<Canvas>());Ref(motion,"controlsCanvas",controlCanvas);Ref(motion,"catalog",catalog);Refs(motion,"cards",cards);
            var camera=root.transform.Find("CameraPoint");camera.localPosition=new Vector3(-.80f,1.99f,0);camera.localRotation=Quaternion.Euler(29f,90,0);
            foreach(var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
            PrefabUtility.SaveAsPrefabAsset(root,path);
            return "Configured Shop hardware controls, balance display, XP surfaces and physical MainPanel transition.";
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }

    public static string UpdateSceneFlags()
    {
        var root=GameObject.Find("/Base/Furniture/Shop");if(root==null || root.scene.isDirty)throw new Exception("Saved Lobby scene required");
        foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            var data=new SerializedObject(t.gameObject);var p=data.FindProperty("m_StaticEditorFlags");
            if(p.prefabOverride)PrefabUtility.RevertPropertyOverride(p,InteractionMode.AutomatedAction);
        }
        EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);
        return "Shop inherits dynamic flags so moving hardware is not statically batched; other scene edits preserved.";
    }
}

